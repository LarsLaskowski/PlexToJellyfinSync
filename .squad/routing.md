# Routing

The pipeline is the same for issues and features; only the input differs (a GitHub issue for
`squad-issue`, a feature idea plus `spec.md` for `squad-spec`). The orchestrator — the session that runs
the skill — launches the members, passes documents between them (subagents cannot talk to each other
directly), performs every Git and GitHub action (including follow-up issues the Lead decides on) and
records every step in the work folder's `log.md` (after step 10, in the "Squad working record" comment
that replaces it). Step numbers below are the ones the skills use.

## Work folder

- Issue: `specs/issue-<number>/` — `plan.md`, `log.md`
- Feature: `specs/feature-<short-slug>/` — `spec.md`, `plan.md`, `tasks.md`, `log.md`
- The work folder lives **only on the work branch**: it is committed and pushed after every step so a
  crashed session can resume, and in step 10 its content is posted as a "Squad working record" comment
  (on the issue, or on the PR for a feature without issue) and the folder is removed before the PR opens.
  `main` never contains `specs/` working records.
- Decision records (both): `docs/decisions/NNNN-title.md` — the lasting *why*, written by the Lead; this
  is what a reader months later looks at.

## Scope of a product PR

An issue or feature PR changes the product and its documentation only. It never touches the squad or the
agent instructions: `.squad/` (charters, `history.md`, `decisions.md`, tools), `.claude/`,
`.github/skills/`, `CLAUDE.md`, `AGENTS.md`, `.github/copilot-instructions.md`. Lessons about the squad
are collected in step 12 as a GitHub issue labelled `squad` and implemented in a separate
squad-maintenance PR, which runs `.squad/tools/config-check.py`. The Reviewer reports any such file in a
product PR as a blocking finding.

## Tiers

In step 2 the Lead classifies the change and justifies the tier in `plan.md` (for `docs`, in its result). When in doubt, the higher
tier applies; Security or the Reviewer may raise the tier at any point (never lower it).

| Tier | When | Pipeline |
| ---- | ---- | -------- |
| `docs` | Only Markdown documentation (`README.md`, `docs/`, `SECURITY.md`), issue/PR templates or code comments change — no file that is compiled, built, tested or executed (`src/`/`tests/` code, `.csproj`/props, `Dockerfile`, workflows, scripts, `appsettings*.json`) | Lead plans briefly (no `plan.md`: tier, change list and acceptance criteria go into its result and the first `log.md` row); steps 3–7 and 9 skipped; the Dev makes the edits; the orchestrator verifies read-only (`reihitsu-format --check ./`, and the build only if a code comment changed); one Reviewer round (a delta round only after a blocking fix); the orchestrator opens the PR once the review is clean. A decision record only for a real decision, never for a wording fix. |
| `trivial` | Documentation, comments, log or UI wording, configuration defaults — no change to behavior or control flow | Steps 3, 4 and 5 skipped (no Security, no tests-first); code check, Reviewer and Lead approval still run. Tests and coverage are still required if production code changes. |
| `standard` | A behavior change that touches none of the security areas below | Step 3 skipped; Security reviews only the diff (step 8) |
| `security` | Touches tokens/secrets, dashboard auth (`TokenAuthMiddleware`, login, sessions, cookies), path mapping or any file write, `.nfo`/XML parsing, HTTP calls to Plex, logging of external data, Docker/CI configuration, or adds/updates a dependency | Full pipeline |

## Pipeline

| # | Step | Owner | Exit condition |
| - | ---- | ----- | -------------- |
| 1 | Intake | Orchestrator | Branch off `main`, work folder and `log.md` created |
| 2 | Plan | Lead | `plan.md` with tier, acceptance criteria, signatures of new/changed API, doc updates; decision records `Proposed`. Or outcome **no change** (see below) |
| 3 | Plan security review | Security | `APPROVED` → 4; `CHANGES_REQUIRED` → Lead revises, back to 3 (`security` tier only) |
| 4 | Skeleton | Dev | Only when the plan adds or changes API: compile-only signatures (bodies throw `NotImplementedException`), solution builds |
| 5 | Tests first | Tester | Tests for every acceptance criterion; they compile and **fail** on the current code |
| 6 | Implementation + coverage | Dev, Tester | All tests green; ≥ 80 % line coverage on new/changed code and overall (`.squad/tools/coverage-check.py`); doc updates from the plan done |
| 7 | Code check | Code Officer | `reihitsu-format`; `.squad/tools/analyzer-check.py` passes (no diagnostic of any severity in changed files); same tests green; no structural change |
| 8 | Review | Reviewer + Security | No blocking findings → 9; blocking → owner fixes (Dev: code, Tester: tests), back to 6, then a mandatory delta round (Security only for `standard`/`security`) |
| 9 | PR approval | Lead | Latest review round without a blocking finding not covered by a recorded Lead decision, and covering every change to `src/`/`tests/`/`docs/` except `specs/` bookkeeping and the Lead's own approval edits (record status, index, `docs/ARCHITECTURE.md` link); plan fulfilled, coverage met, decision records `Accepted` and indexed → `APPROVED` → 10 |
| 10 | Pull request | Dev (via orchestrator) | Working record posted as comment, `specs/<folder>/` removed, PR opened (merged later with *Squash and merge*) |
| 11 | After the PR | Dev, Code Officer, Reviewer | CI green, SonarQube Cloud quality gate passed, review comments worked |
| 12 | Wrap-up | Orchestrator | Squad lessons filed as one `squad` issue (or "no lessons" logged), user informed |

Commits and pushes to the work branch happen after every completed step from step 2 on, once the plan
returned `RESULT: DONE` (step 1 and a `NO CHANGE` outcome leave nothing to commit); with *Squash and merge* only
the PR title and description reach `main`, so intermediate commits may describe the step. They still
never contain secrets and never mention an AI assistant.

## Concurrency

Only one member that builds or runs tests may work at a time: concurrent `dotnet build` / `dotnet test`
runs share `bin/` and `obj/` and break each other (`.squad/tools/analyzer-check.py` serializes itself with
a lock, plain builds do not). In step 8, `squad-reviewer` and `squad-security` may run together because
both are read-only and the reviewer builds in a scratch copy. No member experiments (mutation tests,
trial edits) in the repository working tree — use a scratch `git worktree` instead.

## Outcome "no change"

If the Lead concludes in step 2 that no code change is needed — duplicate, cannot be reproduced, works as
designed (e.g. covered by an accepted decision record), or out of scope — it returns
`RESULT: NO CHANGE` with a proposed issue comment. The orchestrator shows the comment to the Product
Manager and posts it only after confirmation (a public statement on the issue). The work folder is not
committed and no PR is opened.

## Loop limits

- **Plan ↔ Security (steps 2–3):** at most 2 rejections. After the 2nd `CHANGES_REQUIRED` the Lead
  decides: narrow the scope, split into separate issues, or escalate to the Product Manager.
- **Review ↔ Dev (steps 6–8):** review pass 1 is a full review; at most **2 further fix-and-review
  rounds**, each reviewing only the delta. Blocking findings still open after that go to the Lead, who
  decides: accept with justification, split into a follow-up issue, abort, or escalate.
- **Dev ↔ Tester disagreements:** if the Dev believes a step-5 test is wrong, the Lead decides (the test
  is not changed silently). Not counted against a loop limit.
- **Code check needs a structural change** (or breaks build/tests): the Code Officer's edit is reverted
  and the item goes to the Dev (or Tester for tests), then the code check runs again. Not counted
  against a loop limit.
- **Coverage below 80 %:** Dev and Tester iterate; lines that cannot be covered by a unit test go to the
  Lead, whose decision is recorded in `log.md`.
- **After the PR (step 11):** CI or quality-gate failures and review comments are fixed on the same
  branch and go through steps 7–8 again (delta review). The same limit of 2 fix rounds applies per
  failure; after that the Lead decides.

## Escalation to the Product Manager

The Lead escalates only when it cannot decide responsibly on its own: the requirement is ambiguous, the
fix needs a product decision (behavior change visible to users, breaking a documented guarantee in
`docs/ARCHITECTURE.md` or an accepted decision record), or a loop limit was hit and none of the Lead's
options is clearly right. The escalation is one concise question with the options and the Lead's
recommendation. Follow-up GitHub issues the Lead decides on are created by the orchestrator and listed in
the PR under Next Steps.

## Non-blocking findings

Fixed in the same change or opened as a linked GitHub issue now (Lead decides which) — never deferred to
"a later change".
