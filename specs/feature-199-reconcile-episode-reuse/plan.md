# Plan: Series reconcile requests each show's episodes once

Source: Issue #199 | [spec.md](spec.md)
Status: Revised (1)
Tier: standard — a behavior change in the sync pipeline (one Plex request fewer per show) that creates, changes or removes no security control: the path-mapping skip of every aggregated episode stays the same code path.
Security areas touched: Path mapping and file writes (`SeriesAggregateWriter`; the aggregate NFO writes through `MediaItemWriter`/`NfoWriter` are unchanged)

## Problem / root cause

See [spec.md](spec.md), *Problem*: `LibraryReconciler` lists a show's episodes for the episode writes
(`src/PlexToJellyfinSync.Service/LibraryReconciler.cs:144`) and then hands only the show's rating key to
`SeriesAggregateWriter.WriteAggregatesAsync` (`LibraryReconciler.cs:120`), which lists them again
(`src/PlexToJellyfinSync.Service/SeriesAggregateWriter.cs:50`).

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | A series reconcile with aggregates enabled lists each show's episodes twice | `LibraryReconciler.cs:116-121` calls `ReconcileShowEpisodesAsync` (`:144`, `GetEpisodesAsync(...).ToListAsync`) and then `WriteAggregatesAsync(show.RatingKey, ...)`, which enumerates `GetEpisodesAsync` again (`SeriesAggregateWriter.cs:50`) | confirmed |
| 2 | `GetEpisodesAsync` is the paged `/library/metadata/{key}/allLeaves` listing, 200 per page | `PlexClient.cs:615-618`, `GetPagedMediaItemsAsync` (`PlexClient.cs:425-466`), `LibraryPageSize = 200` (`PlexClient.cs:52`) | confirmed |
| 3 | The history path holds no episode list and must keep its own listing | `SyncOrchestrator.cs:125-139` (single items by rating key), `:207-226` (aggregate call with the show key only) | confirmed |
| 4 | The reconcile already holds the complete episode list before the aggregates | `ToListAsync` at `LibraryReconciler.cs:144`; the episode groups finish (`Parallel.ForEachAsync` awaited, `:153`) before `WriteAggregatesAsync` runs (`:118-121`) | confirmed |
| 5 | Nothing on the write path changes a `MediaItem` it is given, so the reused list carries the Plex watch state unchanged | `MediaItemWriter.cs:68-89` only reads the item; `NfoWriter.cs` assigns no member of `item` (`grep -n "item\.\w* ="` finds only comparisons) | confirmed |
| 6 | `ISeriesAggregateWriter` has one implementation and no test double; its consumers are `LibraryReconciler` and `SyncOrchestrator` | `grep -rn "ISeriesAggregateWriter\|WriteAggregatesAsync" src tests`: `SeriesAggregateWriter.cs`, `LibraryReconciler.cs:120`, `SyncOrchestrator.cs:215`, `ServiceCollectionExtensions.cs:49`, `SeriesAggregateWriterTests.cs`, `ServiceCollectionExtensionsTests.cs:60-69` | confirmed |
| 7 | `FakePlexClient` records every episode listing per show in `EpisodeRequests` and can fail one via `EpisodesExceptions` | `tests/PlexToJellyfinSync.Tests/FakePlexClient.cs:98`, `:113`, `:194-213` | confirmed |
| 8 | `RecordingNfoWriter.FailuresByRatingKey` can fail the series aggregate write: the series item comes from `FakePlexClient.Items[showKey]`, whose `RatingKey` is the show key | `RecordingNfoWriter.cs:78`; `LibraryReconcilerTests.cs:464-469` (`AddShow`); `SeriesAggregateWriter.cs:98` | confirmed |
| 9 | All shows of `LibraryReconcilerTests.AddShow` share one season directory, so per-show assertions must use the written item (`Item.RatingKey` of the series write), not the target path | `LibraryReconcilerTests.cs:19`, `:460` | confirmed |
| 10 | Record 0018 is unreleased and can be extended in place | commit `9c93ea1` (adds 0018, 2026-10-03) is not an ancestor of `v1.5.0` (`487c34b`, latest tag per `git ls-remote --tags origin`), which is an ancestor of `HEAD` | confirmed |
| 11 | Defining the first area document would force an `Area:` line into released records | `decision-check.py` `check_areas` requires the field in every record once `docs/areas/README.md` lists an area; 0001 (`d626590`) is contained in `v1.5.0` | confirmed |

## Acceptance criteria

The criteria are AC1–AC9 of [spec.md](spec.md).

## Tests per criterion

All new tests are written first by the Tester; "existing" tests stay unchanged and must stay green.
Assertions on the series aggregate of one show use the written item (`Item.RatingKey`, `Item.Watch`), see
*Verified facts* 9.

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | `LibraryReconcilerSeriesReconcileRequestsEpisodesOncePerShow` (two shows, aggregates on: `EpisodeRequests` equals `["s1", "s2"]`, 5 episode, 2 season and 2 series writes); `LibraryReconcilerShowWithMoreThanOnePageAggregatesAllEpisodes` (201 episodes, the first 200 watched, the 201st not: series and season aggregates unwatched, one entry in `EpisodeRequests`); existing `LibraryReconcilerSeriesLibraryWritesEpisodesAndAggregates` | `tests/PlexToJellyfinSync.Tests/LibraryReconcilerTests.cs` |
| AC2 | `LibraryReconcilerSeriesAggregatesFollowEachShowsOwnListing` (show s1 all watched, s2 with one unwatched episode: s1's series item watched with the newest `LastPlayed`, s2's unwatched, and exactly one `EpisodeRequests` entry per show in the same run); `SeriesAggregateWriterGivenEpisodesAggregatesThemWithoutRequest` (the fake lists unwatched episodes for s1, the passed list holds watched copies: the season and series items are watched and `EpisodeRequests` is empty) | `tests/PlexToJellyfinSync.Tests/LibraryReconcilerTests.cs`, `tests/PlexToJellyfinSync.Tests/SeriesAggregateWriterTests.cs` |
| AC3 | `SeriesAggregateWriterWithoutEpisodesRequestsListingOnce` (rating-key overload: one `EpisodeRequests` entry, aggregates written); existing `SyncOrchestratorProcessHistoryAggregatesEachSeriesOnlyOnce` and the existing `SeriesAggregateWriterTests` on the rating-key overload | `tests/PlexToJellyfinSync.Tests/SeriesAggregateWriterTests.cs`, `tests/PlexToJellyfinSync.Tests/SyncOrchestratorTests.cs` |
| AC4 | `LibraryReconcilerAggregatesDisabledRequestsEpisodesOnce` (one `EpisodeRequests` entry, no season or series write); existing `LibraryReconcilerSeriesLibraryAggregatesDisabledWritesEpisodesOnly` | `tests/PlexToJellyfinSync.Tests/LibraryReconcilerTests.cs` |
| AC5 | `LibraryReconcilerShowWithoutEpisodesWritesNothingAndCountsNoError`; `LibraryReconcilerPartlyMappedShowAggregatesOnlyMappedEpisodes` (one unwatched episode unmapped via `StubPathMapper.Unmapped`, one unwatched without a file path, the rest watched: series item watched, `Errors` 0, one `EpisodeRequests` entry); `SeriesAggregateWriterGivenEpisodesLeavesOutUnmappedEpisodes` (passed list with a mapped, an unmapped and a file-less episode: only the mapped one counts); `SeriesAggregateWriterGivenOnlyUnmappedEpisodesWritesNothing` | `tests/PlexToJellyfinSync.Tests/LibraryReconcilerTests.cs`, `tests/PlexToJellyfinSync.Tests/SeriesAggregateWriterTests.cs` |
| AC6 | `LibraryReconcilerFailingEpisodeListingWritesNoAggregateAndIsNotRequestedAgain` (aggregates on, `EpisodesExceptions["s1"]`: `EpisodeRequests` equals `["s1", "s2"]`, `Errors` 1, `PlexConnected` unchanged, series writes only for s2) — a regression guard that passes on today's build too, not one of the failing tests first (*Challenge* 2); existing `LibraryReconcilerFailingShowIsIsolated` | `tests/PlexToJellyfinSync.Tests/LibraryReconcilerTests.cs` |
| AC7 | `LibraryReconcilerFailingEpisodeWriteStillAggregatesItsWatchState` (aggregates on, e2 unwatched and failing: `Errors` 1, series item unwatched); `LibraryReconcilerFailingAggregateWriteKeepsEpisodesAndCountsOneError` (`FailuresByRatingKey["s1"]`: all episodes written, `Errors` 1) | `tests/PlexToJellyfinSync.Tests/LibraryReconcilerTests.cs` |
| AC8 | `LibraryReconcilerSharedFileEpisodesCountInAggregates` (two watched episodes on one file plus one unwatched: both shared-file episodes written, series item unwatched; with all watched: watched); existing `LibraryReconcilerSharedFileEpisodesNeverOverlap` | `tests/PlexToJellyfinSync.Tests/LibraryReconcilerTests.cs` |
| AC9 | `LibraryReconcilerCancellationWithAggregatesPropagatesWithoutError` (the existing cancellation setup with aggregates on); existing `LibraryReconcilerCancellationPropagatesWithoutError` | `tests/PlexToJellyfinSync.Tests/LibraryReconcilerTests.cs` |

## Approach

1. `ISeriesAggregateWriter` gains a second `WriteAggregatesAsync` overload that takes the show's episodes as
   an already listed `IReadOnlyList<MediaItem>`. The existing rating-key overload keeps its signature and
   meaning for the history path and keeps **streaming** the listing: it never collects the full listing.
2. `SeriesAggregateWriter` is split into two private members that both overloads share:
   - `MapEpisodePath` holds the path filter exactly as today (`SeriesAggregateWriter.cs:52-62`): `null` for an
     episode without a file path (`string.IsNullOrWhiteSpace`) or without a matching mapping
     (`_pathMapper.MapToLocal` returns `null`), otherwise the mapped local path. It is the only place the 0004
     skip happens for aggregates.
   - `WriteMappedAggregatesAsync` holds the rest of today's body unchanged (`SeriesAggregateWriter.cs:65-107`:
     the empty check, the season grouping and writes, the show directory derivation, the show metadata request
     and the series write) and takes the mapped (episode, local path) pairs.
   - The rating-key overload enumerates `GetEpisodesAsync` with `await foreach` and adds each episode for which
     `MapEpisodePath` returns a path to the mapped list while enumerating, as today; the list overload runs
     the same `MapEpisodePath` over every passed episode. Both then call `WriteMappedAggregatesAsync`.
   The caller passes the raw listing, never a pre-filtered or pre-mapped one (the security property of this
   change, *Security considerations*).
3. `LibraryReconciler.ReconcileShowEpisodesAsync` returns the list it collected; `ReconcileSeriesLibraryAsync`
   passes it to the new overload. The `try`/`catch` around both calls, the `WriteSeriesSeasonAggregates`
   condition and the order (all episode groups awaited before the aggregates) stay unchanged.
4. `SyncOrchestrator` is unchanged.

No new user-visible output: log texts and the dashboard are unchanged.

## Options considered

1. **Overload with the listed episodes (chosen)** — the history path and its tests stay untouched, the
   aggregation and the path-mapping filter stay in one place.
2. **One method with an optional, nullable episode parameter** — rejected: two meanings behind one
   signature, and a forgotten argument silently falls back to a second request.
3. **Cache the listing in `PlexClient`** — rejected: makes a stateless singleton stateful (0018) and needs an
   invalidation rule across concurrent libraries.
4. **Compute the aggregates in `LibraryReconciler`** — rejected: duplicates the aggregation and the 0004 skip
   that 0018 places in `SeriesAggregateWriter`.
5. **Change the only method to take the episodes and list them in `SyncOrchestrator`** — rejected: moves a
   Plex listing into the orchestrator's history loop and changes every existing `SeriesAggregateWriterTests`
   call site for no gain.

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| `PlexToJellyfinSync.Core` | `src/PlexToJellyfinSync.Core/Abstractions/ISeriesAggregateWriter.cs` | new overload; XML docs of both overloads (the rating-key one no longer "re-fetches" in every case) |
| `PlexToJellyfinSync.Service` | `src/PlexToJellyfinSync.Service/SeriesAggregateWriter.cs` | new overload holds the body; rating-key overload lists and delegates |
| `PlexToJellyfinSync.Service` | `src/PlexToJellyfinSync.Service/LibraryReconciler.cs` | `ReconcileShowEpisodesAsync` returns the list; the series loop passes it |
| `PlexToJellyfinSync.Tests` | `LibraryReconcilerTests.cs`, `SeriesAggregateWriterTests.cs` | new tests (*Tests per criterion*) |

## Signatures (for the Dev's skeleton)

In `ISeriesAggregateWriter` (new; the interface file gains a using directive for PlexToJellyfinSync.Core.Models)
and implemented in `SeriesAggregateWriter` inside its ISeriesAggregateWriter region:

- `Task WriteAggregatesAsync(string showRatingKey, IReadOnlyList<MediaItem> episodes, CancellationToken cancellationToken);`
  — `showRatingKey` is still needed for the show metadata request (`SeriesAggregateWriter.cs:98`).

Unchanged signature, changed body and docs: `Task WriteAggregatesAsync(string showRatingKey, CancellationToken cancellationToken);`

New private members of `SeriesAggregateWriter` (in a Methods region before its ISeriesAggregateWriter region,
XML docs on both):

- `private string? MapEpisodePath(MediaItem episode)` — the mapped local path, or null when the episode has
  no file path or no matching mapping.
- `private async Task WriteMappedAggregatesAsync(string showRatingKey, List<(MediaItem Episode, string Local)> mapped, CancellationToken cancellationToken)`

Changed private member in `LibraryReconciler` (in its Methods region):

- `private async Task<IReadOnlyList<MediaItem>> ReconcileShowEpisodesAsync(string showRatingKey, CancellationToken cancellationToken)`

The skeleton rewrites `ISeriesAggregateWriter.cs` and `SeriesAggregateWriter.cs` (the new overload throws
`NotImplementedException` until step 6; the two private members come with the implementation, task 4, since
the rating-key overload keeps today's body until then); `LibraryReconciler.cs` keeps calling the rating-key overload until
step 6, so the existing tests stay green after the skeleton.

## Test files

- `tests/PlexToJellyfinSync.Tests/LibraryReconcilerTests.cs`
- `tests/PlexToJellyfinSync.Tests/SeriesAggregateWriterTests.cs`

Existing test call sites of a changed signature: none. The rating-key overload keeps its signature
(`SeriesAggregateWriterTests.cs`, the factories `LibraryReconcilerTests.cs:406` and
`SyncOrchestratorTests.cs:1553` construct `SeriesAggregateWriter` with an unchanged constructor).

Files of the change no unit test can cover: none.

## Areas

None. No area is defined yet, and defining the first one would require an `Area:` line in released decision
records (*Verified facts* 11), which are append-only; that is a change of its own (*Out of scope /
follow-ups*). The behavior stays documented in `docs/ARCHITECTURE.md` point 2.

## Documentation updates

Sweep: `grep -rn -i "re-fetch\|allLeaves\|twice\|WriteAggregatesAsync" docs README.md .squad/project.md` —
hits in `docs/ARCHITECTURE.md` (point 2, "it re-fetches all episodes of that show", lines 106-109) and record
0018; `README.md` (configuration table, `Sync:WriteSeriesSeasonAggregates`) stays true; `.squad/project.md`
needs no change (no guarantee or security area changes).

- `docs/ARCHITECTURE.md` point 2, the `Sync:WriteSeriesSeasonAggregates` bullet: the reconcile passes the
  episodes it listed for the episode writes (one listing per show), the history path lists each touched
  show's episodes once; link 0018 — owner **Lead** (task 7).
- `docs/decisions/0018-sync-orchestrator-split-into-collaborators.md` — extended in place in this step
  (Lead, done).
- XML docs of `ISeriesAggregateWriter` — owner **Dev** (task 1).

## Architecture check

The guarantees of `.squad/project.md` are preserved: NFO files are still written only through
`MediaItemWriter`/`NfoWriter` (0003 unchanged); every aggregated episode still passes the file-path check and
`PathMapper.MapToLocal` inside `SeriesAggregateWriter`, and `NfoWriter` still checks the local root (0004);
the show-level `try`/`catch` in `LibraryReconciler` and the history loop's isolation are untouched (0006);
the history/reconcile split is unchanged (0005). All three collaborators stay stateless singletons; the list
lives only in one call of `ReconcileSeriesLibraryAsync`, so concurrent libraries never share it (E10).
Memory does not increase on either path (spec, *Non-functional requirements*): the reconcile reuses the list
it already holds for the episode grouping, and the history path keeps streaming the listing and holds only the
mapped (episode, local path) pairs, as today. The 0004 skip for aggregates is still decided in one place,
`MapEpisodePath`, used by both overloads.

## Security considerations

- [x] Every byte or length limit on parsed input says whether it applies before or after decoding — no
  limits.
- [x] Every exception that can reach a user-visible failure reason or message is listed — none new: a failing
  listing or write reaches `LastError` through the existing `HandleItemError` as today.

The one security-relevant property: the new overload must not trust its input more than the old path did.
It receives unfiltered episodes and applies the same file-path and mapping skip to each, through the
same `MapEpisodePath` the rating-key overload uses
(`SeriesAggregateWriterGivenEpisodesLeavesOutUnmappedEpisodes`, `SeriesAggregateWriterGivenOnlyUnmappedEpisodesWritesNothing`).

## Decision records

- `docs/decisions/0018-sync-orchestrator-split-into-collaborators.md` (Accepted, unreleased) — extended in
  place: *Source*, the `ISeriesAggregateWriter` bullet of *Decision*, and in *Consequences* the rejected
  options and the accepted snapshot consequence; the "Not changed here" bullet now names only issue #58.

## Out of scope / follow-ups

Proposed follow-up issues (created by the orchestrator):

1. Title: "[Performance] Series reconcile requests each show's metadata again for tvshow.nfo". Body:
   "During a full reconcile, `SeriesAggregateWriter.WriteAggregatesAsync` requests `/library/metadata/{showKey}`
   for the `tvshow.nfo` series item although `LibraryReconciler` already holds the show from
   `/library/sections/{key}/all`. Reusing it saves one request per show, but may change a newly created
   `tvshow.nfo` if the listing carries fewer fields than the single-item endpoint, which needs checking first.
   Found while working on #199."
2. Title: "[Docs] Introduce area documents". Body: "`docs/areas/` has no area yet. Listing the first one makes
   `decision-check.py` require an `Area:` line in every decision record, including records released in
   `v1.5.0`, which are append-only. Decide how released records get their area (an amending record, or an
   exemption in the check) and then move the behavior from `docs/ARCHITECTURE.md` into area documents. Found
   while working on #199."

## Challenge

Devil's Advocate, mode `plan`: 0 major, 2 minor. Both accepted.

1. minor — the rating-key overload collected the full listing with `ToListAsync`, so the history path would
   hold more memory than today, against the spec's "Memory: no increase". Accepted: the history path keeps
   streaming; the path filter moves into `MapEpisodePath` and the aggregation and writes into
   `WriteMappedAggregatesAsync`, both shared by the two overloads (*Approach* 2, *Signatures*, *Architecture
   check*, task 4). The spec's memory requirement stays as written.
2. minor — `LibraryReconcilerFailingEpisodeListingWritesNoAggregateAndIsNotRequestedAgain` passes on today's
   build. Accepted: it stays as a regression guard for AC6 and does not count among the tests that must fail
   first; AC1 and AC2 prove the change (*Tests per criterion*, task 3).