# Tasks: Series reconcile requests each show's episodes once

Plan: [plan.md](plan.md) — Status: Draft

Each task names its criteria, the tasks it waits for and its group (one launch per member in steps 4–6; rows
consecutive). A group has a "Skeleton" row for its API in *Signatures*, or says "no new API". Each
documentation edit is in one row.

| # | Group | Task | Files | Criteria | Depends on | Owner | Done |
| - | ----- | ---- | ----- | -------- | ---------- | ----- | ---- |
| 1 | G1 | Skeleton: `WriteAggregatesAsync(string showRatingKey, IReadOnlyList<MediaItem> episodes, CancellationToken cancellationToken)` on the interface with XML docs for both overloads, and on `SeriesAggregateWriter` throwing `NotImplementedException`; `ReconcileShowEpisodesAsync` returning `Task<IReadOnlyList<MediaItem>>`; the private `MapEpisodePath` and `WriteMappedAggregatesAsync` are not stubbed and come with task 4 | `src/PlexToJellyfinSync.Core/Abstractions/ISeriesAggregateWriter.cs`, `src/PlexToJellyfinSync.Service/SeriesAggregateWriter.cs`, `src/PlexToJellyfinSync.Service/LibraryReconciler.cs` | AC1–AC9 | — | Dev | [ ] |
| 2 | G1 | Tests first for the aggregate writer (the four new `SeriesAggregateWriterTests` of *Tests per criterion*) | `tests/PlexToJellyfinSync.Tests/SeriesAggregateWriterTests.cs` | AC2, AC3, AC5 | 1 | Tester | [ ] |
| 3 | G1 | Tests first for the reconciler (the eleven new `LibraryReconcilerTests` of *Tests per criterion*; the AC6 test is a regression guard that passes before the change) | `tests/PlexToJellyfinSync.Tests/LibraryReconcilerTests.cs` | AC1, AC2, AC4–AC9 | 1 | Tester | [ ] |
| 4 | G1 | Split the writer: `MapEpisodePath` (file-path check plus `MapToLocal`, the only aggregate path filter) and `WriteMappedAggregatesAsync` (today's aggregation and writes, unchanged); the rating-key overload filters with `MapEpisodePath` while enumerating `GetEpisodesAsync` (streaming, no full list), the list overload filters every passed episode with it; both call `WriteMappedAggregatesAsync` | `src/PlexToJellyfinSync.Service/SeriesAggregateWriter.cs` | AC2, AC3, AC5 | 2 | Dev | [ ] |
| 5 | G1 | Return the listed episodes from `ReconcileShowEpisodesAsync` and pass them to the new overload | `src/PlexToJellyfinSync.Service/LibraryReconciler.cs` | AC1, AC2, AC4, AC6–AC9 | 3, 4 | Dev | [ ] |
| 6 | G1 | Confirm the existing `SyncOrchestratorProcessHistoryAggregatesEachSeriesOnlyOnce` and the existing writer and reconciler tests pass unchanged | `tests/PlexToJellyfinSync.Tests/SyncOrchestratorTests.cs` | AC3 | 5 | Tester | [ ] |
| 7 | G2 | Update `docs/ARCHITECTURE.md` point 2, the `Sync:WriteSeriesSeasonAggregates` bullet: one listing per show in the reconcile, reused for the aggregates; the history path lists each touched show once; link 0018 (no new API) | `docs/ARCHITECTURE.md` | AC1, AC3 | 5 | Lead | [ ] |