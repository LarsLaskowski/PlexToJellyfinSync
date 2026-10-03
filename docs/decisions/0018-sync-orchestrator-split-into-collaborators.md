# 0018: SyncOrchestrator is split into an item writer, a series aggregate writer and a library reconciler

- **Status:** Proposed
- **Date:** 2026-10-03
- **Source:** Issue #56 (review finding F-407)
- **Supersedes:** —

## Context

`SyncOrchestrator` (`src/PlexToJellyfinSync.Service/SyncOrchestrator.cs`, 579 lines) owned owner-account
resolution and caching, the incremental history pipeline, the full reconcile of movie and series libraries
(including the bounded per-library and per-episode parallelism and the shared-file grouping), the season and
series aggregate computation and the derivation of their target directories, the path-mapping step before
every NFO write, every status mutation and both levels of error handling (0006). The review (F-407)
recommended extracting at least the aggregation and the reconcile strategy behind small interfaces so that
`SyncOrchestrator` becomes a thin coordinator and each piece can be unit-tested on its own.

The refactoring must not change behavior: unmapped paths stay skipped (0004), existing NFO files are still
only touched in their watch fields (0003, owned by `NfoWriter` and not moved), per-item failures stay isolated
and a run-level failure still clears the cached owner id (0006), and the incremental/reconcile split (0005)
stays as it is.

## Options considered

1. **Leave the class as it is** — no risk, but the aggregation and reconcile logic stay testable only
   through the two public entry points, and the class keeps growing with every reconcile feature (it grew
   from ~420 to 579 lines between the review and this change).
2. **Extract only the season/series aggregation** — smallest change, but the reconcile strategy (the
   largest block, with its parallelism and grouping rules) stays in the orchestrator, and the item write plus
   outcome counting would have to be duplicated in the new aggregate type.
3. **Extract three collaborators behind interfaces in `Core.Abstractions`:** an item writer (file path and
   path-mapping checks, the NFO write and the created/updated counters), a series aggregate writer (season and
   series watch state and target directories) and a library reconciler (per-kind reconcile with its item-level
   error isolation). The orchestrator keeps the run lifecycle, the owner cache, the history pipeline and the
   library-level fan-out.
4. **Additionally extract the history pipeline and the owner-id cache** — the thinnest orchestrator, but the
   owner cache is cleared by the run-level error handler, and the history loop is tied to the high-water mark;
   splitting those would spread one run's state across types for little testability gain.

## Decision

Option 3.

- `IMediaItemWriter` / `MediaItemWriter`: `WriteItemAsync` skips an item without a file path or without a
  matching path mapping (with the existing warnings), otherwise writes it through `INfoWriter` at the mapped
  local path; `WriteAggregateAsync` writes a season or series item to an already-local directory. Both count
  `NfoCreated` / `NfoUpdated`. This is now the single place where the 0004 skip happens for movies and episodes.
- `ISeriesAggregateWriter` / `SeriesAggregateWriter`: `WriteAggregatesAsync` re-fetches a show's episodes,
  keeps only mapped ones, aggregates per season and for the series through `WatchAggregator`, and writes
  `season.nfo` / `tvshow.nfo` through `IMediaItemWriter`, with the directory derivation unchanged.
- `ILibraryReconciler` / `LibraryReconciler`: `ReconcileLibraryAsync` dispatches by library kind, reconciles
  movies one by one and a series library show by show with the episode groups written concurrently (bounded
  by `Sync:EpisodeReconcileParallelism`, clamped to at least 1), calls the aggregate writer when
  `Sync:WriteSeriesSeasonAggregates` is set, and isolates item- and show-level failures (`HandleItemError`).
  A failure outside an item (for example a failing library page) propagates to the orchestrator.
- `SyncOrchestrator` keeps `ProcessHistoryAsync`, `ReconcileAsync`, the owner cache with `HandleError`, the
  history loop with its own `HandleItemError`, the `Plex:Libraries` filter and the library-level
  `Parallel.ForEachAsync` (clamp to at least 1), and no longer depends on `IPathMapper`, `INfoWriter` or
  `WatchAggregator`.

All three types are stateless singletons, so the concurrent library reconcile shares them safely.

## Consequences

- Each concern has its own test class; `SyncOrchestratorTests` stays unchanged apart from its factory and
  keeps verifying the end-to-end flow, so a behavior change in the split would surface there.
- Log entries written by the moved code carry the category of their new type (`MediaItemWriter`,
  `LibraryReconciler`) instead of `SyncOrchestrator`; message texts are unchanged.
- The `Sync:EpisodeReconcileParallelism` runtime clamp that 0016 places in `SyncOrchestrator` now lives in
  `LibraryReconciler`; its purpose (defense-in-depth behind startup validation) is unchanged.
- The private reconcile helpers leave `#region ISyncOrchestrator`, which resolves review finding F-410
  (issue #59) as a side effect.
- Not changed here: a series reconcile still requests a show's episodes twice (once for the episode writes,
  once for the aggregates), and the season/series directory derivation still assumes a show/season folder
  layout (issue #58). Both remain behavior that a later change can address in one place,
  `SeriesAggregateWriter`.
