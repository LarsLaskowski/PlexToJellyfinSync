# Spec: Tests for the remaining sync and persistence paths

Source: Issue #65
Status: Approved by Lead

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
  `HighWaterMark` holding an ISO 8601 time with offset, as an earlier release wrote it — a whole-second
  time from Plex (`{ "HighWaterMark": "2026-02-03T04:05:06+00:00" }`) or a seeded time with a 7-digit
  fraction (`{ "HighWaterMark": "2026-02-03T04:05:06.1234567+00:00" }`) — when the application reads the
  mark, then it reports exactly that time, fraction included.
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
  With the obstacle still in place, that write first reads the file again and logs the E1 error a second
  time before it replaces the file.
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
- E7: A `state.json` that cannot be parsed while a `state.json.corrupt` from an earlier corruption already
  exists: the new file replaces the earlier one, so `state.json.corrupt` then holds the new content; the
  application continues with "no mark" and logs the error of S3.

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

- [ ] AC1: Tests read a `state.json` written byte for byte in the documented format (not produced by the
  store under test) and get exactly its `HighWaterMark` time, once for a whole-second time and once for a
  time with a 7-digit fractional second (compared to the tick); a variant with an additional unknown value
  reads the same time (S1, E5).
- [ ] AC2: A test writes a mark with a fractional second through the store and checks the file on disk: a
  JSON object with exactly one value, `HighWaterMark`, a string that parses as that time to the tick (S2).
- [ ] AC3: A test with an unparseable `state.json` checks the error log entry with both paths and the text
  of S3, besides the existing checks of "no mark" and the moved file (S3).
- [ ] AC4: A test with a well-formed `state.json` whose `HighWaterMark` is not a time gets "no mark" and
  finds the file moved to `state.json.corrupt` (E4).
- [ ] AC5: A test with an unparseable `state.json` and an obstacle at `state.json.corrupt` gets "no mark",
  and right after that read finds `state.json` unchanged in place and exactly one log entry, the error of
  E1; a following write and read then return the new mark, and the log holds the E1 error twice, the second
  from the write's own read (E1, E2).
- [ ] AC6: A test with a `state.json` that cannot be opened for reading gets "no mark", finds the warning of
  E3 in the log, `state.json` still in place and no `state.json.corrupt` (E3).
- [ ] AC7: A test cancels the run's token while the item of a history entry that is not the last one is
  being written (the fake observes the call, cancels the token, and the call then ends by throwing an
  `OperationCanceledException`, never by returning): the run ends with an `OperationCanceledException`, the
  error count stays `0` and the last error stays empty, the later entry's item is not requested or
  written, the stored mark is unchanged and `isRunning` is `false` (S4).
- [ ] AC8: A test with two affected shows cancels the run's token on the first aggregate write of whichever
  show comes first (the call ends by throwing an `OperationCanceledException`, never by returning), without
  depending on which show that is: the run ends with an `OperationCanceledException`, the error count stays
  `0` and the last error stays empty, no season or series aggregate is written and the other show's
  episodes are not listed, the stored mark is unchanged and `isRunning` is `false` (S5).
- [ ] AC9: The full suite passes on the unchanged production code and the *Coverage gate* passes; the
  existing timeout tests of E6 are kept unchanged (E6).
- [ ] AC10: A test with an unparseable `state.json` and an earlier `state.json.corrupt` holding other
  content gets "no mark", finds `state.json.corrupt` holding the new content and the error of S3 in the log
  (E7).

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
  tests pin (*State file*, *Limits and errors*). The behavior does not change, so its rules stay as they
  are; its *State file* section gains the exact log texts of S3, E1 and E3, which the tests now compare.
- `.squad/project.md`, *Test doubles*: gains an entry for the new `StateStore` logger double, and the
  `RecordingNfoWriter` entry mentions its new cancellation hook.

## Challenge

The spec challenge raised five minor objections and no major one. Each is answered here; the notes are
folded into this section.

1. Repeated corruption, the earlier `state.json.corrupt` replaced (`docs/areas/sync-pipeline.md`, *State
   file*). Accepted: edge case E7 and criterion AC10 added.
2. A seeded mark has a 7-digit fraction (`DateTimeOffset.UtcNow`), so a whole-second sample alone would miss
   a reader that breaks fractions. Accepted: S1 names both forms, AC1 reads both and compares to the tick,
   and AC2 writes a fractional mark. A scratch run of System.Text.Json on .NET 10 read
   `"2026-02-03T04:05:06.1234567+00:00"` back to the tick and wrote it in the same form.
3. The write of E2 reads the file again and logs the E1 error a second time (`StateStore.cs` line 188).
   Accepted: E2 states it, and AC5 checks the log after the read alone (exactly one entry, the E1 error)
   and after the write (the E1 error twice).
4. Affected shows are a hash set, so their order is not fixed (`SyncOrchestrator.cs` line 160). Accepted:
   AC8 cancels on the first aggregate write of whichever show comes first and asserts only
   order-independent facts: no aggregate written, one show's episodes listed.
5. Without the rethrow, the generic catch counts an error and the next loop pass still throws, so only the
   error count and the last error tell the builds apart. Accepted: AC7 and AC8 assert error count `0` and an
   empty last error. In both, the fake ends the cancelled call by throwing an `OperationCanceledException`.
   In AC7 the cancelled entry is not the last one.
