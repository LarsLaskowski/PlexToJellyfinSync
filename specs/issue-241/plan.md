# Plan: A failed mark write reports Plex as disconnected

Source: Issue #241
Status: Draft
Tier: standard — a behavior change of the run status (`plexConnected` on the dashboard and `/health`) that creates, changes or removes no security control.
Security areas touched: none

## Problem / root cause

`SyncOrchestrator.HandleError` (`src/PlexToJellyfinSync.Service/SyncOrchestrator.cs`, line 107) sets
`s.PlexConnected = false` for every exception that reaches the catch of `ProcessHistoryAsync` (line 311–314) or
`ReconcileAsync` (line 350–353), without knowing where the run failed. In a history run two failures reach it
that are no Plex request:

- the mark write after Plex answered (`_stateStore.SetHighWaterMarkAsync(maxViewed, …)`, line 301): line 290
  has just set `PlexConnected = true`, and `HandleError` turns it back to `false` — the case of the issue;
- the seeding mark write of a run without a mark (line 277), which makes no request to Plex at all (since #242
  such a run no longer sets the flag to `true`, but a failing one still sets it to `false`). Related defect of
  the same root cause, found while planning; the issue's expected behavior ("only a failed Plex request clears
  the flag") covers it, so it is fixed here.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | `HandleError` clears `PlexConnected` for every failed run (issue) | `SyncOrchestrator.cs` lines 99–109; called from lines 313 and 352 | confirmed |
| 2 | A failed `SetHighWaterMarkAsync` after Plex answered reaches `HandleError` and reports Plex as disconnected (issue) | line 290 sets `true` after `GetHistorySinceAsync`; line 301 writes the mark; no catch between line 290 and line 311 except the per-item ones | confirmed |
| 3 | The real `StateStore.SetHighWaterMarkAsync` propagates a write failure (e.g. a non-writable state directory) | `State/StateStore.cs` lines 161–200: `Directory.CreateDirectory`, the `FileStream`, `SetUnixFileMode` and `File.Move` are not caught; the catch at line 185 only deletes the temp file and rethrows | confirmed |
| 4 | The area document states the current behavior as is (issue) | `docs/areas/sync-pipeline.md` line 158 (status table) and lines 172–177 (*A failed run*: "… or writing the mark … sets `plexConnected` to `false`") | confirmed |
| 5 | The seeding mark write (no Plex request) also clears the flag when it fails | line 277 is inside the same `try`; its exception reaches line 313 (related defect, fixed here) | confirmed |
| 6 | `GetHighWaterMarkAsync` does not throw on a read error, only on cancellation | `StateStore.cs` lines 83–118: `ReadAsync` catches `JsonException` and every other exception and returns "no mark"; line 146 `_gate.WaitAsync` throws only on cancellation, which line 307 rethrows | confirmed |
| 7 | In a history run, every failure after `GetHistorySinceAsync` returned that reaches `HandleError` is the mark write | per-item reads and writes are caught in `TryProcessRatingKeyAsync` (lines 204–220), aggregates in `UpdateAffectedShowAggregatesAsync` (lines 229–248); both rethrow only a cancellation of the run's token, which line 307 rethrows past `HandleError` | confirmed |
| 8 | Every failure of a reconcile that reaches `HandleError` comes from a Plex request: the library list (line 328) or a page of a library's contents | `LibraryReconciler.cs`: item writes (lines 87–98, 171–182) and per-show work (lines 114–130) are caught; only the `await foreach` over `GetLibraryItemsAsync` (lines 81, 110) propagates; tests `SyncOrchestratorReconcileMovieLibraryMidStreamFailureKeepsEarlierWrite` and `SyncOrchestratorReconcileLibraryItemsFailureRecordsOriginalError` show the original exception surfaces unwrapped | confirmed |
| 9 | Plex failures have no closed set of exception types | `PlexClient.GetAsync` (lines 460–474): `HttpRequestException` (`EnsureSuccessStatusCode`), `TaskCanceledException` (timeout), `JsonException` (unreadable answer, also a non-numeric `librarySectionID`, area doc lines 188–192), plus whatever `HttpClient` throws for a bad URI; tests use `InvalidOperationException` | confirmed |
| 10 | The owner cache is cleared on every failed run, the mark write included, and the area document says so ("after a failed run it is resolved again", line 80) | `HandleError` line 102 (unchanged by this plan) | confirmed |
| 11 | No existing test starts a failing run with `PlexConnected = true`, so none detects a failure that leaves the flag untouched | `SyncOrchestratorTests.cs` lines 406–444, 1462–1500 start from the default `false`; only the mid-stream reconcile tests (lines 1342, 1377) set it `true` first through the library list | confirmed |
| 12 | `docs/ARCHITECTURE.md`, `README.md`, `docs/areas/dashboard-and-auth.md` and the decision records state no rule that this change makes untrue | grep for `plexConnected`, `PlexConnected`, `disconnected`, `failed run`, `HandleError` under `docs/` and `README.md`: ARCHITECTURE.md lines 114–120 and 133 defer to the area document; dashboard-and-auth.md line 175 defers to sync-pipeline.md; 0006 line 22 and 0017 line 16 concern item errors and Plex failures | confirmed |

## Acceptance criteria

- [ ] AC1: A history run in which Plex answered the history request and only the mark write afterwards fails
  ends with `plexConnected` `true`; it still counts one error, records the write failure's message as the last
  error, leaves `lastPollAt` and the status's high-water mark unset and clears `isRunning`.
- [ ] AC2: A run without a mark whose seeding mark write fails leaves `plexConnected` as it was before the run
  (`true` stays `true`); it still counts one error and records the message as the last error.
- [ ] AC3: A run that fails on a request to Plex still sets `plexConnected` to `false`, also when it was `true`
  before the run: the owner resolution and the history request in a history run, the library list and a page of
  a library's contents in a reconcile.

The decision is made in `SyncOrchestrator.ProcessHistoryAsync` (which phase the run was in when it failed) and
`HandleError` (which applies it); see *Approach*.

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | `SyncOrchestratorProcessHistoryMarkWriteFailureKeepsPlexConnected` — mark `_since`, one history entry viewed after it, `FakeStateStore.SetException = new IOException("read-only file system")`, flag at its default `false`: after the run `PlexConnected` is `true`, `Errors` is 1, `LastError` is `"read-only file system"`, `LastPollAt` and `HighWaterMark` of the snapshot are `null`, `IsRunning` is `false` | `tests/PlexToJellyfinSync.Tests/SyncOrchestratorTests.cs` |
| AC2 | `SyncOrchestratorFirstRunMarkWriteFailureKeepsPlexConnected` — no mark, `PlexConnected` set `true` before the run, `SetException = new UnauthorizedAccessException("access denied")`: `PlexConnected` stays `true`, `Errors` is 1, `LastError` is `"access denied"`, `OwnerAccountIdCalls` is 0 | `tests/PlexToJellyfinSync.Tests/SyncOrchestratorTests.cs` |
| AC3 | `SyncOrchestratorProcessHistoryOwnerFailureMarksPlexDisconnected` (flag `true` first, mark `_since`, `FakePlexClient.OwnerAccountIdException = new HttpRequestException("unauthorized", null, HttpStatusCode.Unauthorized)` → `false`, `Errors` 1); `SyncOrchestratorProcessHistoryRequestFailureMarksPlexDisconnected` (flag `true` first, `HistoryException` → `false`); `SyncOrchestratorReconcileLibrariesFailureMarksPlexDisconnected` (flag `true` first, `LibrariesException` → `false`); a page of a library's contents is covered by the existing `SyncOrchestratorReconcileMovieLibraryMidStreamFailureKeepsEarlierWrite` and `SyncOrchestratorReconcileSeriesLibraryMidStreamFailureKeepsEarlierShow` | `tests/PlexToJellyfinSync.Tests/SyncOrchestratorTests.cs` |

Test doubles (Tester, step 5):

- `FakeStateStore` gains `public Exception? SetException { get; set; }`; when set, `SetHighWaterMarkAsync`
  throws it before storing or recording the value. It models fact 3: the real store propagates the write
  failure and leaves the stored mark unchanged.
- `FakePlexClient` gains `public Exception? OwnerAccountIdException { get; set; }`; when set,
  `GetOwnerAccountIdAsync` counts the call and then throws it (models the `401`/`403`/timeout propagation of the
  area document, *Owner only*).

## Approach

In `ProcessHistoryAsync` a local `bool` declared before the `try` records whether a Plex request is in flight:
set to `true` immediately before `ResolveOwnerAsync`, set back to `false` immediately after
`GetHistorySinceAsync` returned (next to the existing `PlexConnected = true`). The catch passes it to
`HandleError(ex, <that bool>)`. `ReconcileAsync` passes `true` (fact 8). `HandleError` sets
`s.PlexConnected = false` only when its new parameter is `true`; logging, `Errors++`, `LastError` and the
owner-cache reset stay unconditional (fact 10). No other line changes; the summary of `HandleError` names the
new condition.

Deviation accepted in step 9: the Dev built the flag as two write-once locals (`plexRequestStarted` set before
`ResolveOwnerAsync`, `plexAnswered` set after `GetHistorySinceAsync`) and passes
`plexRequestStarted && plexAnswered == false`, because resetting a single bool is a useless assignment to Sonar
(S1854). Both forms yield `true` exactly while the owner resolution or the history request is in flight.

No new user-visible output: the dashboard card and `/health` show the existing flag.

## Options considered

1. **Positional flag in `ProcessHistoryAsync`** (chosen): the orchestrator knows which call it is awaiting;
   two assignments and one parameter.
2. **Classify the exception by type** (`HttpRequestException`/`TaskCanceledException` → Plex): lost, because
   the Plex client's failures have no closed set of types (fact 9) and the state store's are any I/O exception;
   a misclassification would silently keep a dead server reported as connected.
3. **Own `try`/`catch` around each mark write** with a handler that does not touch the flag: lost, because it
   duplicates `HandleError`'s logging, counting, owner-cache reset and the cancellation filter in two more
   catch blocks for the same behavior as option 1.

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| PlexToJellyfinSync.Service | `SyncOrchestrator.cs` | `HandleError` gains the parameter; `ProcessHistoryAsync` tracks the Plex phase; `ReconcileAsync` passes `true` |
| PlexToJellyfinSync.Tests | `SyncOrchestratorTests.cs`, `FakeStateStore.cs`, `FakePlexClient.cs` | five new tests, two new fake properties |

## Signatures (for the Dev's skeleton)

```csharp
/// <param name="ex">Exception</param>
/// <param name="plexRequestFailed"><c>true</c> if the run failed on a request to Plex, which clears the Plex connection flag</param>
private void HandleError(Exception ex, bool plexRequestFailed)
```

Test-double properties (Tester): `FakeStateStore.SetException` and `FakePlexClient.OwnerAccountIdException`,
both `public Exception? … { get; set; }`.

## Test files

- `tests/PlexToJellyfinSync.Tests/SyncOrchestratorTests.cs` (new tests)
- `tests/PlexToJellyfinSync.Tests/FakeStateStore.cs`, `tests/PlexToJellyfinSync.Tests/FakePlexClient.cs`
  (test-double extensions)

Existing test code that calls a changed signature: none (`HandleError` is private).

Files of the change no unit test can cover: none.

## Areas

`docs/areas/sync-pipeline.md` — *Run status* (`plexConnected` row) and *Limits and errors* (*A failed run*,
*State directory not writable*): only a failed request to Plex sets `plexConnected` to `false`.

## Documentation updates

Owner: Dev (step 6), all in `docs/areas/sync-pipeline.md`:

- Status table, `plexConnected` row, to read: "`true` once a reconcile's library list or a history run's
  history request succeeded; `false` at start and after a run that failed on a request to Plex (see *A failed
  run*). A run that fails only on writing the mark leaves it unchanged, as do a run that only sets a missing
  mark (it makes no request to Plex) and a failing item."
- *A failed run*, the sentence "The run then stops, … and sets `plexConnected` to `false`." to read: "The run
  then stops, logs the error `Synchronization run failed`, counts one error and records the message as the last
  error. When the failed request was one to Plex it sets `plexConnected` to `false`; a failure to write the mark
  leaves `plexConnected` as it is, so a history run in which Plex answered still reports it as connected."
- *State directory not writable*, append: "`plexConnected` is not affected by these failures."

Sweep: `docs/ARCHITECTURE.md`, `README.md`, `docs/areas/dashboard-and-auth.md` and the decision records need no
change (fact 12).

## Architecture check

Guarantees in `.squad/project.md`: item isolation and the `Worker` surviving every error (0006, 0013) are
untouched — the same catch blocks run, only the flag update inside `HandleError` becomes conditional. The mark
seeding (0005) and owner-cache behavior are unchanged. No `docs/ARCHITECTURE.md` update.

## Security considerations

- [x] No limits on parsed input are added or changed.
- [x] User-visible failure text: unchanged — `LastError` still carries the exception's message as today; this
  change adds no new path to it.

## Decision records

- none: no decision beyond the obvious fix; the rule is written in the area document.

## Out of scope / follow-ups

- Follow-up issue (related defect found while planning): `StateStore.SetHighWaterMarkAsync` leaves
  `state.json.tmp` behind when `PreserveFileMode` (`StateStore.cs` line 192) or `File.Move` (line 194) fails —
  the temp-file cleanup at line 185 only covers the serialization — while `docs/areas/sync-pipeline.md`
  (*State file*) says "A failed write deletes the temporary file".
