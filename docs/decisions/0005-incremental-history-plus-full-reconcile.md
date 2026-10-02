# 0005: Incremental history sync plus periodic full reconcile

- **Status:** Accepted
- **Date:** 2026-10-01 (recorded retroactively)
- **Source:** `docs/ARCHITECTURE.md`, `README.md`, `SECURITY.md` — documents the existing design
- **Supersedes:** —

## Context

Reading every library on every poll is expensive; reading only Plex history misses changes that produce
no history entry (e.g. items marked watched by other means). A first start must not replay the owner's
entire watch history as a flood of NFO writes.

## Options considered

1. **Full reconcile on every poll** — always correct, too expensive at short intervals.
2. **History only** — cheap, but misses changes without a history entry.
3. **Both: cheap incremental history polling plus a periodic full reconcile.**

## Decision

Option 3. `ProcessHistoryAsync` runs every poll from a persisted high-water mark (`StateStore`,
`state.json`); `ReconcileAsync` runs on startup and every `Sync:FullReconcileIntervalHours` (minimum 1 h).
On the very first run without a high-water mark, the mark is seeded to "now" and nothing is processed —
the startup reconcile covers the current state instead.

Reconcile streams library items page by page (`IAsyncEnumerable`) and runs libraries and a show's
episodes in parallel within configurable bounds, trading strict snapshot consistency for bounded memory
and shorter wall-clock time.

## Consequences

- Changes without a history entry appear after the next reconcile at the latest.
- A library change during a long reconcile can be missed by one page until the next reconcile.
- `state.json` holds exactly one value; it is written atomically, and a corrupt file is moved aside and
  treated as "no high-water mark yet".
