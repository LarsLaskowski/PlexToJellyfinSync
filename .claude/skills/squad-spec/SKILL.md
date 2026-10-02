---
name: squad-spec
description: Use when the user wants to develop a new feature in PlexToJellyfinSync spec-driven with the squad. Lead writes spec, plan and tasks and picks a tier, Security reviews security-relevant plans, Tester writes failing tests first, Dev implements to 80% coverage, Code Officer clears format/Reihitsu/Sonar, Reviewer (+ Security) review, Lead approves, then a PR is opened.
---

# Squad Spec

Build a feature with the squad defined in `.squad/`. Tiers, pipeline, loop limits, escalation rules,
commit/push rules and the orchestrator role are identical to the `squad-issue` skill — follow its steps
1–12 with these changes:

- **Step 1 — work folder and branch:** the next free `specs/<NNN-short-name>/` (three-digit number) with
  `log.md` from `specs/_template/log.md`; branch `feature-<NNN>-<short-slug>` off the latest `main` (or
  the branch the session prescribes).
- **Step 2 — plan:** `squad-lead` in mode `plan` writes `spec.md` (behavior, acceptance criteria, out of
  scope), `plan.md` and `tasks.md`. A feature rarely qualifies as `trivial`. It is more likely than a bug
  fix to need a product decision — the Lead escalates whenever the request does not settle user-visible
  behavior. `RESULT: NO CHANGE` means the feature already exists or contradicts an accepted decision; report
  that to the user instead of commenting on an issue.
- **Decision records:** features usually involve real design choices, so expect at least one record in
  `docs/decisions/`; the Lead also updates `docs/ARCHITECTURE.md` when the feature changes a flow or
  guarantee.
- **Step 3** reviews `spec.md` and `plan.md` together.
- **Steps 4–6** run per task or group of tasks from `tasks.md`; tick tasks off as they are done. Run
  Tester and Dev one after another, never in parallel (see *Concurrency* in `.squad/routing.md`).
- **Step 10 — pull request:** reference the spec folder; use `Closes #<n>` only if a feature request issue
  exists.
