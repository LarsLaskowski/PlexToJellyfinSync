# Plan: Fix the mislabeled reconcile error in Worker.SafeReconcileAsync

Source: Issue #79
Status: Approved
Tier: trivial — the change is a log message, an XML summary and a comment in `Worker`; control flow and behavior stay exactly as they are (the try/catch is kept), and no security area is touched.

## Problem / root cause

`src/PlexToJellyfinSync/Worker.cs:51-65` (the issue's line numbers 90-104 are stale) defines
`SafeReconcileAsync`, which logs `"Initial reconcile failed"` at line 63. It is called for the startup
reconcile (line 79) **and** for the periodic full reconcile (line 102), so a periodic failure is mislabeled
as "Initial".

The issue's second claim is verified: `SyncOrchestrator.ReconcileAsync`
(`src/PlexToJellyfinSync.Service/SyncOrchestrator.cs:410-445`) catches every exception through
`HandleError` and rethrows only `OperationCanceledException` when its own token is cancelled;
`HandleError` (line 107) and `SyncStatusService.Update` (subscriber exceptions are swallowed) do not throw.
So the `catch (Exception)` branch in the Worker is unreachable with the production orchestrator.

Decision (record 0012): keep the guard as deliberate defense-in-depth — the Worker depends on the
`ISyncOrchestrator` abstraction, an exception escaping a `BackgroundService` stops the host by default, and
`docs/ARCHITECTURE.md` names `SafeReconcileAsync` as the reason a failed reconcile does not crash the host —
and fix the message.

## Acceptance criteria

- [x] AC1: When `ISyncOrchestrator.ReconcileAsync` throws a non-cancellation exception (e.g.
  `InvalidOperationException`) on the startup reconcile, the `Worker` logs exactly one entry at
  `LogLevel.Error` carrying that exception, with the message `Full reconcile failed`; no logged message
  contains `Initial reconcile failed`.
- [x] AC2: In the same scenario the worker keeps running: `ExecuteTask` is not faulted after the error has
  been logged, and `StopAsync` completes without throwing (the exception is swallowed, not propagated).
- [x] AC3: When the host stops (stopping token cancelled) and `ReconcileAsync` throws
  `OperationCanceledException`, no error is logged (cancellation is still rethrown, not reported as a
  failure).

Test notes for the Tester: new `tests/PlexToJellyfinSync.Tests/WorkerTests.cs`; hand-written fakes
`FakeSyncOrchestrator` (configurable exception for `ReconcileAsync`, a `TaskCompletionSource` signalled
when it is called; `ProcessHistoryAsync` completes immediately) and `RecordingWorkerLogger :
ILogger<Worker>` (records level, formatted message and exception; signals on an `Error` entry). Use the
existing `FakeSyncStatusProvider` and `Options.Create(new SyncOptions { PollIntervalSeconds = 3600 })` so
the loop parks in `Task.Delay`. Since .NET 10 `BackgroundService` runs `ExecuteAsync` off the caller's
thread, wait on the signals (with a timeout) instead of assuming the reconcile ran inside `StartAsync`.
The periodic call site goes through the same method; it is not tested separately (the interval minimum is
one hour and the Worker has no injectable clock).

## Approach

1. In `Worker.SafeReconcileAsync`, change the log message from `"Initial reconcile failed"` to
   `"Full reconcile failed"`.
2. Update its XML summary to: `Run a full reconcile and swallow non-cancellation errors as defense-in-depth,
   so an orchestrator that throws cannot stop the host`.
3. Add a one-line comment above the `catch (Exception ex)`:
   `// Defense-in-depth: SyncOrchestrator already handles its errors, but any ISyncOrchestrator that throws must not stop the host`.
4. Leave both `catch` clauses, both call sites and the unguarded `ProcessHistoryAsync` call unchanged.

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| PlexToJellyfinSync | `Worker` / `src/PlexToJellyfinSync/Worker.cs` | Log message, XML summary, comment in `SafeReconcileAsync` |
| PlexToJellyfinSync.Tests | `WorkerTests`, `FakeSyncOrchestrator`, `RecordingWorkerLogger` (new files) | Tests for AC1-AC3 |
| docs | `docs/ARCHITECTURE.md` | Sync pipeline point 1 wording (see below) |

## Signatures (for the Dev's skeleton)

None — no public or internal member is added or changed (`SafeReconcileAsync` keeps
`private async Task SafeReconcileAsync(CancellationToken cancellationToken)`). No skeleton step needed.

## Documentation updates

- `docs/ARCHITECTURE.md`, "Sync pipeline" point 1 (lines 48-50): state that `SafeReconcileAsync` guards
  both the startup and the periodic reconcile, and that it is defense-in-depth on top of the orchestrator's
  own `HandleError` (link `decisions/0012-worker-reconcile-guard-kept-as-defense-in-depth.md`). Suggested
  text: "On startup it runs one reconcile immediately (`SafeReconcileAsync`, which logs and swallows
  non-cancellation errors as defense-in-depth on top of the orchestrator's own error handling, so a failed
  reconcile cannot crash the host — see [0012](decisions/0012-worker-reconcile-guard-kept-as-defense-in-depth.md)), then loops: … call `ISyncOrchestrator.ReconcileAsync` again through the same guard."
- `README.md`: none (no configuration change).

## Architecture check

The guarantee "an unexpected exception must not kill the only `BackgroundService`" (decision 0006,
`docs/ARCHITECTURE.md` sync pipeline) is preserved and now explicitly documented as two layers. No NFO,
path mapping or dashboard auth behavior is touched.

## Security considerations

None: no tokens, auth, file writes, path mapping, XML parsing, Plex HTTP calls or dependencies. The log
entry still carries only the exception the orchestrator raised; no new external data is logged.

## Decision records

- `docs/decisions/0012-worker-reconcile-guard-kept-as-defense-in-depth.md` (Proposed) — keep the
  unreachable-in-production guard instead of removing it as the review finding offered.

## Out of scope / follow-ups

- Guarding `ProcessHistoryAsync` the same way (it relies solely on the orchestrator today) — not requested
  by the issue; no follow-up issue, the asymmetry is recorded in 0012 as a revisit trigger.
- Distinguishing "initial" and "periodic" in the log text (option 3 in 0012) — rejected.
