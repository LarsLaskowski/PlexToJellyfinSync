# 0012: Worker keeps its reconcile guard as defense-in-depth

- **Status:** Superseded by 0013
- **Date:** 2026-10-02
- **Source:** Issue #79 / `specs/issue-79/`
- **Supersedes:** —

## Context

`Worker.SafeReconcileAsync` (`src/PlexToJellyfinSync/Worker.cs`) wraps `ISyncOrchestrator.ReconcileAsync` in
a `catch (Exception)` that logs and swallows the error. It is called both for the startup reconcile and for
the periodic full reconcile, but logged "Initial reconcile failed" in both cases. Review finding F-607
(issue #79) also points out that the generic branch cannot run with the current `SyncOrchestrator`: per
decision 0006, `ReconcileAsync` already catches every exception (`HandleError`) and rethrows only an
`OperationCanceledException` of its own cancelled token; `HandleError` and `SyncStatusService.Update`
themselves do not throw (subscriber exceptions are swallowed in `Update`).

The `Worker` depends on the `ISyncOrchestrator` abstraction, whose contract does not state that it never
throws, and since .NET 8 an exception escaping a `BackgroundService` stops the host by default
(`BackgroundServiceExceptionBehavior.StopHost`). `docs/ARCHITECTURE.md` documents `SafeReconcileAsync` as
the reason a failed first reconcile does not crash the host.

## Options considered

1. **Remove the wrapper and call `ReconcileAsync` directly** — no unreachable branch, symmetric with the
   unguarded `ProcessHistoryAsync` call; but the "worker survives" guarantee would rest solely on the
   orchestrator implementation, and a future regression there (a throw in `HandleError` or in a `finally`
   block) would stop the container instead of being logged.
2. **Keep the wrapper as deliberate defense-in-depth and fix the message** — one cheap, explicit safety net
   at the host boundary; the branch is unreachable with today's orchestrator but reachable for any
   `ISyncOrchestrator` that throws, so it can be unit-tested with a fake.
3. **Keep the wrapper and log a call-site-specific message** (pass "initial"/"periodic" in) — more precise
   log text, but adds a parameter for information the surrounding log lines already give.

## Decision

Option 2. `SafeReconcileAsync` stays; its error message becomes `"Full reconcile failed"`, which is correct
for both call sites, and its XML summary and an inline comment state that it is defense-in-depth on top of
the orchestrator's own error handling (0006). `ProcessHistoryAsync` is left unguarded, unchanged by this
decision.

## Consequences

- The `catch (Exception)` branch is not reachable with the production `SyncOrchestrator`; it is covered by a
  `Worker` unit test with a throwing fake orchestrator.
- The log no longer says "Initial" for a periodic reconcile failure; distinguishing the two call sites would
  need option 3.
- Revisit if `ISyncOrchestrator` documents a never-throws contract, or if `ProcessHistoryAsync` gets the
  same guard (then both calls should be treated alike).
