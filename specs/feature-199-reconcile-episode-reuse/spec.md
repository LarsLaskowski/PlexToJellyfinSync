# Spec: Series reconcile requests each show's episodes once

Source: Issue #199
Status: Draft

## Problem

An operator who runs the sync against a Plex server with large series libraries pays for every full reconcile
in Plex requests and reconcile time. Today, with `Sync:WriteSeriesSeasonAggregates` enabled (the default
`true`), each show of a series library has its complete episode listing
(`/library/metadata/{showKey}/allLeaves`, every page of it) requested twice per reconcile: once to write the
episode NFO files and once more, right afterwards, to compute the `season.nfo` and `tvshow.nfo` aggregates
from the same episodes. The second listing brings no new information within the same pass, doubles the
episode traffic a reconcile puts on the Plex server and lengthens every reconcile run.

Verified against the code (branch base `6709364`):

- Confirmed: the reconcile collects a show's episodes in full
  (`src/PlexToJellyfinSync.Service/LibraryReconciler.cs:144`, `GetEpisodesAsync(...).ToListAsync(...)`) and
  then calls the aggregate writer with only the show's rating key (`LibraryReconciler.cs:118-121`), which
  requests the episode listing again (`src/PlexToJellyfinSync.Service/SeriesAggregateWriter.cs:50`).
  `GetEpisodesAsync` is the paged `/library/metadata/{showRatingKey}/allLeaves` listing
  (`src/PlexToJellyfinSync.Service/PlexClient.cs:615-618`, 200 entries per page).
- Confirmed: the history path has no episode listing of its own. It reads single items by rating key
  (`SyncOrchestrator.cs:125-139`) and collects only the rating keys of the touched shows, then calls the
  aggregate writer per show (`SyncOrchestrator.cs:207-226`), so that path still needs the aggregate writer to
  request the listing itself.
- Confirmed: decision record 0018 names this double request as left unchanged by the split
  (*Consequences*, "Not changed here").
- Related, not part of this issue: the aggregate writer also requests the show's own metadata
  (`/library/metadata/{showKey}`, `SeriesAggregateWriter.cs:98`) for `tvshow.nfo`, although the reconcile
  already has the show from the library listing. See *Out of scope*.

## Scenarios

- S1: Given a series library with a show whose episodes are mapped and `Sync:WriteSeriesSeasonAggregates` is
  `true`, when a full reconcile runs, then Plex receives the show's episode listing
  (`/library/metadata/{showKey}/allLeaves`) exactly once — one pass over its pages — and the episode NFO
  files, every `season.nfo` and the `tvshow.nfo` of that show are written with the same content as today.
- S2: Given the same show, when a full reconcile runs, then the season and series watch state written into
  `season.nfo` / `tvshow.nfo` is derived from exactly the episodes, with exactly the watch state, that the same
  pass wrote into the episode NFO files.
- S3: Given an incremental history poll in which an episode of a show was watched and
  `Sync:WriteSeriesSeasonAggregates` is `true`, when the poll runs, then the show's episode listing is
  requested once for the aggregates, as today, and the aggregates are written as today.
- S4: Given `Sync:WriteSeriesSeasonAggregates` is `false`, when a full reconcile runs, then each show's
  episode listing is requested once and no `season.nfo` / `tvshow.nfo` is written, as today.
- S5: Given a series library with several shows, when a full reconcile runs, then each show's episode
  listing is requested once, so the number of episode listings per reconcile equals the number of shows
  reconciled.

## Errors and edge cases

- E1: A show without any episodes (an empty listing): the listing is requested once, no episode NFO and no
  aggregate is written, and no error is counted.
- E2: A show whose episodes have no file path or no matching path mapping: the episodes are skipped with the
  existing warnings ("Item {RatingKey} ({Title}) has no file path, skipping" / "No path mapping for
  {FilePath}, skipping"), no aggregate is written for them, and they are left out of the aggregate of a
  partly mapped show — the listing is still requested only once.
- E3: The episode listing fails (a Plex error or timeout on any of its pages): the show is skipped as one
  failed item — one "Processing item {RatingKey} failed, skipping it" error, `Errors` incremented,
  `PlexConnected` unchanged — no aggregate is written for it, the reconcile continues with the next show, and
  the listing is not requested a second time for the aggregates.
- E4: Writing one episode's NFO fails (read-only directory, I/O error): that episode counts as one failed
  item as today, and the season and series aggregates of the show are still written and still include that
  episode's Plex watch state, as today.
- E5: Writing an aggregate fails: the show counts as one failed item as today; the episode NFO files already
  written stay written.
- E6: Episodes sharing one file (a multi-episode file such as `S01E01-E02.mkv`): the episode writes stay
  grouped and sequential per file, and each of these episodes still counts in the season and series aggregate,
  as today.
- E7: A show with more episodes than one listing page (more than 200): every page is requested once and
  the aggregates include the episodes of all pages.
- E8: The watch state of an episode changes on Plex while the show is being reconciled: the aggregates
  reflect the state the pass read and wrote into the episode NFOs (today a later second listing could pick up
  the change for the aggregates only); the next history poll or reconcile writes the new state, as for any
  change after a listing was read.
- E9: The reconcile is cancelled (host shutdown) while a show is reconciled: the cancellation propagates
  as today and is not counted as an error.
- E10: Shows of different libraries reconciled concurrently (`Sync:LibraryReconcileParallelism` above 1):
  each show's aggregates are computed only from that show's own episodes.

## Non-functional requirements

- Plex load: a full reconcile with aggregates enabled requests each show's episode listing once instead of
  twice; the requests of the history path and the show metadata request are unchanged.
- Memory: no increase; a show's episodes are already held in full for the episode grouping
  (`docs/ARCHITECTURE.md`, point 2), and the aggregates reuse that collection.
- Compatibility: for unchanged Plex data, every NFO file a reconcile or a history poll writes is the same as
  today (content, created/updated/skipped outcome, counters); the guarantees of `.squad/project.md` hold
  unchanged — only the watch fields of existing NFO files are touched (0003), unmapped paths are skipped and
  the target stays under a configured local root (0004), failures stay isolated per item and per show (0006).
- Configuration: no new setting; `Sync:WriteSeriesSeasonAggregates` and `Sync:EpisodeReconcileParallelism`
  keep their meaning and defaults.
- Logs: no new, removed or changed log message; levels and categories unchanged.
- Security: none beyond today — the aggregates are still written only for episodes whose file path maps to a
  configured local root.

## Acceptance criteria

- [ ] AC1: a full reconcile of a series library with aggregates enabled requests the episode listing of each
  show exactly once, and writes the same episode, `season.nfo` and `tvshow.nfo` files (target, watch state,
  outcome counters) as before the change (S1, S5, E7).
- [ ] AC2: during a full reconcile, the season and series aggregates of a show are computed from the
  episodes and watch state of the single listing that the episode writes used (S2, E8, E10).
- [ ] AC3: an incremental history poll with aggregates enabled still requests the episode listing once for
  each show touched by an episode, and writes the same aggregates as before (S3).
- [ ] AC4: a full reconcile with aggregates disabled requests each show's episode listing once and writes no
  aggregate (S4).
- [ ] AC5: a show with an empty episode listing, or whose episodes are all without a file path or unmapped,
  gets no aggregate and no error; a partly mapped show's aggregates include only its mapped episodes; in each
  case the listing is requested once (E1, E2).
- [ ] AC6: a failing episode listing counts as one show-level error, writes no aggregate for that show, is
  not requested a second time, and the reconcile continues with the next show (E3).
- [ ] AC7: a failing episode write still leaves the show's aggregates written, including that episode's
  watch state; a failing aggregate write counts as one show-level error and keeps the episode NFOs already
  written (E4, E5).
- [ ] AC8: episodes sharing one file are still written sequentially within their group and each counts in
  the aggregates (E6).
- [ ] AC9: a cancellation during a show's reconcile propagates and is not counted as an error (E9).

## Out of scope

- The show metadata request (`/library/metadata/{showKey}`) the aggregate writer makes for `tvshow.nfo`; the
  reconcile already holds the show from the library listing, but reusing it could change the content of a
  newly created `tvshow.nfo` and is a separate change (proposed follow-up issue).
- Requesting the episode listing once for several history entries is already the case (one aggregate call
  per touched show); no change to the history path.
- The season and series directory derivation that assumes a show/season folder layout (issue #58).
- Running shows of one library concurrently, or changing paging and page size.

## Open questions

- Does the history path keep requesting the episode listing for the aggregates?
  Answered: yes. It holds only single items by rating key and the touched shows' keys
  (`SyncOrchestrator.cs:125-139`, `207-226`), not a show's full episode list; the issue says so too.
- Is it acceptable that the reconcile aggregates no longer see a watch change made on Plex between the two
  listings (E8)?
  Answered: yes, from the documentation. A reconcile is already a point-in-time pass whose pages are read once
  and whose misses are caught by the next run (`docs/ARCHITECTURE.md`, point 2, the streaming trade-off), and
  the history poll writes the aggregates again for every show an episode watch touches (decision 0005). The
  change makes the aggregates consistent with the episode NFOs of the same pass; no guarantee in
  `.squad/project.md` covers the timing of a listing.
- Does decision record 0018 stand in the way?
  Answered: no. 0018 leaves the double request open as later work in `SeriesAggregateWriter`; it states that
  `WriteAggregatesAsync` re-fetches the episodes, which this change makes untrue for the reconcile. 0018 is
  not in the latest release tag `v1.5.0` (`git ls-remote --tags origin`; its adding commit `9c93ea1` is not
  an ancestor of that tag), so the plan extends it in place.
- Does the change touch a security area?
  Answered: it touches `SeriesAggregateWriter` (*Path mapping and file writes*); whether a security control
  changes is for the plan to state. The spec requires the path-mapping skip of every episode used for an
  aggregate to stay as it is.

## Area documents

No area document exists yet (`docs/areas/README.md` has an empty index). The plan decides the area; the
proposed one is a new `docs/areas/sync-pipeline.md` ("Sync pipeline": the history poll and the full
reconcile, what each writes and requests from Plex), which would state:

- a full reconcile reads each show's episode listing once per run and derives the episode NFOs and the season
  and series aggregates from that one listing;
- the history poll reads the episode listing once per show touched by an episode, for the aggregates only;
- the aggregates include only episodes with a mapped file path, and are written only when
  `Sync:WriteSeriesSeasonAggregates` is `true`.

Besides the area document, `docs/ARCHITECTURE.md` point 2 ("it re-fetches all episodes of that show") and
decision record 0018 (*Decision*, `ISeriesAggregateWriter`, and *Consequences*, "Not changed here") describe
today's double request and change with this feature; their owners are set in the plan.

## Challenge notes

Devil's Advocate, mode `spec`: RESULT: OBJECTIONS (0 major, 2 minor).

1. minor: AC2 would pass a wrong build as tested today. `FakePlexClient.GetEpisodesAsync` (`tests/PlexToJellyfinSync.Tests/FakePlexClient.cs:194-213`) returns the same `MediaItem` instances on every call, so a build that still fetches twice computes the same aggregates and passes AC2. Instruction for the plan: prove AC2 with a test that fails if the aggregates come from a second listing. Either have the fake return different watch states on its second call for the same show, or assert `Assert.HasCount(1, _plexClient.EpisodeRequests, ...)` in the same reconcile test that checks the aggregate content. The history test `SyncOrchestratorProcessHistoryAggregatesEachSeriesOnlyOnce` (`tests/PlexToJellyfinSync.Tests/SyncOrchestratorTests.cs:327-341`) uses that same count for the history path.
2. minor: The paging part of AC1 and E7 cannot be checked at the reconciler level. `FakePlexClient` has no pages; paging lives inside `PlexClient.GetEpisodesAsync` (`src/PlexToJellyfinSync.Service/PlexClient.cs:615-618`) and is covered by the existing `PlexClientTests` (around lines 899-1000). Instruction for the plan: at the reconciler, state "every page once" as "`GetEpisodesAsync` enumerated once per show". Prove "aggregates include episodes of all pages" with a reconciler test whose listing holds more than one fake page's worth of episodes (e.g. 201) and whose `tvshow.nfo` reflects all of them. Do not add a new paging test to `PlexClient`.