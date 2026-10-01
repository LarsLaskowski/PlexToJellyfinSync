---
name: squad-spec
description: Use when the user wants to develop a new feature in PlexToJellyfinSync spec-driven with the squad. Lead writes spec, plan and tasks, Security reviews them, Tester writes failing tests first, Dev implements to 80% coverage, Code Officer clears format/Reihitsu/Sonar, Reviewer + Security review, Lead approves, then a PR is opened.
---

# Squad Spec

Build a feature with the squad defined in `.squad/`. The pipeline, loop limits, escalation rules and
orchestrator role are identical to the `squad-issue` skill; only the input and the planning documents
differ. Follow `squad-issue` steps 2–10 with these changes:

- **Work folder:** the next free `specs/<NNN-short-name>/` (three-digit number). Create `log.md` from
  `specs/_template/log.md`.
- **Branch:** `feature-<NNN>-<short-slug>` off the latest default branch (or the branch the session
  prescribes).
- **Plan (step 2):** `squad-lead` in mode `plan` writes `spec.md` (behavior, acceptance criteria, out of
  scope), `plan.md` and `tasks.md`. A feature is more likely than a bug fix to need a product decision —
  the Lead escalates whenever the user's request does not settle user-visible behavior.
- **Decision records:** features usually involve real design choices, so expect at least one record in
  `docs/decisions/`; the Lead also updates `docs/ARCHITECTURE.md` when the feature changes a flow or
  guarantee.
- **Security (step 3)** reviews `spec.md` and `plan.md` together.
- **Tests first / implement (steps 4–5)** run per task or group of tasks from `tasks.md`; tick tasks off
  as they are done. Run Tester/Dev for independent tasks in parallel only when they touch different files.
- **Pull request (step 9):** reference the spec folder instead of `Closes #<number>` (use `Closes #<n>`
  only if a feature request issue exists).

Commits and pushes to the work branch are always allowed (see `CLAUDE.md`, golden rules); invoking this
skill is the user's approval for opening the pull request once the Lead has approved it. The user is the Product Manager and is only asked when the Lead escalates.
