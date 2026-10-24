# Spec: Tests for the remaining sync and persistence paths

Source: Issue #65
Status: Draft

## Problem

Issue #65 (review finding F-416) reports that `SyncOrchestrator`, `StateStore`, `SyncStateFile` and
`SyncStatusService` have no tests, so the high-water mark handling, the history-to-write pipeline, the
per-item error isolation (F-406), the atomic and corrupt-file handling of the state file (F-411) and the
concurrency of the run status are unverified.

That is no longer true on `main` (checked at `b81591e`). Today the repository has `SyncOrchestratorTests`
(about 60 tests: seeding and advancing the mark, the pipeline from history to item write to aggregates,
per-item and per-aggregate failures and timeouts that keep the cycle running and still advance the mark,
library and kind filters, cancellation before the run starts), `StateStoreTests` (round trip, missing
directory, corrupt file returns no mark and is moved to `state.json.corrupt`, repair on the next write,
`null` content, write and move failures that leave `state.json` unchanged and delete the temporary file,
the file mode kept, concurrent access) and `SyncStatusServiceTests` (detached snapshots, lost-update and
consistent-snapshot concurrency, a throwing `Changed` subscriber isolated). The collaborators split off the
orchestrator (`LibraryReconciler`, `SeriesAggregateWriter`, `MediaItemWriter`) have their own test classes.
A coverage run of the current suite (582 tests, all passing) leaves every line of `SyncOrchestrator`'s
collaborators, `SyncStatusService` and `SyncStateFile` covered.

What remains unverified are the behaviors the area document
[`docs/areas/sync-pipeline.md`](../../docs/areas/sync-pipeline.md) promises in the less common paths,
where no test fails if the code changes:

- `StateStore` lines 115–122 and 139–150: a corrupt `state.json` that cannot be moved aside;
- `StateStore` lines 126–131: a state file that exists but cannot be read for a reason other than its
  content;
- the on-disk format of `state.json` (`SyncStateFile`): every test writes the file with the same code that
  reads it, so renaming or reformatting the `HighWaterMark` value would pass the suite while every existing
  installation lost its mark on the update — exactly the silent reset F-411 is about;
- the operator-visible log of a corrupt state file: the tests use a null logger, so the error that tells the
  operator the mark was lost, and where the old file went, is not checked;
- `SyncOrchestrator` lines 201–203 and 230–232: a stop of the application that arrives while a history
  run is processing an item or writing a show's aggregates; the only cancellation test cancels before the
  run starts, so nothing distinguishes "cancelled" from "item failed" once the run is under way.

The operator relies on these paths after a crash, a disk problem or a container stop: they decide whether
the mark survives, whether plays are re-read or lost, and whether the error counter is honest.

## Scenarios

The "user" of each scenario is the operator, who reads `state.json`, the log and the dashboard status.
The scenarios describe behavior that holds today and that this feature pins with tests; none changes it.

- S1: Given a `state.json` written in the documented format, a JSON object with the single value
  `HighWaterMark` holding an ISO 8601 time with offset (for example
  `{ "HighWaterMark": "2026-02-03T04:05:06+00:00" }`), as an earlier release wrote it, when the
  application reads the mark, then it reports exactly that time.
- S2: Given the application has written a mark, when the operator opens `state.json`, then the file is a
  JSON object whose value `HighWaterMark` holds that time in ISO 8601 with offset, readable by S1.
- S3: Given a `state.json` that cannot be parsed, when the application reads the mark, then it continues
  with "no mark", the file is moved to `state.json.corrupt`, and the log shows the error
  `State file <path>/state.json is corrupt, preserved as <path>/state.json.corrupt; starting fresh with no high-water mark`.
- S4: Given a history run under way, when the application is stopped while an item of a history entry is
  being read or written, then the run ends by cancellation: the stop reaches the caller, no error is
  counted, the last error stays as it was, the entries after that item are not processed, the mark is not
  advanced, and the status no longer shows a run in progress.
- S5: Given a history run that has processed its entries and is writing the season and series aggregates,
  when the application is stopped while a show's aggregates are being written, then the run ends by
  cancellation as in S4: no error counted, no further show's aggregates written, the mark not advanced, no
  run in progress.

## Errors and edge cases

- E1: A `state.json` that cannot be parsed and cannot be moved to `state.json.corrupt` (for example
  because a directory of that name is in the way): the application continues with "no mark", `state.json`
  stays where it is with its content, and the log shows the error
  `State file <path>/state.json is corrupt and could not be preserved; starting fresh with no high-water mark`.
- E2: After E1, the next written mark replaces the unparseable `state.json`, and the mark is read back.
- E3: A `state.json` that exists but cannot be opened for reading (for example while another handle holds
  it exclusively): the application continues with "no mark", the log shows the warning
  `Could not read state file <path>/state.json, starting fresh`, the file is not moved and no
  `state.json.corrupt` appears.
- E4: A `state.json` whose JSON is well formed but whose `HighWaterMark` is not a time (for example
  `"yesterday"`): treated like S3 — "no mark", moved to `state.json.corrupt`, error logged.
- E5: A `state.json` in the documented format that carries an additional, unknown value besides
  `HighWaterMark` (as a later release might write): the mark is still read as in S1.
- E6: A per-item request that times out on its own, while the application is not being stopped, still
  counts as a failed item and the run continues (already pinned by
  `SyncOrchestratorProcessHistoryItemTimeoutIsTreatedAsItemFailure` and its aggregate counterpart; named
  here so S4 and S5 are not read as changing it).

## Non-functional requirements

- No change to production code, configuration, the `state.json` format or any log text: the feature adds
  tests (and the test doubles they need) only. A test that fails against today's code is a finding about
  the code, reported to the Lead, not a reason to change the expectation.
- Tests follow `docs/UNIT_TESTS.md` and `.squad/project.md` (*Test doubles*): MSTest, hand-written doubles,
  no mocking library, no real clock, no wall-clock delays to provoke a race; the file-system tests use a
  per-test temporary directory as `StateStoreTests` does today and remove it afterwards.
- The tests run on the CI runner (Linux, non-root) and in the sandbox (Linux, root): a failure provoked
  through file permissions alone does not work as root and is not used. A test that depends on Unix
  behavior is excluded on Windows the way the existing `StateStoreTests` file-mode tests are.
- The *Coverage gate* still passes; the uncovered lines of `StateStore.cs` and `SyncOrchestrator.cs` named
  under *Problem* are covered afterwards, except the Windows-only branch of the file-mode copy.
- Logs: unchanged.

## Acceptance criteria

- [ ] AC1: A test reads a `state.json` written byte for byte in the documented format (not produced by the
  store under test) and gets exactly its `HighWaterMark` time; a variant with an additional unknown value
  reads the same time (S1, E5).
- [ ] AC2: A test writes a mark through the store and checks the file on disk: a JSON object whose value
  `HighWaterMark` parses as that time with its offset (S2).
- [ ] AC3: A test with an unparseable `state.json` checks the error log entry with both paths and the text
  of S3, besides the existing checks of "no mark" and the moved file (S3).
- [ ] AC4: A test with a well-formed `state.json` whose `HighWaterMark` is not a time gets "no mark" and
  finds the file moved to `state.json.corrupt` (E4).
- [ ] AC5: A test with an unparseable `state.json` and an obstacle at `state.json.corrupt` gets "no mark",
  finds `state.json` unchanged in place and the error log entry of E1; a following write and read return the
  new mark (E1, E2).
- [ ] AC6: A test with a `state.json` that cannot be opened for reading gets "no mark", finds the warning of
  E3 in the log, `state.json` still in place and no `state.json.corrupt` (E3).
- [ ] AC7: A test cancels the run's token while the item of a history entry is being processed (the fake
  observes the call, cancels, and the call then ends with the cancellation): the run ends with an
  `OperationCanceledException`, the error count stays `0`, a later entry's item is not requested or
  written, the stored mark is unchanged and `isRunning` is `false` (S4).
- [ ] AC8: A test cancels the run's token while a show's aggregates are being written, with a second
  affected show waiting: the run ends with an `OperationCanceledException`, the error count stays `0`, the
  second show's aggregates are not written, the stored mark is unchanged and `isRunning` is `false` (S5).
- [ ] AC9: The full suite passes on the unchanged production code and the *Coverage gate* passes; the
  existing timeout tests of E6 are kept unchanged (E6).

## Out of scope

- Any change to the behavior of the sync pipeline, the state file or the run status, including the
  findings still open from the same review: no schema version for `state.json` (#61, F-412), `GetSnapshot`
  copying properties by hand (#63, F-414), `Changed` raised on every mutation (#64, F-415).
- That a write after E5 drops the unknown value: the store keeps only `HighWaterMark` today; whether a later
  release's values must survive is the schema question of #61.
- Further tests of `SyncStatusService`, `SyncStateFile` beyond its format, `LibraryReconciler`,
  `SeriesAggregateWriter`, `MediaItemWriter` and the reconcile's cancellation: their lines are covered and
  their documented behavior has tests today.
- The Windows-only branch of the file-mode copy in `StateStore` (the CI and the container run on Linux).
- Restructuring or deduplicating the existing test classes.

## Open questions

- Do the tests the issue asks for already exist? Answered: mostly yes, see *Problem*; this spec covers only
  the gaps a reading of the tests and a coverage run of `main` at `b81591e` found.
- Should the issue's `SyncOrchestrator` be read as the orchestrator alone, after the split into
  collaborators (decision 0018)? Answered: the issue's behaviors are spread over `SyncOrchestrator`,
  `LibraryReconciler`, `SeriesAggregateWriter` and `MediaItemWriter`; all four have test classes, and the
  only uncovered orchestration paths are the cancellation rethrows of S4 and S5.
- Can E1 and E3 be provoked in a test that runs as root (sandbox) and as a non-root user (CI)? Answered:
  yes, without file permissions. A scratch experiment on .NET 10 under Linux as root showed that a
  `FileStream` opened with `FileShare.None` makes a second `File.OpenRead` of the same file fail with an
  `IOException` ("being used by another process"), and that `File.Move(..., overwrite: true)` onto a
  non-empty directory fails with an `IOException`. Which of them the tests use is the plan's choice.
- Is the behavior of E1, E3 and S4 intended, so the tests pin it rather than a defect? Answered: yes;
  `docs/areas/sync-pipeline.md`, *State file* ("an error is logged either way", "any other read error is
  logged as a warning and treated as 'no mark'; the file is not moved", "a file that could not be moved is
  overwritten when the next mark is written") and *Limits and errors* ("stopping the application cancels
  the run in progress without counting an error; a history run cancelled this way does not advance the
  mark"), and decision 0006 (item-level error isolation).
- Is a renamed `HighWaterMark` value allowed to break the read of an existing file? Answered: no; the area
  document fixes the file as "a JSON document with one value, `HighWaterMark`", and the configuration area
  keeps `state.json` on a volume across updates.

## Area documents

- [`docs/areas/sync-pipeline.md`](../../docs/areas/sync-pipeline.md) already states every behavior these
  tests pin (*State file*, *Limits and errors*); no change in behavior, so no change to its rules. Whether
  the exact log texts of S3, E1 and E3 are added to its *State file* section is decided in the plan.
- `.squad/project.md`, *Test doubles*: gains an entry for each new test double the plan introduces (for
  example a logger that records the entries of `StateStore`), as its list requires.

## Challenge notes

Minor objections of the spec challenge (Devil's Advocate), appended verbatim; the Lead answers them while it plans.

1. **minor — a documented edge case is not pinned.** docs/areas/sync-pipeline.md:161 says the corrupt file moves to `state.json.corrupt` "(replacing an earlier one)". This is the repeated-corruption case. `File.Move(..., overwrite: true)` at StateStore.cs:143 does it, but no test creates an existing `state.json.corrupt` file first. The spec skips it although it says it pins the State file promises. Instruction: add an edge case and a criterion. With an earlier `state.json.corrupt` file present, a new unparseable `state.json` replaces it: the `.corrupt` file then holds the new content, the result is "no mark", and the S3 error is logged.
2. **minor — AC1's sample is not what real installs hold.** The first mark is seeded from `DateTimeOffset.UtcNow` (SyncOrchestrator.cs:269). An existing `state.json` therefore often holds a 7-digit fraction, e.g. `"2026-02-03T04:05:06.1234567+00:00"`. Only marks from Plex's `FromUnixTimeSeconds` are whole seconds (PlexClient.cs:291). A test with whole seconds only would pass on a reader that breaks fractional values. Instruction: AC1 also reads a byte-for-byte file whose `HighWaterMark` has a 7-digit fractional second and gets that exact time.
3. **minor — E2 logs the E1 error a second time.** `SetHighWaterMarkAsync` calls `ReadAsync` first (StateStore.cs:188). With the obstacle still in place, the parse fails again, the move fails again, and the E1 error is logged once more before the overwrite. Instruction: in AC5, assert the E1 entry after the read alone. After the write, do not expect exactly one E1 entry over the whole test; either expect the second one or check the log per step.
4. **minor — AC8 must not depend on show order.** Affected shows are a `HashSet<string>` (SyncOrchestrator.cs:160), and the order of a set is not guaranteed. Instruction: the fake cancels on whichever show is written first. The test then asserts that the other show's aggregates were not written. It does not hard-code which show comes first.
5. **minor — AC7 and AC8 must check what actually catches a missing rethrow.** If lines 201–203 or 230–232 are removed, the generic catch counts an error. The next iteration's `ThrowIfCancellationRequested` (lines 164 and 224) then still throws `OperationCanceledException`. So "throws", "mark unchanged" and "not running" all pass on that wrong build. Only the error count and `LastError` tell the two builds apart. Instructions:
   - AC7 and AC8 assert error count `0` and `LastError` unchanged; S4 promises the latter but AC7 does not check it yet.
   - In both, the cancelled call ends with an `OperationCanceledException` thrown by the fake, not a normal return. Otherwise the rethrow lines are never reached.
   - In AC7, the cancelled item is not the history's last entry. As the last entry, a build without the rethrow would advance the mark without throwing, because `FakeStateStore` ignores the token.
