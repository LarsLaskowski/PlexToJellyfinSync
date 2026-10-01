# 0001: Poll Plex instead of using webhooks

- **Status:** Accepted
- **Date:** 2026-10-01 (recorded retroactively)
- **Source:** `docs/ARCHITECTURE.md`, `README.md`, `SECURITY.md` — documents the existing design
- **Supersedes:** —

## Context

Plex can push events through webhooks, but webhooks require a Plex Pass subscription and only fire for
playback events. Items marked as watched manually in Plex do not produce a webhook. The tool should work
for every Plex owner and keep Jellyfin in step with *all* watch-state changes (`README.md`, Features).

## Options considered

1. **Webhooks** — near-real-time, no polling load; requires Plex Pass, needs an inbound HTTP endpoint
   reachable from Plex, misses manual "mark as watched".
2. **Polling the Plex HTTP API** — no subscription, no inbound endpoint, sees manual changes; changes
   arrive with a delay of up to the poll interval and cost periodic API calls.

## Decision

Polling only. The `Worker` polls the history endpoint every `Sync:PollIntervalSeconds` and runs a full
reconcile every `Sync:FullReconcileIntervalHours` (see 0005). There is no webhook endpoint.

## Consequences

- Works without Plex Pass and without exposing an endpoint to Plex.
- Watch state reaches Jellyfin with a delay (poll interval, minimum 5 s).
- Adding webhooks later would be an *additional* trigger for the same pipeline, not a replacement — the
  manual-mark case still needs polling.
