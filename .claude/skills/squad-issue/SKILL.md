---
name: squad-issue
description: Use when the user asks to fix a specific GitHub issue in PlexToJellyfinSync. Runs the squad pipeline — Lead plans and picks a tier, Security reviews security-relevant plans, Tester writes failing tests first, Dev implements to 80% coverage, Code Officer clears format/Reihitsu/Sonar, Reviewer (+ Security) review, Lead approves — and opens a PR referencing the issue.
---

# Squad Issue

Fix a reported GitHub issue with the squad defined in `.squad/`. You are the **orchestrator**: you launch
the members as subagents, pass their outputs on (they cannot talk to each other), enforce the tiers and
loop limits from [`.squad/routing.md`](../../../.squad/routing.md), and perform every Git and GitHub
action yourself — including follow-up issues the Lead decides on.

- **Commits and pushes** to the work branch are always allowed (`CLAUDE.md`, golden rules): commit and
  push after every completed step once the plan returned `RESULT: DONE` (nothing is committed before). PRs are merged with *Squash and merge*, so only the PR title and
  description reach `main`; intermediate commit messages may name the step, but never contain secrets or
  mention an AI assistant. Never commit to `main`.
- **Pull request:** invoking this skill is the user's approval for opening the PR in step 10, once the
  Lead has approved it.
- **Product Manager:** the user is only contacted when the Lead returns `RESULT: ESCALATE` (relay the
  question verbatim with its options and wait) or for confirming a public issue comment on
  `RESULT: NO CHANGE`.
- Everything that ends up in the repository or on GitHub is written in **English**.

## Steps

1. **Intake.** Read the issue in full, including comments; note the reported environment (Plex and
   Jellyfin versions, image tag, host OS, volume layout). If it is closed, stop and report that. Start
   from a clean working tree on a new branch off the latest `main`, e.g.
   `fix-issue-<number>-<short-slug>` (or the branch the session prescribes). Create
   `specs/issue-<number>/log.md` from `specs/_template/log.md`; append one line per step.
2. **Plan.** Launch `squad-lead` in mode `plan` with the issue text and the work folder. It returns one of:
   - `RESULT: DONE` — `plan.md` with the **tier** (`trivial` / `standard` / `security`), acceptance
     criteria, the signatures of new or changed API, required documentation updates (`README.md`,
     `docs/`), and `Proposed` decision records. Continue with the steps the tier requires.
   - `RESULT: NO CHANGE` — show the proposed issue comment to the user, post it only after confirmation,
     discard the work folder, and stop. No PR.
   - `RESULT: ESCALATE` — ask the user, then relaunch the Lead with the answer.
3. **Plan security review** (`security` tier only). Launch `squad-security` in mode `plan`. On
   `CHANGES_REQUIRED`, launch `squad-lead` in mode `revise` and repeat. After the **2nd** rejection launch
   `squad-lead` in mode `decide` (scope down, split into issues, abort, or escalate).
4. **Skeleton** (only if the plan adds or changes API). Launch `squad-dev` in mode `skeleton`: the planned
   signatures with bodies that throw `NotImplementedException`, so the tests of step 5 compile.
5. **Tests first** (skipped for `trivial`). Launch `squad-tester` in mode `tests-first`. Confirm yourself
   that the new tests compile and fail on the current code (unless the Tester justified why one cannot).
   A fix without a reproducing test is only acceptable when the bug genuinely needs a live Plex/Jellyfin
   instance — then the PR says so.
6. **Implement and cover.** Launch `squad-dev` in mode `implement` with the plan and the test names; it
   also makes the documentation updates the plan lists. If the Dev disputes a test, launch `squad-lead`
   in mode `decide`; the Tester changes a test only if the Lead says so. Then launch `squad-tester` in
   mode `coverage`; repeat Dev/Tester until `python3 .squad/tools/coverage-check.py` (after `dotnet test … --collect:"XPlat Code Coverage" --results-directory ./TestResults`)
   passes (≥ 80 % on new/changed production code and overall). Lines reported as not unit-testable go to
   `squad-lead` in mode `decide`; an accepted gap is recorded in `log.md`.
7. **Code check.** Launch `squad-code-officer` with the base ref — the only member that runs
   `reihitsu-format` and fixes `RH####` / `S####` diagnostics. Then verify yourself, without formatting:
   `reihitsu-format --check ./` exits 0, `dotnet build PlexToJellyfinSync.slnx -c Release --no-restore` shows zero `RH####` and no `S####` in a
   changed file, `dotnet test PlexToJellyfinSync.slnx -c Release --no-build` is green with the same
   tests, and the coverage check still passes. Structural items handed back go to `squad-dev` (or
   `squad-tester`), followed by another code check. CI does not check formatting; this is the only gate.
8. **Review.** Launch `plextojellyfinsync-reviewer` (round 1, full) and — for `standard` and `security` —
   `squad-security` in mode `diff`, in parallel, against the base ref. Pass both the work folder
   (`specs/<folder>/`) so they check the plan's acceptance criteria and tier; either may raise the tier. Blocking
   findings → `squad-dev` fixes them → steps 6 (coverage) and 7 again → next round reviews only the
   delta. At most **2 fix rounds** after round 1; then `squad-lead` in mode `decide`. Non-blocking
   findings: the Lead decides per finding — fix now, or you open a linked GitHub issue now.
9. **PR approval.** Launch `squad-lead` in mode `approve-pr` with the base ref, the build/test/coverage
   output and the review outcome. `NOT APPROVED` → back to step 6 or 8 (counting against the review loop
   limit) or let the Lead decide/escalate. On `APPROVED`, the decision records are `Accepted` and indexed
   in `docs/decisions/README.md`.
10. **Pull request** (Dev role, performed by you). Push, then open the PR from
    [`.github/pull_request_template.md`](../../../.github/pull_request_template.md): title per
    `docs/CONTRIBUTING.md` (`[area] Description`, it becomes the squash commit subject on `main`), body
    describing the bug, the fix and the reproducing test, `Closes #<number>` under Issues, follow-up issues
    under Next Steps, links to the decision records. Follow the `create-pr` skill's template rules, but
    do **not** run its internal review loop — step 8 replaced it. If the fix is not fully verifiable
    without a real Plex/Jellyfin instance, say so.
11. **After the PR.** Stay with the PR until CI is green and the SonarQube Cloud quality gate passes:
    - SonarQube Cloud findings → `squad-code-officer` (structural ones → `squad-dev`);
    - failing build or tests → `squad-dev` (test defects → `squad-tester`);
    - review comments (human, automated, `review-pr`) → `squad-dev`, worked in this PR, blocking or not.

    Each fix goes through steps 7–8 again (delta review), with at most 2 fix rounds per failure before the
    Lead decides. Never skip, disable or weaken a test to get green.
12. **Wrap-up.** Add lessons learned to the relevant `.squad/agents/*/history.md` and squad-process
    decisions to `.squad/decisions.md` (code decisions live only in `docs/decisions/`), commit and push
    them to the PR branch, and report the branch, the PR URL, the tier and any escalation or Lead
    decision to the user.

## What the pull request says — and what it doesn't

The PR title and description document the change, not how it was produced: the bug, the fix and the test
that pins it down. Plan revisions, review rounds and their findings never appear there.
(`specs/issue-<number>/` stays in the repository as the working record; the lasting reasoning is in
`docs/decisions/`.)
- Prefer non-interactive commands only. If push or PR creation fails, stop and report it.
- Never close the issue manually; `Closes #<number>` closes it on merge.
