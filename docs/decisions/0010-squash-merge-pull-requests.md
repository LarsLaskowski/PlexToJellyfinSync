# 0010: Squash-merge pull requests

- **Status:** Accepted
- **Date:** 2026-10-01
- **Source:** Product Manager decision during the squad workflow review (`.squad/routing.md`)
- **Supersedes:** —

## Context

`CONTRIBUTING.md` asked contributors to rebase their branch onto `main` and to avoid the merge button, so
every branch commit landed on `main`. The squad commits and pushes after every pipeline step to secure
its work, and the project rules forbid the internal process (plan revisions, review rounds) from appearing
in the history. Rewriting a pushed branch to tidy it up would need a force-push, which requires explicit
approval.

## Options considered

1. **Keep rebase merges, commit only at curated milestones** — linear, granular history on `main`; work
   between milestones is not secured, and review fixes have to be folded into earlier commits.
2. **Squash and merge** — one commit per PR on `main`, built from the PR title and description; branch
   commits can be frequent and unpolished; per-commit granularity inside a PR is lost on `main`.

## Decision

Option 2. Pull requests are merged with *Squash and merge*. The PR title (`[area] Description`) becomes
the commit subject on `main`, the description its body. Branches are kept current by merging `main` into
them instead of rebasing.

## Consequences

- Intermediate branch commits may describe pipeline steps; only the PR title and description must follow
  the "document the change, not how it was produced" rule.
- `git log` on `main` shows one commit per PR; finer history is available in the closed PR.
- The repository setting should allow only squash merging (Settings → General → Pull Requests) so the
  rule is enforced, not just documented.
