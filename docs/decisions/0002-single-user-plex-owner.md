# 0002: Sync a single user: the Plex server owner

- **Status:** Accepted
- **Date:** 2026-10-01 (recorded retroactively)
- **Source:** `docs/ARCHITECTURE.md`, `README.md`, `SECURITY.md` — documents the existing design
- **Supersedes:** —

## Context

Jellyfin's NFO user-data import (`watched`, `playcount`, `lastplayed`) is not per user: an `.nfo` file
carries exactly one watch state, which Jellyfin imports for one user (`README.md`, Features and the import
note). Plex, in contrast, tracks watch state per account.

## Options considered

1. **All Plex users** — not representable in a single `.nfo` file without one user overwriting another.
2. **A configurable Plex user** — still one user per `.nfo` file; adds configuration without changing
   the single-user limitation.
3. **The Plex server owner** — matches the token in use and the single-user NFO model.

## Decision

Only the Plex owner's watch state is synced. The owner account id is taken from `Plex:OwnerAccountId` or
resolved via `/accounts`, falling back to id `1` (see `docs/ARCHITECTURE.md`, `PlexClient`). In Jellyfin
the NFO data should be imported to the user that corresponds to the Plex owner.

## Consequences

- Other Plex users' watch state is never written.
- Multi-user support would need a different target than `.nfo` files (e.g. the Jellyfin API) and is a
  product decision, not a code change.
