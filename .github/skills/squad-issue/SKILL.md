---
name: squad-issue
description: Use when the user asks to fix a specific GitHub issue in PlexToJellyfinSync. Runs the squad pipeline (Lead plans, Security reviews the plan, Tester writes failing tests first, Dev implements to 80% coverage, Code Officer clears format/Reihitsu/Sonar, Reviewer + Security review, Lead approves) and opens a PR referencing the issue.
---

# Squad Issue

Fix a reported GitHub issue with the squad defined in `.squad/`. You are the **orchestrator**: you launch
the members as subagents, pass their outputs on (they cannot talk to each other), enforce the loop limits
from `.squad/routing.md`, and run every Git and GitHub step yourself.

Invoking this skill is the user's approval for the commit, push and pull-request steps below (see
`CLAUDE.md`, golden rules). The user is the **Product Manager**: only contact them when the Lead returns
`RESULT: ESCALATE`, then relay the Lead's question verbatim with its options and wait.

Everything that ends up in the repository or on GitHub — branch, commit message, PR title and body, code
comments, files under `specs/` — is written in **English**.

## Steps

1. **Intake.** Read the issue in full, including comments; note the reported environment (Plex and
   Jellyfin versions, image tag, host OS, volume layout). If it is closed, stop and report that. Start from
   a clean working tree on a branch off the latest default branch, e.g.
   `fix-issue-<number>-<short-slug>` (or the branch the session prescribes). Create
   `specs/issue-<number>/log.md` from `specs/_template/log.md`; append one line per step below.
2. **Plan.** Launch `squad-lead` in mode `plan` with the issue text and the work folder; it drafts the
   plan and any decision records (`docs/decisions/`, status `Proposed`). If it escalates, ask the user and
   resume with the answer.
3. **Plan security review.** Launch `squad-security` in mode `plan`. On `CHANGES_REQUIRED`, launch
   `squad-lead` in mode `revise` with the verdict and repeat this step. After the **2nd** rejection launch
   `squad-lead` in mode `decide` instead (scope down, split into issues, abort, or escalate).
4. **Tests first.** Launch `squad-tester` with the approved plan. Confirm yourself that the new tests
   exist and fail on the current code (unless the Tester justified why one cannot). A fix without a
   reproducing test is only acceptable when the bug genuinely needs a live Plex/Jellyfin instance — then
   the PR says so.
5. **Implement and cover.** Launch `squad-dev` with the plan and the test names. If the Dev disputes a
   test, launch `squad-lead` in mode `decide`; the Tester changes the test only if the Lead says so. Then
   launch `squad-tester` in mode `coverage`; repeat Dev/Tester until
   `python3 .squad/tools/coverage-check.py <base-ref> <results-dir>` passes (≥ 80 % line coverage on
   new/changed production code and overall). Lines the Tester reports as not unit-testable go to
   `squad-lead` in mode `decide`; an accepted gap is recorded in `log.md`.
6. **Code check.** Launch `squad-code-officer` with the base ref — it is the only member that runs
   `reihitsu-format` and fixes `RH####` / `S####` diagnostics. Then verify yourself, without formatting:
   `dotnet build PlexToJellyfinSync.slnx -c Release --no-restore` shows zero `RH####` diagnostics and no
   `S####` diagnostic in a changed file, `dotnet test PlexToJellyfinSync.slnx -c Release --no-build` is
   green with the same tests, and the coverage check still passes. Structural items the Code Officer hands
   back go to `squad-dev` (or `squad-tester` for tests), followed by another code check. CI does not check
   formatting, so this step is the only gate.
7. **Review.** Launch `plextojellyfinsync-reviewer` (round 1, full) and `squad-security` in mode `diff`
   in parallel, both against the base ref. Blocking findings from either → `squad-dev` fixes them →
   coverage check and step 6 again → next round reviews only the delta. At most **2 fix rounds** after round
   1; if blocking findings remain, launch `squad-lead` in mode `decide`. Non-blocking findings: the Lead
   decides per finding — fix now, or open a linked GitHub issue now.
8. **PR approval.** Launch `squad-lead` in mode `approve-pr` with the base ref, the verification result
   (build, tests, coverage output) and the review outcome. `NOT APPROVED` → handle the reasons (back to step 5 or 7, counting against the
   review loop limit) or let the Lead decide/escalate. On `APPROVED`, the change's decision records are
   `Accepted` and listed in `docs/decisions/README.md`.
9. **Pull request** (Dev role, performed by you). Commit — subject ≤ 80 characters, no first person, no
   trailing period; body 3–5 sentences on *why*. Include `specs/issue-<number>/` and the decision records
   in the commit, and link each record from the PR description. Push with
   `git push -u origin <branch-name>` and open the PR from
   [`.github/pull_request_template.md`](../../../.github/pull_request_template.md) with
   `Closes #<number>` under Issues and any follow-up issues the Lead opened under Next Steps. Follow the
   `create-pr` skill's template and push rules, but do **not** run its internal review loop again — step
   7 replaced it. If the fix is not fully verifiable without a real Plex/Jellyfin instance, say so.
10. **Wrap-up.** Add lessons learned to the relevant `.squad/agents/*/history.md` and decisions about the
    squad process to `.squad/decisions.md` (commit them with the change); decisions about the code live
    only in `docs/decisions/`. Report the branch, the PR URL, and any
    escalation or Lead decision to the user.

## What the pull request says — and what it doesn't

The pull request documents the change, not how it was produced: describe the bug, the fix and the test
that pins it down. The squad's internal loops — plan revisions, review rounds, their findings — never
appear in the PR title, body or commit messages. (`specs/issue-<number>/` stays in the repository as the
working record; the lasting reasoning is in `docs/decisions/`.)

## Findings that arrive after the push

Review comments on the open PR (human, automated, or `review-pr`) are worked in this session and this PR,
blocking or not, by `squad-dev` and re-checked by the Reviewer; a finding is never deferred to "the next
change in this area".

## Notes

- Do not add Claude/Anthropic/Copilot attribution to commits or PRs: no `Co-Authored-By: Claude ...` /
  `Claude-Session: ...` trailers, no "Generated with Claude Code" line or session link in the PR body.
- Prefer non-interactive commands only. If push or PR creation fails, stop and report it.
- Never close the issue manually; `Closes #<number>` closes it on merge.
