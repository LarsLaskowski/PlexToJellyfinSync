# Review Session 7: Tests (MSTest)

Perform a code review of the 6 files listed below from the `PlexToJellyfinSync.Tests` project.
You only analyze — **do not change any production or test code**. The only file you may edit is
`docs/review/results/00-report.md`.

## Preparation

1. Read `docs/review/00-process.md` in full (criteria catalog A–E, severity levels,
   finding convention).
2. Read `.claude/CLAUDE.md`, especially the **Testing** section (naming scheme,
   assert messages, no FluentAssertions).
3. Context: The classes under test live in `src/PlexToJellyfinSync.Service/` and
   `src/PlexToJellyfinSync.Data/`. Read them as needed.

## Files to Review (6 — read and assess each one)

Review depth **deep** (criteria A–D), `.csproj` **quick** (criterion E + CPM check):

1. `tests/PlexToJellyfinSync.Tests/MSTestSettings.cs`
2. `tests/PlexToJellyfinSync.Tests/NfoWriterTests.cs`
3. `tests/PlexToJellyfinSync.Tests/PathMapperTests.cs`
4. `tests/PlexToJellyfinSync.Tests/PlexMetadataDeserializationTests.cs`
5. `tests/PlexToJellyfinSync.Tests/WatchAggregatorTests.cs`
6. `tests/PlexToJellyfinSync.Tests/PlexToJellyfinSync.Tests.csproj`

## Focus Areas for This Session

- **B (Test conventions)**: Class names `{Feature}Tests`; method names
  `{Class}{Scenario}{ExpectedResult}` in PascalCase **without underscores**; every assertion
  with an assert message; MSTest only (no FluentAssertions); `#region` blocks and XML docs
  in test classes too.
- **A (Test quality)**: Do the tests test the right behavior or only
  implementation details? Cleanup of temp files (`NfoWriterTests` presumably writes
  to disk — `TestCleanup`?), deterministic tests (no time-zone/culture dependency).
- **D (Coverage gaps)**: Document as findings which service classes have **no** tests
  (e.g. `SyncOrchestrator`, `StateStore`, `PlexClient`, `SyncStatusService`,
  logging classes) and which edge cases are missing in existing test classes.
  **Do not write new tests** — only document.
- `.csproj`: no package versions (CPM), correct project references.

## Record Results

1. Record each finding under `## Findings → ### Session 7 — Tests` in
   `docs/review/results/00-report.md`. Finding IDs: `F-701`, `F-702`, …
   Format per the process doc (file+line, criterion, severity, description, recommendation).
2. In the file checklist, set the status of all 6 files of this session to ✅ and enter the
   associated finding IDs in the "Findings" column ("none" if there are no findings).
3. Remove the "_Not yet performed._" placeholder under the session heading.

## Wrap-up

1. Self-check: Have all 6 files in the list above been read, assessed, and marked ✅ in the
   checklist? If not, follow up.
2. Show me a short summary of the findings.
3. Ask me for confirmation and only then commit
   (commit message: `Review Session 7: Tests`) and push the current branch.
