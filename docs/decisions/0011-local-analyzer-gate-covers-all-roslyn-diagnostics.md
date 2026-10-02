# 0011: Local analyzer gate covers all Roslyn diagnostics

- **Status:** Accepted
- **Date:** 2026-10-02
- **Source:** First squad run (issue #78, PR #185); refines decision 0009
- **Supersedes:** —

## Context

Decision 0009 moved quality gates before the pull request and added `SonarAnalyzer.CSharp` to the local
build, assuming that a console build without `RH####` / `S####` warnings meant nothing new for SonarQube
Cloud. The first squad run disproved that: SonarQube Cloud reported four `MSTEST0049` issues in a new
test file that no local build had shown. SonarQube Cloud imports *every* Roslyn diagnostic from the
build's SARIF error log, including info-level ones such as the MSTest analyzer rules, which MSBuild never
prints as warnings. Grepping the console output also failed for a second reason: an incremental build
prints no warnings for projects it does not recompile.

## Options considered

1. **Raise the severity of individual MSTest rules in `.editorconfig`** — makes known rules visible, but
   every new analyzer rule or package needs another entry, and the next blind spot surfaces in CI again.
2. **Raise all analyzer diagnostics to warnings** — floods the build with IDE and style suggestions the
   project does not follow.
3. **Read the same data SonarQube Cloud reads** — build with an SARIF error log and report every
   non-suppressed diagnostic, at any severity, in changed files.

## Decision

Option 3. `.squad/tools/analyzer-check.py` runs a full, non-incremental Release build with an SARIF error
log per project and fails when a file changed since the merge base with `origin/main` carries any
diagnostic (`RH`, `S`, `MSTEST`, `CA`, …, info level included). The rule in `CLAUDE.md` changes from "no
`S####` in a changed file" to "no analyzer diagnostic of any severity in a changed file". The Code Officer
owns the check; Dev and Tester run it before handing over and fix findings that need a code or test
change themselves, and `docs/UNIT_TESTS.md` documents the MSTest rules that tests most often trip
(`TestContext.CancellationToken`, specific `Assert` members).

## Consequences

- The check reproduces the Roslyn diagnostics the build emits — MSTest, CA, Reihitsu and the default
  Sonar way rules of `SonarAnalyzer.CSharp`. SonarQube Cloud can still differ in two ways: its own C#
  quality profile (`csharpsquid` rules) is not the package default, so it reports some `S####` rules the
  local build does not (and ignores some the local build reports), and non-Roslyn checks (duplication,
  security hotspots, Python/shell rules) only run in CI. Findings that only appear there go back through
  the post-PR step of the squad pipeline.
- The check costs one extra full build per run.
- Pre-existing diagnostics in unchanged files are reported as a count only and do not gate a change; they
  are fixed when a change touches their file.
- The script takes no arguments, so no user-supplied value reaches the shell, git or the filesystem.
