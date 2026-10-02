# 0003: Existing NFO files are only changed in their watch fields

- **Status:** Accepted
- **Date:** 2026-10-01 (recorded retroactively)
- **Source:** `docs/ARCHITECTURE.md`, `README.md`, `SECURITY.md` — documents the existing design
- **Supersedes:** —

## Context

`.nfo` files sit next to the media on a shared volume. They are often curated by hand or written by other
tools (Kodi, Jellyfin itself, Windows tools). Rewriting them would destroy metadata this tool does not own.
`README.md` promises that existing `.nfo` files are left untouched except for the watch fields.

## Options considered

1. **Regenerate the whole file from Plex metadata** — simple, but overwrites foreign metadata, formatting
   and encoding.
2. **Update only `watched` / `playcount` / `lastplayed` in place** — more code (parse, compare, preserve),
   but no collateral changes.

## Decision

Option 2, implemented in `NfoWriter`:

- Existing files are parsed with `PreserveWhitespace`, only the three watch elements are changed, and a
  write is skipped entirely when nothing changed. A missing `LastPlayed` removes a stale `lastplayed`.
- Updated files are saved without re-indenting and keep the encoding detected from their byte-order mark.
- A file that fails to parse is logged and left untouched — never rebuilt.
- Full documents (`BuildDocument`) are only written when the file does not exist and
  `Sync:CreateMissingNfo` is `true`; new files are UTF-8 without BOM, as Jellyfin expects.
- Every write goes to `<name>.tmp` and replaces the target via an atomic move, preserving the Unix file
  mode; a per-target `SemaphoreSlim` serializes concurrent writers to the same file.

## Consequences

- This is a guarantee, not an implementation detail: changes that touch other elements of an existing
  `.nfo` need a new decision record and Product Manager approval.
- Metadata in an existing file is never refreshed from Plex.
- The per-target semaphore dictionary grows with the number of targets ever written (acceptable for a
  media library, see `docs/ARCHITECTURE.md`).
