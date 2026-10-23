# 0021: Four area documents; released records get their area from this record

- **Status:** Accepted
- **Date:** 2026-10-23
- **Area:** —
- **Source:** Issue #224 (Product Manager decision on the areas)
- **Supersedes:** —

## Context

What the software guarantees today was spread over `docs/ARCHITECTURE.md` (mixed with component structure and
class names), `README.md`, `SECURITY.md`, `.squad/project.md` and the decision records; `docs/areas/` had no area.
Once `docs/areas/README.md` lists an area, `.squad/tools/decision-check.py` requires every decision record to name
one. Records 0001–0014 are released in `v1.5.0` and append-only, so they cannot gain an `Area:` line; the
decision-record rules let a later record assign their area in an `## Area assignment` table instead.

## Options considered

1. **One area per project** (`Core`, `Service`, `Host`, …) — mirrors the code layout, but the behavior a user or
   operator relies on (an `.nfo` write, a login) crosses projects, and the project names are already the PR areas.
2. **Four behavior areas: Sync pipeline, Path mapping and NFO, Dashboard and auth, Configuration** — each owns one
   kind of contract (what is synced when, what is written where, the web surface, what an operator sets and runs);
   decided by the Product Manager.
3. **One document for everything** — no index to maintain, but it would grow past the size rule and repeat
   `docs/ARCHITECTURE.md`.

For the released records: superseding each of 0001–0014 only to add an area would create fourteen records that
change no decision; the assignment table in one record leaves them untouched.

## Decision

Option 2, with the slugs and scopes listed in the [area index](../areas/README.md). The released records
0001–0014 take their area from the table below; every later record carries its own `Area:` line, and records
about the development process carry `—`.

## Consequences

- `docs/ARCHITECTURE.md` keeps the structure and links the area documents for behavior; a change in behavior
  updates its area document in the same pull request (`.squad/project.md`, *Integration surface*).
- The configuration settings are stated normatively in the Configuration area document and, for users, in the
  `README.md` table; both change together.
- A further area is a Lead decision in a plan; a record released later without an `Area:` line would need a new
  assignment table, which is why every new record carries the field.

## Area assignment

| Record | Area |
| ------ | ---- |
| 0001   | Sync pipeline |
| 0002   | Sync pipeline |
| 0003   | Path mapping and NFO |
| 0004   | Path mapping and NFO |
| 0005   | Sync pipeline |
| 0006   | Sync pipeline |
| 0007   | Dashboard and auth |
| 0008   | Sync pipeline |
| 0009   | — |
| 0010   | — |
| 0011   | — |
| 0012   | Sync pipeline |
| 0013   | Sync pipeline |
| 0014   | — |