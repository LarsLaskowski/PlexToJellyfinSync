# 0004: Path mapping is mandatory; unmapped paths are skipped

- **Status:** Accepted
- **Date:** 2026-10-01 (recorded retroactively)
- **Source:** `docs/ARCHITECTURE.md`, `README.md`, `SECURITY.md` — documents the existing design
- **Supersedes:** —

## Context

The file path reported by Plex flows almost directly into a filesystem write path (`PathMapper` →
`NfoWriter`). A crafted or corrupted Plex response must not be able to make the container write outside
the intended media roots.

## Options considered

1. **Pass unmapped paths through unchanged** — convenient when the container mounts media at the same
   path as Plex, but any path Plex reports becomes writable.
2. **Require a matching `PathMappings` entry** — needs explicit configuration even for identity mappings,
   but only configured roots are ever written.

## Decision

Option 2, with defense in depth:

- `PathMapper` uses the longest matching `PathMappings` prefix, rejects `..` traversal patterns, and
  returns `null` for any unmapped path; the item is skipped with a warning.
- `NfoWriter` independently canonicalizes the target (`Path.GetFullPath`) and refuses any target outside a
  configured `PathMappings:N:Local` root, regardless of what `PathMapper` returned.

## Consequences

- An identity mapping must still be configured explicitly (`README.md`, identity-mapping note).
- A `Local` root below a series/season directory makes `tvshow.nfo` / `season.nfo` writes refused, since
  those targets lie above it.
- This is a security guarantee: it may not be relaxed without a new decision record.
