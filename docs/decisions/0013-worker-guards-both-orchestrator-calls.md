# 0013: Worker guards both orchestrator calls alike

- **Status:** Accepted
- **Date:** 2026-10-02
- **Source:** Issue #188 / `specs/issue-188/`
- **Supersedes:** 0012

## Context

Decision 0012 kept `Worker.SafeReconcileAsync` (`src/PlexToJellyfinSync/Worker.cs`) as defense-in-depth: it
logs and swallows a non-cancellation exception from `ISyncOrchestrator.ReconcileAsync` so an orchestrator that
throws cannot stop the host. The incremental call in the poll loop, `ISyncOrchestrator.ProcessHistoryAsync`,
was left unguarded. 0012 named exactly this asymmetry as a reason to revisit, and issue #188 raises it.

The forces are the same for both calls: the `Worker` depends on the `ISyncOrchestrator` abstraction, whose
contract does not promise that it never throws; since .NET 8 an exception escaping a `BackgroundService` stops
the host (`BackgroundServiceExceptionBehavior.StopHost`). Today `SyncOrchestrator.ProcessHistoryAsync` catches
every exception itself (`HandleError`, decision 0006), so the gap is not reachable in production — but
`ProcessHistoryAsync` runs on every poll (every few seconds) instead of every few hours, so a future regression
there would take the container down far sooner than one in `ReconcileAsync`.

A second, smaller gap: `SafeReconcileAsync` rethrows **every** `OperationCanceledException`, including one
whose token was not cancelled (for example an HttpClient timeout surfacing from a future orchestrator). Such an
exception would escape `ExecuteAsync` and stop the host just like any other error. The orchestrator itself
(0006) only rethrows a cancellation when its run's token was actually cancelled.

## Options considered

1. **Guard `ProcessHistoryAsync` the same way and treat both calls alike** — one consistent safety net at the
   host boundary for every orchestrator call; the guard is cheap and unit-testable with a throwing fake. Cost:
   a second small private method whose generic branch is unreachable with today's orchestrator.
2. **Leave `ProcessHistoryAsync` unguarded and record why** — no additional unreachable branch; the "worker
   survives" guarantee for the poll loop rests solely on `SyncOrchestrator` (0006). Rejected: it keeps the
   asymmetry 0012 flagged, and the more frequently called path is the less protected one.
3. **Remove both guards and document a never-throws contract on `ISyncOrchestrator`** — symmetric and no
   unreachable code; but the guarantee would depend on every implementation honouring an XML-doc promise that
   the compiler cannot check, and 0012 already rejected removing the reconcile guard for that reason.
4. **One generic helper taking a delegate and a message** — removes the duplicated try/catch shape, but a
   non-constant log message template violates CA2254 / Sonar logging rules, and the two call sites gain nothing
   from the indirection.

## Decision

Option 1. `Worker` gets `SafeProcessHistoryAsync`, mirroring `SafeReconcileAsync`: it awaits
`ISyncOrchestrator.ProcessHistoryAsync`, rethrows an `OperationCanceledException` only when the worker's own
token is cancelled, and logs any other exception at `Error` with the constant message
`"Incremental history sync failed"` and swallows it, so the loop continues with the reconcile check and the
next poll. In both guards the cancellation clause becomes
`catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)`, so a cancellation that
is not the host shutting down is logged as a failure like any other exception instead of stopping the host —
the same rule the orchestrator applies (0006). Two separate methods are kept rather than a generic helper
(option 4).

## Consequences

- Neither orchestrator call can stop the host with a non-shutdown exception; the poll loop's survival no longer
  depends on the orchestrator implementation alone.
- Both generic `catch (Exception)` branches are unreachable with the production `SyncOrchestrator`; they are
  covered by `Worker` unit tests with a throwing fake orchestrator.
- An `OperationCanceledException` from an uncancelled token is now logged as `Error` by the `Worker` (when it
  escapes the orchestrator) rather than ending the worker.
- The `Worker` guards do not touch `SyncStatusViewData` (errors counter, `LastError`); a failure that only the
  guard catches shows up in the log, not on the dashboard. That is accepted: the guard is a last-resort net,
  and the orchestrator remains responsible for status reporting (0006).
- Revisit if `ISyncOrchestrator` documents and enforces a never-throws contract, or if the `Worker` gains
  further orchestrator calls (they should go through the same kind of guard).
