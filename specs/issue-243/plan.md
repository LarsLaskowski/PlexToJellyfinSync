# Plan: A failed state file move leaves state.json.tmp behind

Source: Issue #243
Status: Draft
Tier: standard — the fix widens the cleanup in `StateStore.SetHighWaterMarkAsync` to the file-mode copy and the move, a change of control flow (so not `patch`) that creates, changes or removes no security control.
Security areas touched: State on disk (`StateStore`, the high-water-mark file)

## Problem / root cause

`StateStore.SetHighWaterMarkAsync` (`src/PlexToJellyfinSync.Service/State/StateStore.cs`) wraps only the
creation and serialization of `state.json.tmp` (lines 179–184) in the `try` whose `catch` (line 185) calls
`TryDeleteTempFile` and re-throws. `PreserveFileMode(tempPath, _filePath)` (line 192) and
`File.Move(tempPath, _filePath, overwrite: true)` (line 194) run after that block, so an exception from either
propagates with the fully written temp file still in the state directory. `docs/areas/sync-pipeline.md`
(*State file*, lines 155–157) says "A failed write deletes the temporary file and leaves `state.json`
unchanged".

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | The `catch` that deletes the temp file covers only opening and serializing it | `StateStore.cs` lines 179–190: `try` holds the `FileStream` and `SerializeAsync`, `catch` calls `TryDeleteTempFile` and `throw;` | confirmed |
| 2 | `PreserveFileMode` (line 192) and `File.Move` (line 194) run outside that `try`, so their failure leaves `state.json.tmp` | `StateStore.cs` lines 192, 194; no other `try` in `SetHighWaterMarkAsync` except the outer `try`/`finally` that releases `_gate` (lines 165, 196–199) | confirmed |
| 3 | The area document promises deletion of the temp file on a failed write | `docs/areas/sync-pipeline.md` lines 155–157 | confirmed |
| 4 | `TryDeleteTempFile` swallows only `IOException` and `UnauthorizedAccessException`, so the original exception is what the caller sees | `StateStore.cs` lines 50–60 | confirmed |
| 5 | A leftover `state.json.tmp` does not break the next write: it is opened with `FileMode.Create` | `StateStore.cs` line 181 | confirmed |
| 6 | With a directory at the path `state.json`, `ReadAsync` returns a fresh state (`File.Exists` is false for a directory), `PreserveFileMode` returns early for the same reason, and the temp file is written, so only `File.Move` fails | `StateStore.cs` lines 70, 85, 181–183; `File.Exists` returns false for a directory (.NET API documentation) | confirmed |
| 7 | `File.Move(file, existingDirectory, overwrite: true)` on Linux throws, and of which exact type (POSIX `rename` gives `EISDIR`, which .NET is expected to map to `IOException`) | to verify: the Tester runs the new move-failure test on the current code in step 5 and asserts the type it measures with `Assert.ThrowsExactlyAsync` | unverified |
| 8 | `NfoWriter.SaveAsync` has the same pattern, but there it is documented: a temp file left by a failure after its serialization "stays next to the target" | `src/PlexToJellyfinSync.Service/NfoWriter.cs` lines 200–217; `docs/areas/path-mapping-and-nfo.md` lines 153–156 | confirmed |
| 9 | Both claims of the issue (the line numbers 185, 192, 194 and the quoted sentence) match the base `origin/main` | `StateStore.cs`, `sync-pipeline.md` read at the work branch, which has no change under `src/` or `docs/` against `origin/main` | confirmed |

## Acceptance criteria

- [ ] AC1: When `File.Move` (or `PreserveFileMode`, which shares the same `catch`) throws in
  `SetHighWaterMarkAsync`, `state.json.tmp` no longer exists afterwards.
- [ ] AC2: That failure still reaches the caller as the original exception, unchanged in type, and the next
  write after the cause is removed succeeds.
- [ ] AC3: A failure while opening or serializing the temp file keeps its current behavior: the original
  exception propagates and the previously persisted value is kept (existing test).

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | `StateStoreSetHighWaterMarkAsyncMoveFailureDeletesTempFile` (new) | `tests/PlexToJellyfinSync.Tests/StateStoreTests.cs` |
| AC2 | `StateStoreSetHighWaterMarkAsyncMoveFailureRethrowsOriginalException` (new) | `tests/PlexToJellyfinSync.Tests/StateStoreTests.cs` |
| AC3 | `StateStoreSetHighWaterMarkAsyncWriteFailurePreservesExistingFile` (existing, unchanged; must stay green) | `tests/PlexToJellyfinSync.Tests/StateStoreTests.cs` |

Test setup for both new tests (fact 6): create `_tempDirectory`, then a directory at `GetStateFilePath()`, call
`SetHighWaterMarkAsync`. AC1 asserts the throw and then `File.Exists(GetStateFilePath() + ".tmp")` is false.
AC2 asserts the exact exception type measured in step 5 (fact 7), then deletes the directory at
`GetStateFilePath()`, writes again and reads the value back. Both carry
`[OSCondition(OperatingSystems.Linux | OperatingSystems.OSX)]` like their neighbors, since the failure rests on
POSIX `rename` semantics. AC1's test fails on the current code (the temp file remains); AC2's part after the
retry passes on the current code too, and that is acceptable as part of the same test. No test for the
`PreserveFileMode` failure: it cannot be triggered without a seam, and it is covered by sharing the `try` with
`File.Move` (the Reviewer checks the scope by reading).

## Approach

In `SetHighWaterMarkAsync`, move `PreserveFileMode(tempPath, _filePath)` and `File.Move(tempPath, _filePath,
overwrite: true)` into the existing `try` whose `catch` calls `TryDeleteTempFile(tempPath)` and re-throws. The
temp file's stream must be disposed before the move, so the Dev extracts the open-and-serialize part into a
private method (see *Signatures*) awaited as the first statement of the `try`; the `await using` declaration
then ends with that method. The `catch` stays a bare `catch` with `throw;` (cancellation included, as today).
The outer `try`/`finally` that releases `_gate` is unchanged. No new user-visible output; no log line is added.

## Options considered

- **Chosen: make the code match the area document** — delete the temp file on every failure after it was
  created. It keeps the state directory free of a stale copy of the state, keeps the documented behavior, and
  costs a few moved lines.
- **Rejected: change the area document to match the code**, as `docs/areas/path-mapping-and-nfo.md` does for
  `NfoWriter` (fact 8). A leftover is harmless (fact 5), but the documented promise is the better behavior for
  a file in the operator's `/config` volume, the issue reports the code as the defect, and there is no
  decision record behind the current code path to keep.
- A `finally` that always calls `TryDeleteTempFile` is the same behavior (after a successful move the temp
  file no longer exists), not a separate design; the `catch` is kept because it already exists and states the
  intent.

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| `PlexToJellyfinSync.Service` | `State/StateStore.cs` | `SetHighWaterMarkAsync`: widen the cleanup `try`; new private helper |
| `PlexToJellyfinSync.Tests` | `StateStoreTests.cs` | two new tests |

## Signatures (for the Dev's skeleton)

No public or internal API changes, so no skeleton (step 4 not applicable). New private member, in the
`Methods` region of `StateStore`:

```csharp
/// <summary>
/// Write the state to the temp file
/// </summary>
/// <param name="tempPath">Temp file path</param>
/// <param name="state">State to write</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>Task</returns>
private static async Task WriteTempFileAsync(string tempPath, SyncStateFile state, CancellationToken cancellationToken)
```

## Test files

- `tests/PlexToJellyfinSync.Tests/StateStoreTests.cs`

Existing test code that calls a changed signature: none.

Files of the change no unit test can cover: none.

## Areas

- `docs/areas/sync-pipeline.md` (*State file*): the behavior it states becomes true; one sentence is made
  precise (below).

## Documentation updates

Sweep: `docs/areas/*.md`, `docs/ARCHITECTURE.md`, `README.md`, `SECURITY.md` searched for `state.json`,
`.tmp`, "temp file", "temporary file". Hits: `sync-pipeline.md` lines 153–160 (this change),
`configuration.md` lines 45 and 84 and `README.md` line 45 (location of `state.json` only, unaffected),
`path-mapping-and-nfo.md` lines 126 and 153–156 and `ARCHITECTURE.md` line 148 (`NfoWriter`, unaffected).

- `docs/areas/sync-pipeline.md` line 156–157, owner **Dev**: replace "A failed write deletes the temporary file
  and leaves `state.json` unchanged." with "A failed write — creating or serializing the temporary file,
  copying the file mode or the move — deletes the temporary file when possible and leaves `state.json`
  unchanged." ("when possible": the deletion is best-effort, fact 4.)

## Architecture check

No guarantee of `.squad/project.md` or `docs/ARCHITECTURE.md` is touched. The atomic replace stays one
`rename`; the failure still propagates to `SyncOrchestrator`, whose handling of a failed mark write (#244) is
unchanged. Decision 0005 ("written atomically") stays true.

## Security considerations

- [ ] No limits.
- [ ] Exceptions reaching a user-visible message: none new. The exception from `File.Move` or
  `PreserveFileMode` propagates exactly as today; only the temp file is removed first.

## Decision records

- none: the fix makes the code match the documented behavior; the rejected documentation-only option is
  recorded in this plan.

## Out of scope / follow-ups

- `NfoWriter.SaveAsync` keeps a temp file left by a failed file-mode copy or rename, as its area document
  says (fact 8). Proposed follow-up issue for the orchestrator — title: "NfoWriter leaves `<target>.tmp` in the
  media folder when the replace fails"; body: "`NfoWriter.SaveAsync` (`src/PlexToJellyfinSync.Service/NfoWriter.cs`)
  deletes `<target>.tmp` only when serialization fails; a failure of `PreserveFileMode` or `File.Move` leaves it
  next to the `.nfo` file in the media library, as `docs/areas/path-mapping-and-nfo.md` (*Limits and errors*)
  documents. #243 changed `StateStore` to delete its temp file on every failure; decide whether `NfoWriter`
  should do the same and update the area document accordingly."

## Challenge notes

- minor: Fact 7 does not need to wait for the Tester. The Devil's Advocate ran it in a scratch program (.NET 10, Linux): `File.Move(file, existingDirectory, overwrite: true)` throws exactly `System.IO.IOException` ("Is a directory"), and on the current code `state.json.tmp` is still there afterwards. Mark fact 7 confirmed as `IOException`; the AC2 test asserts `Assert.ThrowsExactlyAsync<IOException>`, and the Tester still confirms the type when it runs the new test on the current code in step 5.

## Security notes

- Fact 7 measured by Security (.NET SDK 10.0.402, Linux): `File.Move(tmp, <empty or non-empty directory>, overwrite: true)` throws exactly `System.IO.IOException` (HResult 21, EISDIR); the Tester can assert `Assert.ThrowsExactlyAsync<IOException>`.
- The *Security considerations* checklist leaves both items unticked although each answer is given; tick them or phrase them as statements.
- The wording change in `docs/areas/sync-pipeline.md` ("deletes the temporary file when possible") accurately describes the best-effort deletion of fact 4; keep it as written.