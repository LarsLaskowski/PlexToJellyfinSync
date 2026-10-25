# 0027: The state file records its schema version; an unknown version is set aside, not guessed at

- **Status:** Accepted
- **Date:** 2026-10-25
- **Area:** Sync pipeline
- **Source:** Issue #61
- **Supersedes:** [0005](0005-incremental-history-plus-full-reconcile.md) — amends only its consequence "`state.json` holds exactly one value": the file now also holds its schema version; the rest of 0005 stands

## Context

`state.json` held the high-water mark and nothing that names its format. The deserializer skips unknown values
and accepts missing ones, so a later release that changes the file's shape could neither recognize an older
file to migrate it nor tell that a file comes from a newer release it does not understand (a downgrade on the
same `/config` volume). A wrongly interpreted mark skips history entries silently; "no mark" is the documented
safe state, because the history run then seeds the mark and the full reconcile covers the gap (0005). Every
file written by the releases up to v1.6.0 carries no version and must keep its mark after the upgrade.

## Options considered

1. **No version; detect formats by shape when one changes** — no change now, but a shape test cannot tell a
   newer format that reuses `HighWaterMark` from today's, and the issue asks for the marker while the file is
   trivial.
2. **Version marker; an unknown version is read best-effort (its `HighWaterMark` used if present)** — keeps a
   mark across a downgrade, but guesses at a format whose meaning may have changed, which is what the marker is
   meant to prevent.
3. **Version marker; an unknown version is moved aside like a corrupt file and means "no mark"** — loses the
   mark on a downgrade from a future release, which only costs a seeded mark and one reconcile, and keeps the
   file for the operator.
4. **Version marker; an unknown version stops the application** — never loses data, but turns a recoverable
   state into an outage of the whole sync.

## Decision

Option 3. Every write records `SchemaVersion` `1`; a file without a version (or with `null`) is a version-1
file of an earlier release; any other integer version is moved to `state.json.unsupported` and read as "no
mark". The rules and log texts are in [Sync pipeline, *State file*](../areas/sync-pipeline.md#state-file).

## Consequences

- A future format bumps the version and branches on it when the file is read; files without a version stay
  readable as version 1.
- A downgrade from a release with a newer version costs the mark once (seeded, then a reconcile); the newer
  file is kept as `state.json.unsupported`, not merged back.
- A downgrade from this release to an earlier one keeps the mark, because earlier releases ignore the version.
- Unknown values next to a supported version are still ignored and dropped on the next write.