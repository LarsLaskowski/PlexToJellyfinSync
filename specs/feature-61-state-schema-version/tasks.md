# Tasks: Schema version in the state file

Plan: [plan.md](plan.md) — Status: Draft

Each task names its criteria, the tasks it waits for and its group (one launch per member in steps 4–6; rows
consecutive). A group has a "Skeleton" row for its API in *Signatures*, or says "no new API". Each
documentation edit is in one row.

| # | Group | Task | Files | Criteria | Depends on | Owner | Done |
| - | ----- | ---- | ----- | -------- | ---------- | ----- | ---- |
| 1 | G1 | Skeleton: `CurrentSchemaVersion` (public const int, `1`), `SchemaVersion` (int?, before `HighWaterMark`), `DeserializeFileAsync`, `PreserveCorruptFile` renamed to `PreserveFile`, internal `SchemaVersionProbe` (only `SchemaVersion`); bodies compile, no behavior change yet | `src/PlexToJellyfinSync.Service/State/SyncStateFile.cs`, `src/PlexToJellyfinSync.Service/State/StateStore.cs`, `src/PlexToJellyfinSync.Service/State/SchemaVersionProbe.cs` | AC1, AC4 | — | Dev | [ ] |
| 2 | G2 | Tests first, no new API: new tests of *Tests per criterion* for AC1–AC6 and AC9, with `CreateUnmovableUnsupportedFileAsync` and the E1/E2 message helpers | `tests/PlexToJellyfinSync.Tests/StateStoreTests.cs` | AC1, AC2, AC3, AC4, AC5, AC6, AC9 | 1 | Tester | [ ] |
| 3 | G2 | Adapt `StateStoreWithUnknownValueReadsMark` (unknown value not `SchemaVersion`) and rewrite `StateStoreWritesMarkInDocumentedFormat` with its summary to the AC1 layout | `tests/PlexToJellyfinSync.Tests/StateStoreTests.cs` | AC7 | 1 | Tester | [ ] |
| 4 | G3 | Implement, no new API: version set on write, `DeserializeFileAsync` closes the stream before any move, unsupported check with E1/E2 logging and the `.unsupported` move, until all `StateStoreTests` and the *Coverage gate* pass | `src/PlexToJellyfinSync.Service/State/StateStore.cs`, `src/PlexToJellyfinSync.Service/State/SyncStateFile.cs` | AC1, AC2, AC3, AC4, AC5, AC6, AC7, AC9 | 2, 3 | Dev | [ ] |
| 5 | G4 | `docs/areas/sync-pipeline.md`: *State file* (two values, version rule, unsupported case with E1/E2 texts, case-sensitive and last-wins names, "no mark" list) and *Related decisions* (0027), checked against the final code | `docs/areas/sync-pipeline.md` | AC8 | 4 | Lead | [ ] |
| 6 | G4 | `docs/ARCHITECTURE.md` component 7: "a missing, unreadable or unsupported file" | `docs/ARCHITECTURE.md` | AC8 | 4 | Lead | [x] |