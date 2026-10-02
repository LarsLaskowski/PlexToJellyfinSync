# 0008: Named HttpClient for the singleton PlexClient

- **Status:** Accepted
- **Date:** 2026-10-01 (recorded retroactively)
- **Source:** `docs/ARCHITECTURE.md`, `README.md`, `SECURITY.md` — documents the existing design
- **Supersedes:** —

## Context

All services in the sync path are singletons (one sequential background worker, no per-request scope).
`PlexClient` therefore lives for the whole process.

## Options considered

1. **Typed client** (`AddHttpClient<IPlexClient, PlexClient>`) — idiomatic, but a singleton would capture
   one `HttpClient` for the process lifetime, so `IHttpClientFactory`'s handler rotation (DNS changes,
   connection recycling) would never take effect.
2. **Named client** — `PlexClient` holds `IHttpClientFactory` and calls `CreateClient(nameof(IPlexClient))`
   per request.

## Decision

Option 2, configured once in `ServiceCollectionExtensions.AddPlexToJellyfinSync` (base address,
`X-Plex-Token`, `Accept: application/json`, 30 s timeout).

## Consequences

- Handler rotation works despite the singleton lifetime.
- Changing `PlexClient` to a typed client, or capturing an `HttpClient` in a field, would silently
  reintroduce the stale-handler problem.
