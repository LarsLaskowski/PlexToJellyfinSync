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
- 2026-10-01 — Commits and pushes to a feature branch are always allowed (never to `main` — every
  change goes through a branch and a pull request; no force-push, no tags without approval); pull requests are only opened by the squad (after the Lead's approval) or when the user asks.
  Reason: work is secured continuously instead of waiting for approval, while the PR stays a deliberate
  step.
- 2026-10-01 — Workflow review: the Lead classifies every change into a tier (`trivial`, `standard`,
  `security`) that decides which Security and tests-first steps run; when in doubt the higher tier, and
  Security or the Reviewer may raise it. Added: a compile-only skeleton step before tests-first, an
  explicit "no change" outcome for issues, documentation updates in the plan, a post-PR step for CI and
  SonarQube Cloud failures, and the orchestrator creating follow-up issues. Commits happen after every
  step; PRs are squash-merged (decision record 0010). Reason: the full pipeline was too heavy for small
  issues, and the first review found gaps that would have stalled a real run.
- 2026-10-01 — The Lead records the reasoning behind code decisions as decision records in
  `docs/decisions/` (one file per decision, append-only, committed with the change). Reason: the
  reasoning must stay available months later, independent of PRs, issues and sessions, without bloating
  `docs/ARCHITECTURE.md`.
- 2026-10-01 — The Style Manager role becomes the Code Officer: the only member that runs
  `reihitsu-format` and owns zero `RH####` / `S####` diagnostics. Dev and Tester own at least 80 % line
  coverage on new/changed code (`.squad/tools/coverage-check.py`). Reason: quality gates move before the
  pull request; see decision record 0009 for the repository-level part (local Sonar rules, no CI format
  check).
- 2026-10-02 — Lessons from the first squad run (issue #78): the Code Officer's gate is now
  `.squad/tools/analyzer-check.py` (all Roslyn diagnostics incl. info-level MSTest rules, decision record
  0011); Dev and Tester run it before handing over, and the Tester follows the MSTest conventions in
  `docs/UNIT_TESTS.md`. The Lead names the owner of every fix it decides on by file (tests → Tester), and
  only the orchestrator writes `log.md`. Reason: four MSTEST0049 issues reached SonarQube Cloud
  unnoticed, a Lead decision assigned a test fix to the Dev, and log rows merged.
- 2026-10-02 — The reviewer subagent is renamed from `plextojellyfinsync-reviewer` to `squad-reviewer`.
  Reason: it is a squad member like the others; `create-pr` and `review-pr` keep using the same agent.
