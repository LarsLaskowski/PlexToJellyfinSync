---
name: fix-issue
description: Use when the user asks to fix a specific GitHub issue in PlexToJellyfinSync. Reproduces the problem as a failing unit test, implements a minimal fix, reviews it with the plextojellyfinsync-reviewer subagent, and opens a PR referencing the issue.
---

# Fix Issue

Use this skill to resolve a reported GitHub issue in this repository.

All user-facing output you create — branch name, commit message, PR title and
body, code comments and UI text — is written in **English**, regardless of the
language the user wrote in.

## Steps

1. Read the issue in full, including comments — note the reported environment
   (Plex server version, Jellyfin version, container image tag, host OS and
   volume layout), since bugs in path mapping and NFO writing are often
   specific to a particular mount setup. If the issue is already closed, stop
   and report that instead of starting work. If it is ambiguous or could be
   solved in several materially different ways, ask one focused question
   before writing code.
2. **Create a branch** for the fix off the latest default branch, e.g.
   `fix-issue-<number>-<short-slug>`. Do not commit directly to the default
   branch. Start from a clean working tree — unrelated uncommitted changes
   get reported, not bundled into the fix.
3. **Reproduce** the problem locally, following
   [`UNIT_TESTS.md`](../../../docs/UNIT_TESTS.md)'s conventions (MSTest, flat
   layout in `tests/PlexToJellyfinSync.Tests/`,
   `{TypeUnderTest}{Scenario}{ExpectedResult}` naming, hand-written
   fakes/stubs instead of a mocking library, an assert message on every
   assertion):
   - For a parsing, mapping or aggregation bug (`PlexClient`, `PathMapper`,
     `WatchAggregator`, `NfoWriter`, `StateStore`), write a failing unit test
     built from the input the issue reports — the actual Plex path, the
     actual `.nfo` content, the actual history payload.
   - For a bug in the orchestration itself, drive the real `SyncOrchestrator`
     against `FakePlexClient` / `FakeStateStore` / `RecordingNfoWriter` /
     `StubPathMapper`, the way `SyncOrchestratorTests` already does.
   - For an issue that only manifests against a real Plex or Jellyfin
     instance, reproduce with the closest available fake or fixture and note
     in the PR that verification against real data is still needed.
   A fix without a reproducing test is not acceptable except in that genuine
   live-server case — unit tests are mandatory for this repository, see
   `UNIT_TESTS.md`.
4. Implement the **minimal** fix, following the conventions in
   [`CLAUDE.md`](../../../CLAUDE.md), and consult
   [`ARCHITECTURE.md`](../../../docs/ARCHITECTURE.md) before touching the sync
   pipeline, path mapping, NFO writing or the dashboard's auth model — several
   behaviors documented there (existing `.nfo` files are only ever touched in
   their watch fields, an unmapped path is always skipped rather than passed
   through) are deliberate guarantees, not incidental behavior to "fix" away.
   Do not refactor unrelated code or expand scope beyond what the issue
   describes. Wrap every type's members in `#region` blocks as you write the
   code, and add new NuGet packages only through Central Package Management
   (`Directory.Packages.props`).
5. Confirm the previously-failing test now passes, and run the full
   verification (`reihitsu-format ./`, `dotnet build PlexToJellyfinSync.slnx
   -c Release --no-restore` with zero `RH####` diagnostics, and
   `dotnet test PlexToJellyfinSync.slnx -c Release --no-build`) to check for
   regressions. For a tighter loop on a single test, use
   `dotnet test tests/PlexToJellyfinSync.Tests/PlexToJellyfinSync.Tests.csproj
   --filter "FullyQualifiedName~ClassName.MethodName"`.
6. **Commit** the fix with a subject line of at most 80 characters, not in
   the first person and without a trailing period, and a body of 3–5
   sentences explaining *why* the change was made if it is not obvious from
   the diff.
7. **Review the fix before it leaves this session**: run the internal review
   loop from the `create-pr` skill — one or more
   `plextojellyfinsync-reviewer` subagent passes (Opus, fresh context)
   against the local branch, fixing blocking findings and re-running the
   reviewer on the delta until a pass comes back clean, capped at three
   passes. A non-blocking finding is either fixed on the spot or opened as a
   GitHub issue now and linked under Next Steps; it is not iterated on. This
   is what keeps the review out of the pull request comments, so do not skip
   it and do not defer it to a separate review session.
8. **Push** the branch (`git push -u origin <branch-name>`) and **open a PR**
   referencing the issue (`Closes #<number>` under Issues), following the
   `create-pr` skill's verification and template steps — do not skip the
   push/PR-creation steps even if verification already ran in step 5.
9. If the fix is not fully verifiable without a real Plex or Jellyfin
   instance, say so explicitly in the PR description rather than claiming
   full verification.

## What the pull request says — and what it doesn't

As in `create-pr`, the pull request documents the change, not how it was
produced. Describe the bug, the fix and the test that pins it down. Do not
record the internal review loop — its pass count, its findings or the commits
that resolved them — anywhere in the PR.

## Findings that arrive after the push

If a review lands on the pull request after it is open — from a human
reviewer, from an automated code review, or from the `review-pr` skill — work
those findings in this session, in this pull request, per `review-pr`. A
posted finding is never carried forward to "the next change in this area":
there is no such change scheduled, and this session's context is gone once it
ends.

## Notes

- As with `create-pr`, do not add Claude/Anthropic/Copilot attribution to
  commits or PRs: no `Co-Authored-By: Claude ...` / `Claude-Session: ...`
  commit trailers, and no "Generated with Claude Code" line or session link in
  the PR body.
- Prefer non-interactive commands only.
- If push or PR creation fails, stop and report the failure clearly — do not
  continue as if it succeeded.
- Never close the issue manually; let `Closes #<number>` in the PR body do it
  on merge.