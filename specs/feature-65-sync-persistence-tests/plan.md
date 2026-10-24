# Plan: Tests for the remaining sync and persistence paths

Source: Issue #65 | [spec.md](spec.md)
Status: Draft
Tier: trivial — the change adds tests and test doubles only and changes no production code, behavior or control flow, so there is nothing a test written first could fail on.
Security areas touched: none

The tests exercise `StateStore`, which `.squad/project.md` lists under *State on disk*, but no production file
of that or any other security area changes, and no control is created, changed or removed. The diff touches
only `tests/`, `.squad/project.md` (*Test doubles*) and `docs/areas/sync-pipeline.md`.

## Problem / root cause

See [spec.md](spec.md), *Problem*: most of the tests issue #65 asks for exist on `main`; the spec covers the
remaining unpinned paths of `StateStore` and the cancellation rethrows of `SyncOrchestrator`.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | Tests for watermark advancement, the history → item → aggregate pipeline, per-item failure isolation (F-406), the `StateStore` round trip, corrupt-file recovery, atomic write and `SyncStatusService` concurrency exist on `main` | `SyncOrchestratorTests.cs` lines 144, 302, 564–672; `StateStoreTests.cs` lines 82–336; `SyncStatusServiceTests.cs` lines 118–156 | confirmed |
| 2 | The issue's claim "four of the five classes have no tests" | refuted by fact 1; `SyncStateFile` has no test of its own but is serialized by every `StateStoreTests` round trip | refuted |
| 3 | The only lines of the named classes and the split-off collaborators that no test covers: `StateStore.cs` 115/121 (unpreserved branch), 126–130 (other read error), 147–149 (move failure), the Windows branch of line 70; `SyncOrchestrator.cs` 203 and 232 (cancellation rethrow) | Build, then *Test* with `--collect:"XPlat Code Coverage"` on `b81591e`, 582 tests passed; per-line report of the Cobertura file for the eight files | confirmed |
| 4 | The orchestrator's only cancellation test cancels before the run starts, so the rethrow lines are never reached | `SyncOrchestratorTests.cs` lines 679–700 (`CancelAsync` before `ProcessHistoryAsync`); fact 3 | confirmed |
| 5 | Without the rethrow at line 201–203 or 230–232 the generic catch counts an error and the loop's `ThrowIfCancellationRequested` (lines 164, 224) still throws, so only `Errors` and `LastError` tell the builds apart | `SyncOrchestrator.cs` lines 162–211, 220–239, 108–116 | confirmed |
| 6 | A `FileStream` opened with `FileShare.None` makes a second `File.OpenRead` of the file in the same process fail with `IOException`; `File.Move(..., overwrite: true)` onto a non-empty directory fails with `IOException`; both on Linux as root, and neither depends on file permissions | Scratch `dotnet run` program on .NET 10 in the session scratchpad: `read failed: IOException … being used by another process`, `move failed: IOException`, user `root` | confirmed |
| 7 | `IOException` from `File.OpenRead` reaches the generic catch (warning, no move); `JsonException` the corrupt branch; a failed `File.Move` of the corrupt file returns `false` and logs the "could not be preserved" error | `StateStore.cs` lines 104–131, 139–151 | confirmed |
| 8 | System.Text.Json reads `"2026-02-03T04:05:06.1234567+00:00"` to the tick, ignores an unknown property, throws `JsonException` for `"yesterday"` and for `[]`, and writes a `DateTimeOffset?` property as `"HighWaterMark": "2026-02-03T04:05:06.1234567+00:00"` with `WriteIndented` | Scratch `dotnet run` program with a class shaped like `SyncStateFile` and the store's options: `ok …1234567+00:00`, `ok`, `ex JsonException`, `ex JsonException`, the indented output | confirmed |
| 9 | `SetHighWaterMarkAsync` reads the file before writing (so E2 logs the E1 error again) | `StateStore.cs` line 188 | confirmed |
| 10 | Affected shows are kept in a `HashSet<string>`; within one show the season aggregates are written before the series aggregate | `SyncOrchestrator.cs` line 160; `SeriesAggregateWriter.cs` lines 93 and 113 | confirmed |
| 11 | `RecordingNfoWriter` and `FakePlexClient` have no hook to cancel a token during a call; `RecordingNfoWriter` records a write only after its failure check | `RecordingNfoWriter.cs` lines 104–180; `FakePlexClient.cs` lines 125–201 | confirmed |
| 12 | The orchestrator tests run with aggregates enabled by default and with the real `MediaItemWriter` and `SeriesAggregateWriter` around `RecordingNfoWriter` | `SyncOptions.cs` line 41; `SyncOrchestratorTests.cs` lines 1814–1834 | confirmed |
| 13 | The log texts the tests compare are those of `StateStore.cs` lines 117, 121 and 128, formatted with the full `state.json` path | `StateStore.cs` lines 39, 113–128 | confirmed |

## Acceptance criteria

The criteria are those of [spec.md](spec.md), AC1–AC10.

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | `StateStoreReadsWholeSecondMarkInDocumentedFormat`, `StateStoreReadsFractionalMarkInDocumentedFormat`, `StateStoreWithUnknownValueReadsMark` | `tests/PlexToJellyfinSync.Tests/StateStoreTests.cs` |
| AC2 | `StateStoreWritesMarkInDocumentedFormat` | `tests/PlexToJellyfinSync.Tests/StateStoreTests.cs` |
| AC3 | `StateStoreWithCorruptFileLogsErrorWithBothPaths` | `tests/PlexToJellyfinSync.Tests/StateStoreTests.cs` |
| AC4 | `StateStoreWithNonTimeMarkPreservesItAsCorrupt` | `tests/PlexToJellyfinSync.Tests/StateStoreTests.cs` |
| AC5 | `StateStoreWithUnmovableCorruptFileKeepsItAndLogsError`, `StateStoreWithUnmovableCorruptFileIsOverwrittenByNextWrite` | `tests/PlexToJellyfinSync.Tests/StateStoreTests.cs` |
| AC6 | `StateStoreWithUnreadableFileLogsWarningAndKeepsIt` | `tests/PlexToJellyfinSync.Tests/StateStoreTests.cs` |
| AC7 | `SyncOrchestratorProcessHistoryCancellationDuringItemWritePropagatesWithoutError` | `tests/PlexToJellyfinSync.Tests/SyncOrchestratorTests.cs` |
| AC8 | `SyncOrchestratorProcessHistoryCancellationDuringAggregateWritePropagatesWithoutError` | `tests/PlexToJellyfinSync.Tests/SyncOrchestratorTests.cs` |
| AC9 | none: *Test* and *Coverage gate* in step 7; the existing `SyncOrchestratorProcessHistoryItemTimeoutIsTreatedAsItemFailure` and `SyncOrchestratorProcessHistoryAggregateTimeoutContinuesAndAdvancesWatermark` stay unchanged | — |
| AC10 | `StateStoreWithCorruptFileReplacesEarlierCorruptCopy` | `tests/PlexToJellyfinSync.Tests/StateStoreTests.cs` |

Every test passes on today's code (they pin behavior). A test that fails is reported to the Lead as a finding
about the production code; its expectation is not bent to the code.

## Approach

No user-visible output is added. The Tester writes all tests in step 6 (tier `trivial` skips steps 4 and 5).

**`StateStoreTests`** (each test in the existing per-test temporary directory):

- A private overload `CreateStore(ILogger<StateStore> logger)`; the existing parameterless `CreateStore()`
  stays and passes `NullLogger`.
- AC1: write the file with `File.WriteAllTextAsync` from a literal, never through the store:
  `{ "HighWaterMark": "2026-02-03T04:05:06+00:00" }`, `{ "HighWaterMark": "2026-02-03T04:05:06.1234567+00:00" }`
  (expected `new DateTimeOffset(2026, 2, 3, 4, 5, 6, TimeSpan.Zero).AddTicks(1234567)`, compared with
  `Assert.AreEqual` on the value and on `UtcTicks`), and
  `{ "HighWaterMark": "2026-02-03T04:05:06+00:00", "SchemaVersion": 2 }`.
- AC2: write that fractional mark through the store, parse the file with `JsonDocument`: the root is an object
  with exactly one property, `HighWaterMark`, of kind string, whose
  `DateTimeOffset.Parse(…, CultureInfo.InvariantCulture)` has the mark's `UtcTicks`.
- AC3: the existing corrupt literal; the recorded entries hold exactly one, level `Error`, message
  `State file {dir}/state.json is corrupt, preserved as {dir}/state.json.corrupt; starting fresh with no high-water mark`
  (paths built with `Path.Combine` from the temporary directory), exception of type `JsonException`.
- AC4: `{ "HighWaterMark": "yesterday" }`: no mark, `state.json` gone, `state.json.corrupt` holds that text.
- AC5: obstacle = a directory `state.json.corrupt` holding one file. Read test: no mark; `state.json` exists
  with its original content; exactly one entry, level `Error`, message
  `State file {dir}/state.json is corrupt and could not be preserved; starting fresh with no high-water mark`.
  Write test: same setup, read, then `SetHighWaterMarkAsync(mark)` and read again: the mark is returned, and
  the entries are exactly two, both that error. Both tests excluded on Windows with
  `[OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]` (fact 6 was measured on Linux only).
- AC6: write a valid file, hold it open with
  `new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)` around the read: no mark; one
  entry, level `Warning`, message `Could not read state file {dir}/state.json, starting fresh`, exception an
  `IOException`; `state.json` exists and `state.json.corrupt` does not. Release the handle before the cleanup.
  Excluded on Windows as AC5.
- AC10: `state.json.corrupt` pre-filled with `earlier`, `state.json` with the corrupt literal: no mark,
  `state.json.corrupt` holds the corrupt literal, one entry with the AC3 error text.

**`SyncOrchestratorTests`** (with a `CancellationTokenSource` the test owns and the extended
`RecordingNfoWriter`):

- AC7: movies `m1` (history `_since + 10 min`) and `m2` (`_since + 30 min`); the writer's `CancelWhen` matches
  `RatingKey == "m1"`. Assert: `ProcessHistoryAsync(source.Token)` throws `OperationCanceledException`
  (`Assert.ThrowsAsync`); `Errors` is `0`; `LastError` is `null`; `MediaItemRequests` is exactly `["m1"]`;
  `Writes` is empty; `_stateStore.HighWaterMark` is `_since` and `_stateStore.Writes` is empty; `IsRunning`
  is `false`.
- AC8: two shows, each with one episode with its own rating key, show key and season directory (the existing
  `AddSeriesWithTwoEpisodes` fixes the keys `e1`/`e2`, so the test registers its own items), one history entry
  per episode; `CancelWhen` matches `Kind is MediaKind.Season or MediaKind.Series`. Assert: the run throws
  `OperationCanceledException`; `Errors` is `0`; `LastError` is `null`; `WritesOf(Season)` and
  `WritesOf(Series)` are empty; `EpisodeRequests` holds exactly one key, one of the two show keys; the mark
  is unchanged; `IsRunning` is `false`. No assertion names the show that came first.

**Test doubles:**

- `RecordingStateStoreLogger` records level, formatted message (`formatter(state, exception)`) and exception
  of every entry, in the pattern of `RecordingSyncStatusLogger`.
- `RecordingNfoWriter` gets `CancellationSource` and `CancelWhen`. In `WriteAsync`, after the concurrency
  gate and before the `FailuresByRatingKey` check: when both are set and `CancelWhen(item)` is true, it
  cancels the source (`CancelAsync`) and throws `new OperationCanceledException(<message>, cancellationToken)`;
  the write is not recorded. The call ends by throwing, never by returning (Challenge note 5 of the spec).

## Options considered

- Cancellation hook on `RecordingNfoWriter` (chosen) versus on `FakePlexClient.GetMediaItemAsync`: the writer
  sees both the item write (AC7) and the aggregate writes (AC8), so one hook serves both; a Plex-client hook
  would need a second one for the aggregate path.
- Unreadable file through `FileShare.None` (chosen) versus `chmod 000`: the sandbox runs as root, which
  ignores file permissions, so a permission test would pass in CI and fail here.
- Corrupt file kept in place through a non-empty directory at `state.json.corrupt` (chosen) versus a
  read-only directory: the same root problem.
- Tier `standard` was weighed: it would run steps 4 and 5, but with no production change every plan-named test
  would pass on today's code and be deferred to step 6, so it adds launches without adding a check.

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| PlexToJellyfinSync.Tests | `StateStoreTests.cs` | 10 tests, `CreateStore` overload |
| PlexToJellyfinSync.Tests | `RecordingStateStoreLogger.cs` (new) | logger double for `StateStore` |
| PlexToJellyfinSync.Tests | `SyncOrchestratorTests.cs` | 2 tests |
| PlexToJellyfinSync.Tests | `RecordingNfoWriter.cs` | cancellation hook |

## Signatures (for the Dev's skeleton)

No production API. Test-code members, written by the Tester with their tests (no skeleton step in tier
trivial): `RecordingStateStoreLogger`, `RecordingStateStoreLogger.Entries`,
`RecordingNfoWriter.CancellationSource`, `RecordingNfoWriter.CancelWhen`, `StateStoreTests.CreateStore`.

```csharp
internal sealed class RecordingStateStoreLogger : ILogger<StateStore>
public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

// RecordingNfoWriter
public CancellationTokenSource? CancellationSource { get; set; }
public Predicate<MediaItem>? CancelWhen { get; set; }

// StateStoreTests
private StateStore CreateStore(ILogger<StateStore> logger)
```

## Test files

`tests/PlexToJellyfinSync.Tests/StateStoreTests.cs`, `tests/PlexToJellyfinSync.Tests/SyncOrchestratorTests.cs`,
and the doubles `tests/PlexToJellyfinSync.Tests/RecordingStateStoreLogger.cs` and
`tests/PlexToJellyfinSync.Tests/RecordingNfoWriter.cs`.

Existing test code that calls a changed signature: none (the `CreateStore()` overload and the new
`RecordingNfoWriter` properties leave every existing call site as it is).

Files of the change no unit test can cover: none.

## Areas

`docs/areas/sync-pipeline.md`: no change in behavior. Its *State file* section names the three log texts the
tests now compare.

## Documentation updates

- `docs/areas/sync-pipeline.md`, *State file* — owner Lead: the bullet on an unparseable file gets the error
  texts `State file <path> is corrupt, preserved as <path>.corrupt; starting fresh with no high-water mark` and
  `State file <path> is corrupt and could not be preserved; starting fresh with no high-water mark`; the bullet
  on any other read error gets the warning `Could not read state file <path>, starting fresh`.
- `.squad/project.md`, *Test doubles* — owner Tester: the line `RecordingWorkerLogger, RecordingSyncStatusLogger`
  adds `RecordingStateStoreLogger` (and `StateStore` to the classes it names); the `RecordingNfoWriter` line
  adds "can cancel a token on a chosen write".
- Sweep: `docs/ARCHITECTURE.md` and the area documents hold no list of test doubles; `docs/UNIT_TESTS.md`
  (*Test doubles*) lists no logger doubles and its `RecordingNfoWriter` line stays true, so it is unchanged.

## Architecture check

No guarantee of `docs/ARCHITECTURE.md` or `.squad/project.md` is touched; the tests pin the state-file and
cancellation guarantees `docs/areas/sync-pipeline.md` already states.

## Security considerations

- [x] Every byte or length limit on parsed input says whether it applies before or after decoding — no limits.
- [x] Every exception that can reach a user-visible failure reason or message is listed — none: no
  production code changes.

## Decision records

- none: no decision beyond the obvious one; the choice of test hooks is recorded under *Options considered*.

## Out of scope / follow-ups

As in [spec.md](spec.md), *Out of scope*; no follow-up issue.
