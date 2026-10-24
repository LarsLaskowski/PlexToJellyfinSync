# Tasks: Tests for the remaining sync and persistence paths

Plan: [plan.md](plan.md) — Status: Draft

| # | Group | Task | Files | Criteria | Depends on | Owner | Done |
| - | ----- | ---- | ----- | -------- | ---------- | ----- | ---- |
| 1 | G1 | Skeleton: none (tier trivial skips step 4); write RecordingStateStoreLogger with Entries and the CreateStore overload together with the tests | `tests/PlexToJellyfinSync.Tests/RecordingStateStoreLogger.cs`, `tests/PlexToJellyfinSync.Tests/StateStoreTests.cs` | AC3, AC5, AC6, AC10 | — | Tester | [ ] |
| 2 | G1 | State file format tests | `tests/PlexToJellyfinSync.Tests/StateStoreTests.cs` | AC1, AC2 | — | Tester | [ ] |
| 3 | G1 | Corrupt, unmovable, unreadable and repeated-corruption tests | `tests/PlexToJellyfinSync.Tests/StateStoreTests.cs` | AC3, AC4, AC5, AC6, AC10 | 1 | Tester | [ ] |
| 4 | G1 | *Test doubles* entry for the logger double | `.squad/project.md` | AC3 | 1 | Tester | [ ] |
| 5 | G2 | Skeleton: none (tier trivial skips step 4); add CancellationSource and CancelWhen to RecordingNfoWriter together with the tests | `tests/PlexToJellyfinSync.Tests/RecordingNfoWriter.cs` | AC7, AC8 | — | Tester | [ ] |
| 6 | G2 | Cancellation during an item write and an aggregate write | `tests/PlexToJellyfinSync.Tests/SyncOrchestratorTests.cs` | AC7, AC8 | 5 | Tester | [ ] |
| 7 | G2 | *Test doubles* entry for the writer hook | `.squad/project.md` | AC7 | 5 | Tester | [ ] |
| 8 | G2 | Full suite and *Coverage gate* green, the timeout tests unchanged | — | AC9 | 3, 6 | Tester | [ ] |
| 9 | G3 | Log texts in the *State file* section | `docs/areas/sync-pipeline.md` | AC3, AC5, AC6 | 3 | Lead | [ ] |
