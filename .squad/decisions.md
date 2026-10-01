# Decisions

How the **squad itself** works (roles, pipeline, limits). Append-only; one entry per decision: date,
decision, reason. Decisions about the **code** — why something was implemented the way it was — are not
recorded here but as decision records in [`docs/decisions/`](../docs/decisions/README.md).

- 2026-09-30 — Adopt the Squad file layout plus spec-driven work folders (`specs/`) and map roles to
  Claude subagents. Reason: Squad itself targets the Copilot CLI and cannot be run in the Claude Code
  cloud session; the Markdown layout is tool-neutral.
- 2026-10-01 — Issues and features share one pipeline: Lead plans, Security reviews the plan (max 2
  rejections), tests first, Dev implements, Style Manager runs before the review, Reviewer + Security
  review the diff (1 full pass + max 2 delta rounds), Lead approves the PR. `squad-issue` replaces
  `fix-issue`. Reason: one consistent, bounded workflow; the human acts as Product Manager and is only
  asked when the Lead cannot decide.
- 2026-10-01 — Invoking `squad-issue` or `squad-spec` is the user's approval for the commit, push and
  pull-request steps those skills document. Reason: the Lead approves the PR; the Product Manager should
  not be a routine gate.
- 2026-10-01 — The Lead records the reasoning behind code decisions as decision records in
  `docs/decisions/` (one file per decision, append-only, committed with the change). Reason: the
  reasoning must stay available months later, independent of PRs, issues and sessions, without bloating
  `docs/ARCHITECTURE.md`.
