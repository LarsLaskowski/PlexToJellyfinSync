# 0009: Quality gates before the pull request

- **Status:** Accepted
- **Date:** 2026-10-01
- **Source:** Product Manager decision while setting up the squad workflow (`.squad/`)
- **Supersedes:** —

## Context

SonarQube issues only became visible in the SonarQube Cloud analysis of the pull request build, i.e. after
the push, which costs an extra fix-and-push round per finding. Formatting was checked twice: by the author
before the push and again by a `reihitsu-format --check` step in CI. The squad workflow (decision log in
`.squad/decisions.md`) introduces a Code Officer whose sole job is to leave nothing of that kind behind.

## Options considered

1. **Keep everything in CI** — a safety net for every contributor, but Sonar findings keep arriving late
   and every formatting slip costs a CI round.
2. **Check locally and in CI** — earliest feedback plus a safety net; formatting is checked twice.
3. **Check locally, drop the CI format check** — feedback before the push, one owner for formatting;
   nothing in CI catches an unformatted push.

## Decision

- `SonarAnalyzer.CSharp` is added as a dev-only analyzer to every project via `Directory.Build.props`
  (version in `Directory.Packages.props`), so the Sonar C# rules run in every local build.
- The `reihitsu-format --check` step is removed from `.github/workflows/ci.yml` (option 3). Formatting,
  zero `RH####` diagnostics and no `S####` diagnostic in a changed file are required *before* a push; in
  the squad skills only the Code Officer runs `reihitsu-format` and fixes these diagnostics.
- New or changed production code needs at least 80 % line coverage, and overall coverage at least 80 %,
  checked locally with `.squad/tools/coverage-check.py` (same measure as SonarQube's "coverage on new
  code").
- The SonarQube Cloud analysis in CI stays: it remains the system of record for the quality gate.

## Consequences

- Sonar C# rule violations appear while coding; the Code Officer clears them before the review.
- A push that skips the local gate (outside the squad, or a contributor who does not run the formatter) is
  no longer stopped by CI on formatting. The PR template checklist and `create-pr` skill still require
  `reihitsu-format`; reinstating the CI step is the remedy if unformatted code starts reaching `main`.
- The local analyzer uses the default Sonar way rules of the package; the SonarQube Cloud quality profile
  may differ, and checks that are not Roslyn rules (duplication, security hotspots, taint analysis) still
  only run in CI.
- When the analyzer was introduced the existing code had five `S####` warnings (S3267 in `PlexClient`,
  S8969 in `SyncOrchestrator`, three S8949 in `NfoWriterTests`); they are left as they are and are fixed
  by the Code Officer when a change touches those files.
- At introduction, overall line coverage was 84.6 %, but the host project (`PlexToJellyfinSync`:
  `Program.cs`, `Worker`, Razor components) only 47.6 %. The 80 % rule applies to new or changed lines,
  so it does not force retroactive tests there; a new hosting line that cannot be unit-tested needs a
  Lead decision.
