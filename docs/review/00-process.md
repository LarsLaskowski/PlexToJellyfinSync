# Process: Full Repository Analysis of PlexToJellyfinSync

This document defines the process, criteria, and conventions for a complete review of all
**107 version-controlled files** in this repository. It is referenced by the session prompts under
`docs/review/prompts/`.

## Goal

Every version-controlled file is reviewed exactly once — none is skipped. C# and Razor files are
reviewed **in depth** (criteria A–D); non-code files (configs, workflows, docs, CSS, JSON) get a
**quick review** (criterion E, plus C for configs/workflows). All findings and the review status of
each file are recorded centrally in `docs/review/results/00-report.md`.

## Workflow

The review is split into **8 independently runnable sessions** along the data flow
(Core → Data → Service → Host → Tests → Infrastructure). Each session has its own standalone prompt
under `docs/review/prompts/` that is run manually in a fresh Claude Code session:

| Session | Prompt file | Scope | Files |
|---|---|---|---|
| 1 | `session-1-core.md` | Core: Abstractions, Enums, Models, Options | 24 |
| 2 | `session-2-data.md` | Data: Plex DTOs | 14 |
| 3 | `session-3-service-plex.md` | Service I: PlexClient, WatchAggregator, PathMapper | 4 |
| 4 | `session-4-service-sync.md` | Service II: NfoWriter, SyncOrchestrator, State, Status | 5 |
| 5 | `session-5-service-infra.md` | Service III: Logging, DI registration | 5 |
| 6 | `session-6-host.md` | Host: Program, Worker, Security, Blazor components | 22 |
| 7 | `session-7-tests.md` | Tests: MSTest classes | 6 |
| 8 | `session-8-infrastructure.md` | Infrastructure: Root, `.github/`, `.claude/` | 27 |

Total: **107 files** (24 + 14 + 4 + 5 + 5 + 22 + 6 + 27).

The sessions may be run in any order; the recommended order is 1 → 8, since later sessions benefit
from contextual knowledge of the layers beneath them.

## Criteria Catalog

### A — Correctness & Bugs

- Logic errors and edge cases: `null`, empty lists, missing or unexpected Plex fields,
  invalid/unmapped paths.
- Error handling: swallowed exceptions, overly broad `catch` blocks, missing error paths,
  behavior when the Plex server is unreachable.
- Async correctness: `.ConfigureAwait(false)` in service/data code, propagated
  `CancellationToken`, no `async void`, no blocking `.Result`/`.Wait()`.
- Resources: `IDisposable`/`await using`, HttpClient lifecycle, file handles when writing
  NFO/state.
- Concurrency: thread safety of state accessed in parallel by the Worker (writing) and
  Blazor circuits (reading) — StateStore, InMemoryLogStore, SyncStatusService.
- Data integrity: is the state file written atomically? Is existing NFO content preserved on write?

### B — Rule/Style Conformance (per `.claude/CLAUDE.md`)

- `#region` blocks around all members, grouped by member kind (`Constants`, `Fields`,
  `Constructors`, `Properties`, `Events`, `Methods`, …); interface regions named after the
  interface, description not ending in "implementation".
- XML documentation on all members, English, no `<remarks>`.
- `== false` instead of `!`, `is null` / `is not null`, `var` preferred, language keywords instead
  of BCL types, LINQ method syntax only.
- No primary constructors; constructor injection with `_camelCase` readonly fields.
- File-scoped namespaces, one top-level type per file, `using` outside the namespace
  (System first), Allman braces, 4 spaces, CRLF, no trailing newline.
- Package versions exclusively in `Directory.Packages.props` (Central Package Management),
  no version numbers in `.csproj` files.
- Tests: MSTest only, class names `{Feature}Tests`, method names
  `{Class}{Scenario}{ExpectedResult}` in PascalCase **without underscores**, every assertion with
  an assert message.

### C — Security

- Plex token: leaks in logs, display in the dashboard, transport (header vs. query string),
  token in error messages/exceptions.
- `TokenAuthMiddleware` / `LoginPage`: timing-safe comparison, cookie flags (HttpOnly,
  Secure, SameSite), bypass paths (static files, reconnect endpoints).
- Path safety: `PathMapper`/`NfoWriter` against path traversal; no writing outside the
  mapped library roots.
- No secrets in `appsettings*.json`, `launchSettings.json`, workflows, Dockerfile.
- Dockerfile: non-root user, base-image pinning; GitHub workflows: minimal `permissions:`,
  pinned actions (SHA or at least major version).

### D — Architecture & Design

- Layer separation upheld: Core ← Data ← Service ← Host, no backward references.
- Abstractions sensibly scoped; DI registration complete, lifetimes correct
  (Singleton vs. Scoped vs. Transient, especially in the Worker/Blazor interplay).
- Responsibilities: do large classes (e.g. `SyncOrchestrator`) do too much? Testability.
- Options pattern: validation at startup, `IOptionsMonitor` where reload would make sense.
- Document test gaps (which service classes are untested?) — do **not** write new tests as part of
  the review.

### E — Non-Code Files (Quick Review)

- Consistency: `.claude/CLAUDE.md` ↔ `.github/copilot-instructions.md` in sync?
  README ↔ actual options/configuration? `.editorconfig` ↔ documented style
  (CRLF, 4 spaces)?
- CI/release workflows: do they build and test what is documented
  (`PlexToJellyfinSync.slnx`, Release configuration)?
- Plausibility: `.gitignore`/`.dockerignore` appropriate, rulesets consistent with the
  Reihitsu rules, dependabot configuration sensible, issue templates current.

## Severity Levels

| Level | Meaning |
|---|---|
| 🔴 Critical | Data loss, security hole, sync writes wrong watch states |
| 🟠 High | Misbehavior in realistic scenarios, serious robustness gap |
| 🟡 Medium | Bug in edge cases, clear rule violations against CLAUDE.md |
| 🔵 Low | Minor style/consistency deviations, room for improvement |
| ⚪ Note | Observation without urgency, doc/consistency remark |

## Finding Convention

- Finding IDs: `F-{Session}{sequential no., two digits}`, e.g. `F-101` (Session 1, finding 1),
  `F-403` (Session 4, finding 3).
- Each finding contains: **file + line(s)**, **criterion** (A–E), **severity**,
  **description**, **recommendation**.
- The review is **purely analytical**: no production code is changed. The only permitted write
  operation is maintaining `docs/review/results/00-report.md`.

## Completeness Proof

After all 8 sessions are done, every one of the 107 files in the checklist in
`docs/review/results/00-report.md` must have status ✅. Final check:

```bash
git ls-files | sort > /tmp/expected.txt
# Extract paths from the checklist (the "File" column) and diff against /tmp/expected.txt
```

The diff must be empty. Files added to the repo after this framework was created
(including the review files under `docs/review/` themselves) are added to the checklist during the
final session's completeness check and either reviewed as well or explicitly marked as
"outside the review baseline".
