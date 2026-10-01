# 0007: Dashboard is open by default, optionally protected by a token

- **Status:** Accepted
- **Date:** 2026-10-01 (recorded retroactively)
- **Source:** `docs/ARCHITECTURE.md`, `README.md`, `SECURITY.md` — documents the existing design
- **Supersedes:** —

## Context

The tool is meant for a home network (`SECURITY.md`). The dashboard shows status and live logs; it must be
usable with zero configuration in the documented plain-HTTP quick start, but protectable when exposed
further.

## Options considered

1. **Mandatory authentication with user accounts** — heavier setup than the tool's scope justifies.
2. **No authentication at all** — unacceptable once the host is reachable beyond the home network.
3. **Unauthenticated by default, optional shared token with server-side sessions.**

## Decision

Option 3:

- `TokenAuthMiddleware` is a pass-through while `Dashboard:Token` is empty; a startup warning is logged
  when the dashboard is enabled without a token. `Dashboard:Enabled = false` maps only `/health`.
- With a token: constant-time comparison of SHA-256 hashes, a 256-bit random session id stored in
  `IMemoryCache` with a fixed 8 h lifetime (absolute, not sliding — matching the cookie's `MaxAge`), cookie `HttpOnly`, `SameSite=Strict`, and `Secure` only when the request
  arrived over HTTPS (a hard-coded `Secure` flag would break the plain-HTTP quick start), antiforgery on
  login/logout, exponential login backoff per client IP.
- Configured secrets are masked in the in-memory log buffer before it reaches the dashboard, because that
  buffer may be readable without authentication.

## Consequences

- Sessions are revocable (cache eviction) and lost on restart.
- Exposing the dashboard beyond a trusted network requires setting a token and a TLS-terminating reverse
  proxy; this is documented rather than enforced.
- Any change to this model is security-relevant and needs a new decision record.
