---
name: publish-pr
description: Creates a branch, commits the current changes, pushes the branch, opens a pull request, and switches back to main. Use this when asked to publish local changes as a pull request.
---

Use this skill when the user wants the current local changes published to GitHub as a pull request.

All user-facing output you create — branch name, commit message, PR title and body — must be written in
**English**, regardless of the language the user wrote in. Never mention Claude, Anthropic, Copilot, or
any other AI/assistant tooling in the PR title or body, and do not add any `Co-Authored-By` trailer,
"Generated with" footer, session link, or other note attributing the work to an AI.

Follow this workflow:

1. Inspect the repository state first with non-interactive Git commands:
   - confirm the current branch
   - review `git status --short --branch`
   - confirm the `origin` remote exists
2. If there are no relevant local changes to publish, stop and say so plainly.
3. Format and validate before committing:
   - run `reihitsu-format ./`
   - run `dotnet build PlexToJellyfinSync.slnx -c Release --no-restore` — the build must finish with
     **zero Reihitsu (`RH####`) warnings and errors**; treat every `RH` diagnostic as a failure and fix
     it before continuing
   - run `dotnet test PlexToJellyfinSync.slnx -c Release --no-build`
   - if any step fails, fix the cause before continuing — do not commit or push broken or unformatted code
4. Choose or confirm a branch name based on the change. If the user already provided one, use it.
   Otherwise derive a short kebab-case branch name from the work.
5. Create and switch to the branch from the current base branch.
6. Stage only the intended files for this task. Do not include unrelated changes.
7. Create a non-interactive Git commit with a concise, descriptive message that matches the repo's
   commit style.
8. Push the branch to `origin` and set upstream tracking.
9. Create a pull request with GitHub CLI:
   - base branch: `main`, unless the user explicitly requests a different base
   - title: concise English summary of the change
   - body: short English summary of what changed
   - do not add any attribution, "Generated with" footer, or other note referencing an AI/assistant
10. After the pull request is created, switch back to the `main` branch.
11. Report the branch name and pull request URL clearly.

Additional rules:

- Prefer non-interactive commands only.
- Do not amend existing commits unless the user explicitly asks.
- Do not include unrelated modified files in the commit.
- If push or PR creation fails, stop and report the failure clearly instead of continuing as if it succeeded.
- If switching back to `main` would discard or conflict with uncommitted work, stop and explain the blocker.
