# 0006: Isolate failures per item and keep the worker alive

- **Status:** Accepted
- **Date:** 2026-10-01 (recorded retroactively)
- **Source:** `docs/ARCHITECTURE.md`, `README.md`, `SECURITY.md` — documents the existing design
- **Supersedes:** —

## Context

One bad item — a read-only media directory, an I/O error, a transient Plex timeout — must not stop the
sync for every other item, and an unexpected exception must not kill the only `BackgroundService`.

## Options considered

1. **Fail the whole run on the first error** — simple, but one poisoned item blocks all progress, and the
   high-water mark would never advance past it.
2. **Catch per item, count the error, continue** — progress continues; the failing item is retried later.

## Decision

Option 2. Each per-item call is wrapped in its own try/catch (`HandleItemError`), counted in
`SyncStatusViewData.Errors` without clearing `PlexConnected`. Both entry points additionally catch
unexpected exceptions (`HandleError`) so the `Worker` loop survives; a cancellation of the run's own token
is rethrown so host shutdown is not reported as an error. The incremental high-water mark advances past a
failing history entry.

## Consequences

- A failed item's watch state is only recovered by the next full reconcile (see 0005).
- Errors are visible on the dashboard and `/health` instead of stopping the service.
