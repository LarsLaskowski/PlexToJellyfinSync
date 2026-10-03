# Plan: Split SyncOrchestrator into an item writer, a series aggregate writer and a library reconciler

Source: Issue #56
Status: Draft
Tier: security — a behavior-neutral refactoring, but it relocates the code that enforces the "unmapped paths
are always skipped" guarantee (0004) and that derives the `season.nfo` / `tvshow.nfo` write targets, and
`.squad/routing.md` puts any change touching path mapping or a file write into `security` (when in doubt, the
higher tier).

## Problem / root cause

Not a bug: a structural finding (F-407). `src/PlexToJellyfinSync.Service/SyncOrchestrator.cs` mixes several
concerns in one type, which makes the aggregation and reconcile logic testable only through the two public
entry points.

Claims of the issue, checked against the code (base `origin/main`, `37a5841`):

| Claim | Result |
| ----- | ------ |
| "~420 lines", `SyncOrchestrator.cs:14-420` | **Refuted (stale):** the file now has 579 lines (class at 14–579); it grew with the parallel reconcile. The finding is stronger, not weaker. |
| Owns owner-account resolution/caching | Confirmed: `ResolveOwnerAsync` (78–83), cache cleared in `HandleError` (107–117). |
| Owns the incremental history pipeline | Confirmed: `ProcessHistoryAsync` (352–407), `ProcessHistoryEntriesAsync` (268–293), `TryProcessRatingKeyAsync` (301–317), `ProcessRatingKeyAsync` (170–184). |
| Owns full reconcile for movie and series libraries | Confirmed: `ReconcileAsync` (410–446), `ReconcileLibraryAsync` (455–465), `ReconcileMovieLibraryAsync` (473–494), `ReconcileSeriesLibraryAsync` (502–526), `ReconcileShowEpisodesAsync` (536–548), `WriteEpisodeGroupAsync` (557–576). |
| Owns season/series aggregate computation and writing | Confirmed: `UpdateSeriesAggregatesAsync` (192–258), `UpdateAffectedShowAggregatesAsync` (326–345). |
| Owns path-mapping orchestration | Confirmed: `WriteItemAsync` (141–162) and the episode mapping loop in `UpdateSeriesAggregatesAsync` (196–209). |
| Owns status mutation and error handling | Confirmed: `RecordOutcome` (89–99), `HandleError`, `HandleItemError` (125–133), status updates throughout. |
| "Hard to unit-test in isolation (F-416)" | **Partly refuted:** `tests/PlexToJellyfinSync.Tests/SyncOrchestratorTests.cs` (1705 lines, 45 tests) already covers watermark advancement, per-item isolation, aggregates, parallelism and cancellation — but only end to end through `SyncOrchestrator`; aggregation and reconcile have no focused tests. |
| "Makes the per-item error gap (F-406) easy to miss" | **Refuted as current:** per-item isolation exists (`HandleItemError`, decision 0006) on every per-item call. |

Related findings on the way (not fixed here, the change stays behavior-neutral):

- The private reconcile helpers `ReconcileLibraryAsync`, `ReconcileMovieLibraryAsync`,
  `ReconcileSeriesLibraryAsync`, `ReconcileShowEpisodesAsync` and `WriteEpisodeGroupAsync` sit inside
  `#region ISyncOrchestrator` (448–576) — open issue #59 (F-410). They move out of the class here, so #59 is
  resolved as a side effect; the PR may reference it.
- A series reconcile requests each show's episodes twice: `ReconcileShowEpisodesAsync` (538) and again
  `UpdateSeriesAggregatesAsync` (196). An avoidable extra `/allLeaves` request per show per reconcile; proposed
  as a follow-up issue (see below).
- Season/series target directories assume a show/season folder layout (218, 238–239) — open issue #58,
  unchanged.

## Acceptance criteria

`MediaItemWriter` (new):

- [ ] AC1: `WriteItemAsync` with a `null`, empty or whitespace `FilePath` does not call `INfoWriter` and
  changes neither `NfoCreated` nor `NfoUpdated`.
- [ ] AC2: `WriteItemAsync` with a file path for which `IPathMapper.MapToLocal` returns `null` does not call
  `INfoWriter` and changes no counter (guarantee 0004).
- [ ] AC3: `WriteItemAsync` with a mapped path calls `INfoWriter.WriteAsync` once with the item and the mapped
  local path; outcome `Created` increments `NfoCreated` by 1, `Updated` increments `NfoUpdated` by 1,
  `Skipped` increments neither.
- [ ] AC4: `WriteAggregateAsync` calls `INfoWriter.WriteAsync` with the item and the given directory unchanged
  (no path mapping) and counts the outcome as in AC3.
- [ ] AC5: An exception thrown by `INfoWriter.WriteAsync` propagates out of both methods (isolation is the
  caller's job) and no counter changes.

`SeriesAggregateWriter` (new):

- [ ] AC6: For a show with two mapped season-1 episodes, of which one is watched, `WriteAggregatesAsync` writes
  one `MediaKind.Season` item (`SeasonNumber = 1`, `Title = "Season 1"`, `Watched = false`) to the episodes'
  directory and one `MediaKind.Series` item (title from the show metadata, `Watched = false`) to the parent of
  that directory.
- [ ] AC7: When every mapped episode is watched, the season and series items are `Watched = true` with
  `LastPlayed` equal to the latest episode `LastPlayed`.
- [ ] AC8: Episodes in two seasons in two season directories produce one season write per season, each to its
  own directory, and one series write.
- [ ] AC9: A season whose episodes have no season number is written with `Title = "Season"`.
- [ ] AC10: Episodes without a file path or without a path mapping are left out of the aggregates; when no
  episode is mapped, nothing is written and the show metadata is not requested.
- [ ] AC11: When the show metadata is not found, the series item's title is the first mapped episode's
  `ShowTitle`, or empty when that is `null`.
- [ ] AC12: A mapped local path without a parent directory (e.g. `S01E01.mkv`) produces neither a season nor a
  series write.
- [ ] AC13: An exception from `IPlexClient` or the writer propagates (isolation is the caller's job).

`LibraryReconciler` (new):

- [ ] AC14: A movie library: every movie is written through `IMediaItemWriter.WriteItemAsync` and
  `ItemsProcessed` grows by the number of movies.
- [ ] AC15: A series library: every episode of every show is written and `ItemsProcessed` grows by the number of
  episodes; with `WriteSeriesSeasonAggregates = true` the season and series aggregates are written, with
  `false` they are not.
- [ ] AC16: A library of another kind (e.g. `MediaKind.Unknown`) causes no Plex item request
  (`FakePlexClient.LibraryItemRequests` stays empty) and no write.
- [ ] AC17: A failing movie, a failing episode and a failing show (e.g. its episode request throws) are each
  counted in `Errors` with `LastError` set, `PlexConnected` is not changed, and the remaining movies / episodes
  / shows are still written; this includes an `OperationCanceledException` that is not caused by the caller's
  token (timeout).
- [ ] AC18: Episodes of one show are written concurrently up to `EpisodeReconcileParallelism`; a value of 0 or
  less is treated as 1; episodes sharing one `FilePath` are never written concurrently.
- [ ] AC19: Cancelling the caller's token propagates an `OperationCanceledException` and is not counted as an
  error.
- [ ] AC20: An exception while enumerating the library's items (`GetLibraryItemsAsync`) propagates to the
  caller, and a movie streamed before the failure is still written.

`SyncOrchestrator` and DI:

- [ ] AC21: Every existing test in `SyncOrchestratorTests` passes with only `CreateOrchestrator` changed (it wires
  the real `MediaItemWriter`, `SeriesAggregateWriter` and `LibraryReconciler` to the existing fakes); no test
  method, assertion or assert message is changed or removed.
- [ ] AC22: `AddPlexToJellyfinSync` registers `IMediaItemWriter`, `ISeriesAggregateWriter` and
  `ILibraryReconciler` as singletons, and `ISyncOrchestrator` still resolves (`ServiceCollectionExtensionsTests`).
- [ ] AC23 (checked in review, not by a test): `SyncOrchestrator` no longer takes `IPathMapper`, `INfoWriter`
  or `WatchAggregator`, and `#region ISyncOrchestrator` holds only `ProcessHistoryAsync` and `ReconcileAsync`.

## Approach

Pure extract-class refactoring; every moved line keeps its logic, order of status updates, log message text
and exception filters (`catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)`).

1. `MediaItemWriter` takes over `WriteItemAsync` (141–162) and `RecordOutcome` (89–99); `WriteAggregateAsync`
   is the `_nfoWriter.WriteAsync` + `RecordOutcome` pair used at 233–235 and 255–257.
2. `SeriesAggregateWriter` takes over `UpdateSeriesAggregatesAsync` (192–258) verbatim, with the two write
   calls replaced by `IMediaItemWriter.WriteAggregateAsync`. It does not check
   `WriteSeriesSeasonAggregates`; callers keep that check where it is today.
3. `LibraryReconciler` takes over `ReconcileLibraryAsync`, `ReconcileMovieLibraryAsync`,
   `ReconcileSeriesLibraryAsync`, `ReconcileShowEpisodesAsync`, `WriteEpisodeGroupAsync` (455–576) and its own
   copy of `HandleItemError` (same log message and status fields); the
   `WriteSeriesSeasonAggregates` check at 512 and the episode parallelism clamp at 543 move with them.
4. `SyncOrchestrator` keeps `ResolveOwnerAsync`, `HandleError`, `HandleItemError`, `ProcessRatingKeyAsync`
   (now calling `IMediaItemWriter.WriteItemAsync`), `ProcessHistoryEntriesAsync`, `TryProcessRatingKeyAsync`,
   `UpdateAffectedShowAggregatesAsync` (now calling `ISeriesAggregateWriter.WriteAggregatesAsync`) and the two
   entry points; `ReconcileAsync` passes `_libraryReconciler.ReconcileLibraryAsync` to `Parallel.ForEachAsync`.
   The library filter and the library parallelism clamp stay here.
5. Register the three new types as singletons in `ServiceCollectionExtensions.AddPlexToJellyfinSync`. They are
   stateless, so concurrent library reconciles share them safely.

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| Core | `Abstractions/IMediaItemWriter.cs` | new interface |
| Core | `Abstractions/ISeriesAggregateWriter.cs` | new interface |
| Core | `Abstractions/ILibraryReconciler.cs` | new interface |
| Service | `MediaItemWriter.cs` | new class |
| Service | `SeriesAggregateWriter.cs` | new class |
| Service | `LibraryReconciler.cs` | new class |
| Service | `SyncOrchestrator.cs` | constructor and dependencies changed, moved members removed |
| Service | `ServiceCollectionExtensions.cs` | three new singleton registrations |
| Tests | `SyncOrchestratorTests.cs` | `CreateOrchestrator` wiring only (AC21) |
| Tests | `FakePlexClient.cs` | may gain a recorded `MediaItemRequests` list for AC10 (additive only) |

## Signatures (for the Dev's skeleton)

All new types follow the code style (file-scoped namespace, `#region`, XML docs, `.ConfigureAwait(false)`).

```csharp
// src/PlexToJellyfinSync.Core/Abstractions/IMediaItemWriter.cs
namespace PlexToJellyfinSync.Core.Abstractions;

public interface IMediaItemWriter
{
    // Movie/episode: skip without file path or path mapping, else write at the mapped local path and count the outcome
    Task WriteItemAsync(MediaItem item, CancellationToken cancellationToken);

    // Season/series: write to an already-local directory and count the outcome
    Task WriteAggregateAsync(MediaItem item, string localDirectory, CancellationToken cancellationToken);
}

// src/PlexToJellyfinSync.Core/Abstractions/ISeriesAggregateWriter.cs
public interface ISeriesAggregateWriter
{
    Task WriteAggregatesAsync(string showRatingKey, CancellationToken cancellationToken);
}

// src/PlexToJellyfinSync.Core/Abstractions/ILibraryReconciler.cs
public interface ILibraryReconciler
{
    ValueTask ReconcileLibraryAsync(PlexLibrary library, CancellationToken cancellationToken);
}
```

```csharp
// src/PlexToJellyfinSync.Service/MediaItemWriter.cs
public sealed class MediaItemWriter : IMediaItemWriter
{
    public MediaItemWriter(INfoWriter nfoWriter,
                           IPathMapper pathMapper,
                           ISyncStatusProvider status,
                           ILogger<MediaItemWriter> logger);

    private void RecordOutcome(NfoWriteOutcome outcome);

    #region IMediaItemWriter
    public Task WriteItemAsync(MediaItem item, CancellationToken cancellationToken);
    public Task WriteAggregateAsync(MediaItem item, string localDirectory, CancellationToken cancellationToken);
    #endregion
}

// src/PlexToJellyfinSync.Service/SeriesAggregateWriter.cs
public sealed class SeriesAggregateWriter : ISeriesAggregateWriter
{
    public SeriesAggregateWriter(IPlexClient plexClient,
                                 IPathMapper pathMapper,
                                 IMediaItemWriter itemWriter,
                                 WatchAggregator aggregator);

    #region ISeriesAggregateWriter
    public Task WriteAggregatesAsync(string showRatingKey, CancellationToken cancellationToken);
    #endregion
}

// src/PlexToJellyfinSync.Service/LibraryReconciler.cs
public sealed class LibraryReconciler : ILibraryReconciler
{
    public LibraryReconciler(IPlexClient plexClient,
                             IMediaItemWriter itemWriter,
                             ISeriesAggregateWriter aggregateWriter,
                             ISyncStatusProvider status,
                             IOptions<SyncOptions> syncOptions,
                             ILogger<LibraryReconciler> logger);

    private void HandleItemError(Exception ex, string ratingKey);
    private Task ReconcileMovieLibraryAsync(string libraryKey, CancellationToken cancellationToken);
    private Task ReconcileSeriesLibraryAsync(string libraryKey, CancellationToken cancellationToken);
    private Task ReconcileShowEpisodesAsync(string showRatingKey, CancellationToken cancellationToken);
    private ValueTask WriteEpisodeGroupAsync(IEnumerable<MediaItem> episodeGroup, CancellationToken cancellationToken);

    #region ILibraryReconciler
    public ValueTask ReconcileLibraryAsync(PlexLibrary library, CancellationToken cancellationToken);
    #endregion
}

// src/PlexToJellyfinSync.Service/SyncOrchestrator.cs — changed constructor
public SyncOrchestrator(IPlexClient plexClient,
                        IMediaItemWriter itemWriter,
                        ISeriesAggregateWriter aggregateWriter,
                        ILibraryReconciler libraryReconciler,
                        IStateStore stateStore,
                        ISyncStatusProvider status,
                        IOptions<PlexOptions> plexOptions,
                        IOptions<SyncOptions> syncOptions,
                        ILogger<SyncOrchestrator> logger);
// Removed private members: RecordOutcome, WriteItemAsync, UpdateSeriesAggregatesAsync, ReconcileLibraryAsync,
// ReconcileMovieLibraryAsync, ReconcileSeriesLibraryAsync, ReconcileShowEpisodesAsync, WriteEpisodeGroupAsync.
// ISyncOrchestrator is unchanged.
```

## Test files

- `tests/PlexToJellyfinSync.Tests/MediaItemWriterTests.cs` — AC1–AC5
- `tests/PlexToJellyfinSync.Tests/SeriesAggregateWriterTests.cs` — AC6–AC13 (real `MediaItemWriter` and
  `WatchAggregator` over `FakePlexClient`, `StubPathMapper`, `RecordingNfoWriter`)
- `tests/PlexToJellyfinSync.Tests/LibraryReconcilerTests.cs` — AC14–AC20 (real `MediaItemWriter` and
  `SeriesAggregateWriter` over the same fakes)
- `tests/PlexToJellyfinSync.Tests/SyncOrchestratorTests.cs` — AC21 (factory wiring only)
- `tests/PlexToJellyfinSync.Tests/ServiceCollectionExtensionsTests.cs` — AC22

No mocking library; existing fakes are reused and may only be extended additively.

## Documentation updates

- `docs/ARCHITECTURE.md`: the high-level flowchart shows `SyncOrchestrator → LibraryReconciler`,
  `→ SeriesAggregateWriter`, `→ MediaItemWriter → PathMapper / NfoWriter`, `SeriesAggregateWriter →
  WatchAggregator`; Sync pipeline point 2 names which type owns each part (history and run lifecycle in
  `SyncOrchestrator`, per-kind reconcile and episode parallelism in `LibraryReconciler`, aggregates in
  `SeriesAggregateWriter`, the path-mapping skip in `MediaItemWriter.WriteItemAsync`) and links
  [0018](../../docs/decisions/0018-sync-orchestrator-split-into-collaborators.md); point 6 says the per-episode
  `Parallel.ForEachAsync` is `LibraryReconciler`'s and the per-library one `SyncOrchestrator`'s.
- `docs/UNIT_TESTS.md`: the unit-test list (point 1) names `MediaItemWriter`, `SeriesAggregateWriter` and
  `LibraryReconciler`; point 2 says `SyncOrchestratorTests` drives `SyncOrchestrator` with its real
  collaborators over the fakes.
- `README.md`: none (no configuration or behavior change).

## Architecture check

- **Unmapped paths always skipped (0004):** the check moves unchanged into `MediaItemWriter.WriteItemAsync` and
  the episode filter of `SeriesAggregateWriter`; AC2, AC10 and the existing orchestrator tests guard it.
  `NfoWriter`'s own root check is untouched.
- **NFO only touched in watch fields (0003):** `NfoWriter` is not changed.
- **Item-level isolation and run-level error handling (0006):** per-item `try/catch` stays around the same
  calls; failures outside an item still reach `SyncOrchestrator.HandleError`, which still clears the owner
  cache (AC17, AC20, AC21).
- **Incremental plus reconcile (0005), parallelism bounds and clamps (0016):** unchanged in behavior; the
  episode clamp moves to `LibraryReconciler` (recorded in 0018).
- **Worker guards (0013):** `ISyncOrchestrator` is unchanged.
- Log message texts are unchanged; the log category of moved messages becomes the new type's name (visible in
  the dashboard's Category column). Treated as diagnostic metadata, not a product behavior change.

## Security considerations

- No new input, dependency, endpoint or file-write target; the path-mapping skip and the season/series
  directory derivation move verbatim and are covered by tests at their new home and end to end.
- The moved warnings log the same Plex-provided values as today (`FilePath`, `Title`, `RatingKey`) through
  the same `InMemoryLogger` redaction; no new external data is logged.
- Stateless singletons: no new shared mutable state under the concurrent library and episode reconcile.

## Decision records

- `docs/decisions/0018-sync-orchestrator-split-into-collaborators.md` (Proposed)

## Out of scope / follow-ups

- Issue #58 (season/series directory layout assumption), issue #15 (`ToList` before `Aggregate`) — unchanged.
- Proposed follow-up issue: **"[Performance] Series reconcile requests each show's episodes twice"** — body:
  "`LibraryReconciler.ReconcileShowEpisodesAsync` collects a show's episodes from `/library/metadata/{key}/allLeaves`,
  and `SeriesAggregateWriter.WriteAggregatesAsync` requests the same endpoint again for the aggregates, so every
  show costs two full episode requests per reconcile. Pass the already collected episodes to the aggregate writer
  during reconcile (the history path still needs the fetch). Found while planning #56."
