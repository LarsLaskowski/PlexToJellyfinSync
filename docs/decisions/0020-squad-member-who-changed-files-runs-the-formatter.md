# 0020: In the squad, the member that changed the files runs the formatter

- **Status:** Accepted
- **Date:** 2026-10-17
- **Area:** —
- **Source:** Squad update from Squad-Spec-Repository-Template (template PR #88)
- **Supersedes:** [0009](0009-quality-gates-before-the-pull-request.md) — amends only who runs `reihitsu-format` in the squad skills; the rest of 0009 stands

## Context

Decision 0009 made the Code Officer the only squad member that runs `reihitsu-format`. Every Dev or Tester
change therefore cost an extra Code Officer launch just to run a deterministic command, although the change
needed no analyzer judgment. The squad template now lets the member that changed the files format them
(`.squad/team.md`, *Shared rules*).

## Options considered

1. **Keep the Code Officer as the only formatter** — one owner, but a launch per hand-off for a command that
   needs no judgment.
2. **The member that changed the files runs *Format* on them before handing back; the Code Officer keeps the
   analyzer gate** — fewer launches, formatting still happens before every review.

## Decision

Option 2. Inside the squad, the Dev, the Tester or the orchestrator runs *Format* from `.squad/stack.md` on the
files it changed before handing back; the Code Officer owns a clean *Analyzer gate* and is launched for analyzer
diagnostics or style findings that need reading.

## Consequences

- Formatting is still required before every push, and CI still does not check it (0009 unchanged there).
- A file the formatter touches outside the member's change is reported and restored, not edited.
