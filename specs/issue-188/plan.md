# Plan: Guard ProcessHistoryAsync in the Worker like ReconcileAsync

Source: Issue #188
Status: Approved
Tier: standard — the change alters the `Worker`'s exception and cancellation control flow (a behavior change) but touches no security area (no tokens, auth, path mapping, file writes, XML parsing, Plex HTTP calls, new logging of external data, Docker/CI or dependencies).

## Problem / root cause

`src/PlexToJellyfinSync/Worker.cs:104` calls `_orchestrator.ProcessHistoryAsync(stoppingToken)` directly inside
the poll loop of `ExecuteAsync`, while the startup and periodic reconcile calls (lines 85 and 108) go through
`SafeReconcileAsync` (lines 52-68), which logs and swallows non-cancellation errors (decision 0012). Any
`ISyncOrchestrator` whose `ProcessHistoryAsync` throws therefore escapes `ExecuteAsync`, and since .NET 8 that
stops the host (`BackgroundServiceExceptionBehavior.StopHost`). With the production `SyncOrchestrator`
(`src/PlexToJellyfinSync.Service/SyncOrchestrator.cs:352-407`) this is not reachable today — it catches
everything through `HandleError` and rethrows only a cancellation of its own token (decision 0006) — but the
`ISyncOrchestrator` contract does not promise that.

Related gap found while investigating: `SafeReconcileAsync` (line 58) rethrows **every**
`OperationCanceledException`, including one whose token was not cancelled (e.g. an HttpClient timeout), which
would also stop the host. The issue's requirement "cancellation of the worker's own token still propagates" is
implemented precisely by filtering on the token, so both guards get
`when (cancellationToken.IsCancellationRequested)` — the same rule the orchestrator uses.

Decision (Proposed record 0013, superseding 0012): guard both calls alike (issue option 1).

## Acceptance criteria

- [ ] AC1: When `ISyncOrchestrator.ProcessHistoryAsync` throws a non-cancellation exception (e.g.
  `InvalidOperationException`) on a poll, the `Worker` logs exactly one entry at `LogLevel.Error` carrying
  that exception, with the message `Incremental history sync failed`.
- [ ] AC2: In the AC1 scenario the worker keeps running: after the error is logged, `ExecuteTask` is neither
  faulted nor completed, the loop starts its next poll iteration, and `StopAsync` completes with
  `ExecuteTask.IsCompletedSuccessfully == true`.
- [ ] AC3: When the host stops while `ProcessHistoryAsync` is running (it waits on its token and throws the
  resulting `OperationCanceledException`), no error is logged and the cancellation propagates
  (`ExecuteTask.Status == TaskStatus.Canceled`).
- [ ] AC4: When `ReconcileAsync` throws an `OperationCanceledException` while the worker's token is **not**
  cancelled (e.g. `new OperationCanceledException("timeout")` or a `TaskCanceledException` without a
  token), the `Worker` logs one error `Full reconcile failed` carrying that exception and keeps running
  (`ExecuteTask` not faulted/completed/cancelled; `StopAsync` completes successfully).
- [ ] AC5: Same as AC4 for `ProcessHistoryAsync`: an `OperationCanceledException` with an uncancelled worker
  token is logged as `Incremental history sync failed` and the worker keeps running.
- [ ] AC6: The existing `WorkerTests` (`WorkerStartupReconcileFailureLogsFullReconcileFailedError`,
  `WorkerStartupReconcileFailureKeepsWorkerRunning`, `WorkerStopDuringStartupReconcileLogsNoError`) stay green
  unchanged.

Test notes for the Tester:

- Extend `tests/PlexToJellyfinSync.Tests/FakeSyncOrchestrator.cs` symmetrically to the reconcile members:
  `ProcessHistoryException` (`Exception?`), `ProcessHistoryWaitsForCancellation` (`bool`) and a
  `ProcessHistoryCalled` task signalled on the first call. Keep `ProcessHistoryAsync` completing normally when
  nothing is configured, so the existing tests are unaffected.
- The poll interval is clamped to at least 5 s (`Worker.cs:77`) and the Worker has no injectable clock, so
  poll-path tests use `PollIntervalSeconds = 5` and wait on signals with a generous timeout (e.g. 30 s), never
  on fixed sleeps. Add an overload or parameter to `CreateWorker` for the poll interval (and, for AC2, the
  status provider) rather than duplicating the construction.
- AC2 "next poll iteration": the loop calls `_status.Update(s => s.NextPollAt = …)` at the start of every
  iteration; subscribe to `FakeSyncStatusProvider.Changed` before `StartAsync` and wait until it has fired at
  least twice (the second time is after the failed `ProcessHistoryAsync`). This proves continuation in about
  5 s instead of waiting 10 s for a second `ProcessHistoryAsync` call.
- AC1/AC5 run with a non-throwing `ReconcileAsync`, so the only error entry comes from the history guard.
- Without the fix AC1/AC2/AC5 fail (nothing is logged and `ExecuteTask` faults); AC4 fails on the current code
  because the unfiltered `catch (OperationCanceledException) { throw; }` ends the worker as cancelled with no
  error logged.

## Approach

1. In `Worker`, add `SafeProcessHistoryAsync` mirroring `SafeReconcileAsync`:
   - `await _orchestrator.ProcessHistoryAsync(cancellationToken).ConfigureAwait(false);`
   - `catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }`
   - `catch (Exception ex) { _logger.LogError(ex, "Incremental history sync failed"); }` with the same
     defense-in-depth comment style as the reconcile guard.
2. In `SafeReconcileAsync`, change `catch (OperationCanceledException)` to
   `catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)`.
3. In `ExecuteAsync`, replace line 104 with `await SafeProcessHistoryAsync(stoppingToken).ConfigureAwait(false);`.
   Nothing else in the loop changes (the reconcile check still follows in the same iteration).
4. Update the XML summaries: `SafeReconcileAsync` — "…swallow errors other than the worker's own cancellation
  as defense-in-depth…"; `SafeProcessHistoryAsync` — "Process the incremental watch history and swallow errors
  other than the worker's own cancellation as defense-in-depth, so an orchestrator that throws cannot stop the
  host". Both methods stay inside `#region Methods`.
5. Two separate methods, not a generic delegate helper: a non-constant log template would trigger CA2254 /
  Sonar (see 0013, option 4).

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| PlexToJellyfinSync | `Worker` / `src/PlexToJellyfinSync/Worker.cs` | New private `SafeProcessHistoryAsync`; cancellation filter in `SafeReconcileAsync`; loop calls the new guard |
| PlexToJellyfinSync.Tests | `FakeSyncOrchestrator` / `tests/PlexToJellyfinSync.Tests/FakeSyncOrchestrator.cs` | Configurable history failure, wait-for-cancellation, call signal |
| PlexToJellyfinSync.Tests | `WorkerTests` / `tests/PlexToJellyfinSync.Tests/WorkerTests.cs` | Tests for AC1-AC5; `CreateWorker` takes the poll interval (and optionally the status provider) |
| docs | `docs/ARCHITECTURE.md` | Sync pipeline point 1 (see below) |
| docs | `docs/decisions/0013-worker-guards-both-orchestrator-calls.md` | New record (Lead) |

## Signatures (for the Dev's skeleton)

New, in `Worker` (`#region Methods`):

```csharp
private async Task SafeProcessHistoryAsync(CancellationToken cancellationToken)
```

Unchanged signature, changed body: `private async Task SafeReconcileAsync(CancellationToken cancellationToken)`.
No public or internal production member is added or changed; the test fakes are the Tester's. Because the new
member is private and not visible to tests, the skeleton step is optional: if the Dev adds it, the method throws
`NotImplementedException` and is **not** yet wired into `ExecuteAsync`, so the build stays green and the step-5
tests fail on the unchanged loop.

## Documentation updates

- `docs/ARCHITECTURE.md`, "Sync pipeline" point 1 (lines 48-55), made by the Dev: state that **both**
  orchestrator calls go through a guard — `SafeReconcileAsync` for the startup and periodic reconcile,
  `SafeProcessHistoryAsync` for every poll — which log and swallow every error except a cancellation of the
  worker's own token, as defense-in-depth on top of the orchestrator's own error handling, so neither call can
  crash the host; link [0013](decisions/0013-worker-guards-both-orchestrator-calls.md) instead of 0012.
  Suggested text for the middle of the point: "…then loops: wait `Sync:PollIntervalSeconds` (minimum 5s), call
  `ISyncOrchestrator.ProcessHistoryAsync` through `SafeProcessHistoryAsync`, and — once
  `Sync:FullReconcileIntervalHours` (minimum 1h) has elapsed since the last reconcile — call
  `ISyncOrchestrator.ReconcileAsync` again through `SafeReconcileAsync`. Both guards log and swallow every error
  except a cancellation of the worker's own stopping token, which still propagates so host shutdown is not
  reported as a failure (see [0013](decisions/0013-worker-guards-both-orchestrator-calls.md))."
- `README.md`: none (no configuration or env var change).
- Decision index (`docs/decisions/README.md`) and the status line of 0012 (`Superseded by 0013`): the Lead,
  at PR approval when 0013 becomes `Accepted`.

## Architecture check

- Strengthens the documented guarantee that the `Worker` loop survives orchestrator failures (ARCHITECTURE.md
  sync pipeline points 1 and 2; decisions 0006, 0012); nothing is weakened.
- Host shutdown is still not reported as an error: a cancellation of the stopping token propagates from both
  guards exactly as before (AC3 and the existing `WorkerStopDuringStartupReconcileLogsNoError`).
- The `Task.Delay` cancellation handling (lines 95-102) and the sequential, non-overlapping loop are unchanged.
- NFO, path-mapping and dashboard-auth guarantees are not touched.

## Security considerations

None beyond the tier's diff review. The new log entry passes the exception object to the logger exactly as the
existing reconcile guard does; it adds no new external data to a log message template (the message is a
constant). Swallowing errors cannot hide a security failure that would otherwise be visible: the alternative is
the host stopping, and the error is still logged at `Error`.

## Decision records

- `docs/decisions/0013-worker-guards-both-orchestrator-calls.md` (Proposed) — supersedes 0012: guard both
  orchestrator calls alike, filter the cancellation rethrow on the worker's own token, two methods instead of a
  generic helper.

## Out of scope / follow-ups

- Injecting a `TimeProvider` into `Worker` to make the poll/reconcile intervals testable without real delays —
  not needed for this fix; the 5 s minimum is acceptable for the few poll-path tests.
- Reporting guard-caught failures in `SyncStatusViewData` (dashboard error counter) — the orchestrator owns
  status reporting (0006); accepted as a limitation in 0013.
