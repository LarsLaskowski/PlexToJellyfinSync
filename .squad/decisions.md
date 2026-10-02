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
- 2026-10-02 — Lessons from the second squad run (issue #79): the orchestrator never does a member's
  work (no edits to src/tests, no formatting, no analyzer fixes); members that build or test never run in
  parallel and no member experiments in the working tree (`.squad/routing.md`, *Concurrency*); a delta
  review after a blocking fix is mandatory and the Lead refuses approval without it; guards added for an
  analyzer rule count as structural (Dev, not Code Officer); after the PR the session subscribes to it and
  the wrap-up is never skipped; PR titles use the fixed area list and Next Steps only lists linked issues.
  Reason: each of these happened in the issue #79 run (PR #187); the asymmetry it left unlinked is #188.
- 2026-10-02 — Lessons from the third squad run (issue #188): the Code Officer agent was not loaded
  because an unquoted ": " in its front matter broke the YAML; `.squad/tools/config-check.py` now checks
  agent and skill front matter and the skill mirrors. Product PRs never change the squad; lessons go into
  a `squad` issue for a separate maintenance PR, and working records stay off `main` (decision record
  0014). Reason: PR #190 carried a squad history edit, and per-issue logs were accumulating on `main`.
- 2026-10-02 — New tier `docs` for issues that only touch product Markdown documentation or issue/PR
  templates (no code comments, no decision record, never a feature): no plan.md, no Security, tests, Code
  Officer or Lead approval, one review round that checks the diff against the first log row. A Lead correction that resolves a blocking finding needs a delta round, even
  in its own decision record. Lead hint: a record about a removed string says a search "finds only this
  record". Reason: the issue #92 run (PR #192, lessons in #193) spent six agent runs on a three-line doc
  fix and approved a blocking fix without re-review. The suggested CRLF rule from #193 was not adopted:
  Markdown and `specs/` files are stored with LF in the index and `.gitattributes` normalizes new or
  changed ones on commit, so the Write tool's LF output is harmless there (a few CI and props files are
  still stored with CRLF and are not affected by this decision).
- 2026-10-02 — Lessons from the issue #31 run (#196): the Lead checks every factual claim of an issue
  against the code before planning, and the plan names test files strictly `{TypeUnderTest}Tests.cs`.
  `specs/<folder>/log.md` is committed right after intake; on `NO CHANGE` the folder is removed with a
  commit and the branch stays without a PR. The skill points to the GitHub MCP tools (`gh` only works as
  `gh api repos/...`). The rule that commits and PRs never mention an AI assistant is dropped from all
  instruction files and skills: attribution lines are acceptable now (Product Manager decision), so the
  repo rule no longer conflicts with the attribution the session adds. Reason: each of these caused
  friction or a fix round in PR #195.
- 2026-10-02 — New read-only role Devil's Advocate (`squad-devils-advocate`, Opus): for the tiers
  `standard` and `security` it challenges the plan once in step 2, before Security — assumptions checked
  against the code, need (no change?), simpler alternatives, scope, tier and acceptance criteria. No veto
  and no second round: the Lead answers every objection in the plan's *Challenge* section and decides.
  Reason: in the issue #31 run the plan adopted the issue's claims unchecked; the Reviewer only sees the
  finished diff, and Security sees the plan only for the `security` tier and only from the security
  angle, so a wrong plan was caught late. Not used for `docs`/`trivial`, to keep those
  tiers light; it never reviews code, so it does not duplicate the Reviewer or Security.
