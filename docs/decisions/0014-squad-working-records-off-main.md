# 0014: Squad working records stay off main, and product PRs never change the squad

- **Status:** Superseded by [0019](0019-squad-maintained-in-the-template-repository.md)
- **Date:** 2026-10-02
- **Source:** Review of the squad run for issue #188 (PR #190)
- **Supersedes:** —

## Context

The squad committed its working records (`specs/<folder>/plan.md`, `log.md`, for features also `spec.md`
and `tasks.md`) together with every change, and its wrap-up step edited `.squad/agents/*/history.md` and
`.squad/decisions.md` in the same pull request. After three runs, `main` carried a growing pile of
per-issue logs (the issue #188 record alone was about 170 lines), and product PRs mixed product changes
with changes to the squad's own rules, which then reached `main` without a review of their own. The
lasting *why* of a change is already recorded in `docs/decisions/`.

## Options considered

1. **Keep everything in the repository** — full traceability next to the code; `main` grows with
   working notes nobody reads again, and squad changes ride along unreviewed in product PRs.
2. **Never commit working records** — clean history; a crashed session loses the plan and log, and the
   squad's resume-after-crash guarantee is gone.
3. **Commit working records only on the work branch, publish them as a comment, keep squad changes in
   their own PRs.**

## Decision

Option 3. The work folder is committed and pushed after every step as before. In step 10 its content is
posted as a "Squad working record" comment (on the issue, or on the PR for a feature without issue) and
the folder is removed before the PR opens; with *Squash and merge* it never reaches `main`. A product PR
never touches `.squad/`, `.claude/`, `.github/skills/`, `CLAUDE.md`, `AGENTS.md` or
`.github/copilot-instructions.md`; squad lessons are filed as one GitHub issue labelled `squad` and worked
in a separate squad-maintenance PR, which runs `.squad/tools/config-check.py`. The existing working
records for issues #78, #79 and #188 were removed; a comment on each issue links them at the last commit
that contained them.

## Consequences

- `main` keeps no per-issue working records; the history of a run is found through its issue.
- Squad rules change only in PRs whose purpose is changing them, so each change gets its own review.
- Older decision records (0012, 0013) still name their `specs/` folder as source; the folders remain in
  the git history and their content is on the issue.
- Lessons are no longer applied automatically; someone has to pick up the `squad` issues.
