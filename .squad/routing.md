# Routing

The pipeline is the same for issues and features; only the input differs (a GitHub issue for
`squad-issue`, a feature idea plus `spec.md` for `squad-spec`). The orchestrator runs the steps, passes
documents between members (subagents cannot talk to each other directly) and records every step in the
work folder's `log.md`.

## Work folder

- Issue: `specs/issue-<number>/` — `plan.md`, `log.md`
- Feature: `specs/<NNN-name>/` — `spec.md`, `plan.md`, `tasks.md`, `log.md`
- Decision records (both): `docs/decisions/NNNN-title.md` — the lasting *why*, written by the Lead
  (`specs/` holds the working record of one change; `docs/decisions/` is what a reader months later
  looks at)

## Pipeline

| # | Step                    | Owner          | Output / exit condition                                           |
| - | ----------------------- | -------------- | ----------------------------------------------------------------- |
| 1 | Plan                    | Lead           | `plan.md` (feature: `spec.md` first, then `plan.md` + `tasks.md`), decision records as `Proposed` |
| 2 | Plan security review    | Security       | `APPROVED` → step 3; `CHANGES_REQUIRED` → back to step 1          |
| 3 | Tests first             | Tester         | tests for the acceptance criteria; must **fail** on the current code (or justify why they cannot) |
| 4 | Implementation          | Dev            | production code until the step-3 tests and the full suite are green |
| 5 | Style pass              | Style Manager  | style-only edits; same tests green, zero `RH####` diagnostics     |
| 6 | Review                  | Reviewer + Security | no blocking findings → step 7; blocking → back to Dev (step 4) |
| 7 | PR approval             | Lead           | decision records `Accepted` and indexed; `APPROVED` → step 8; otherwise Lead decides (see escalation) |
| 8 | Pull request            | Dev (via orchestrator) | commit, push, open PR                                     |

## Loop limits

- **Plan ↔ Security (steps 1–2):** at most 2 rejections. After the 2nd `CHANGES_REQUIRED` the Lead
  decides: narrow the scope, split into separate issues, or escalate to the Product Manager.
- **Review ↔ Dev (steps 4–6):** review pass 1 is a full review; at most **2 further fix-and-review
  rounds**, each reviewing only the delta. Blocking findings still open after that go to the Lead, who
  decides: accept with justification, split into a follow-up issue, abort, or escalate.
- **Dev ↔ Tester disagreements:** if the Dev believes a step-3 test is wrong, the Lead decides (the
  test is not changed silently). This does not count against a loop limit.
- **Style pass breaks something** (build, tests, or a structural change): the style edit is reverted and
  the Dev fixes it; the Style Manager never changes behavior.

## Escalation to the Product Manager

The Lead escalates only when it cannot decide responsibly on its own: the requirement is ambiguous, the
fix needs a product decision (behavior change visible to users, breaking a documented guarantee in
`docs/ARCHITECTURE.md`), or a loop limit was hit and none of the Lead's options is clearly right. The
escalation is one concise question with the options and the Lead's recommendation. Creating new GitHub
issues as a Lead decision is allowed; they are listed in the PR under Next Steps.

## Non-blocking findings

Fixed in the same change or opened as a linked GitHub issue now (Lead decides which) — never deferred to
"a later change".
