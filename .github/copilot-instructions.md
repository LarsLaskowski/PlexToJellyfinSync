# PlexToJellyfinSync Project Instructions

This file describes project-specific conventions and configuration for PlexToJellyfinSync.
Copilot and other AI assistants must follow these guidelines when working in this repository.
These rules mirror `CLAUDE.md` and `AGENTS.md`; keep all three in sync. This file is a summary;
the binding, detailed references are [`ARCHITECTURE.md`](../docs/ARCHITECTURE.md) (how the system is
put together and why), [`CONTRIBUTING.md`](../docs/CONTRIBUTING.md) (workflow, PR conventions,
versioning) and [`UNIT_TESTS.md`](../docs/UNIT_TESTS.md) (test conventions — **unit tests are
mandatory for new code**). Read those three documents before making a non-trivial change; when
this file and one of them appear to disagree, treat that as a sync bug to fix, not as license to
pick either one.

---

## Project purpose

A .NET 10 worker (with an ASP.NET / Blazor Server host) that cyclically reads the Plex watch state
and writes it into Jellyfin `.nfo` files. Polling only (no webhooks), single user (the Plex owner),
runs as a Docker container, and exposes a web dashboard for status and logs. See
[`ARCHITECTURE.md`](../docs/ARCHITECTURE.md) for how the sync pipeline, dashboard and deployment fit
together.

---

## Commit Messages

- The first line should be a one-line summary of no more than 80 characters
- Do not end the subject line with a period
- Do not write the text in the first person
- Keep the main body to a maximum of 3–5 sentences, depending on the number of changes

---

## Git Workflow

- **Commits and pushes are always allowed** without asking: commit finished work and push it to the
  current feature branch (creating that branch if needed), so nothing is lost when a session ends. This
  does **not** cover: pushing to or committing directly on `main`, force-pushing or otherwise rewriting
  published history, deleting branches, and creating tags (a `v*` tag triggers a release) — those still
  need explicit user approval.
- **Pull requests are only opened by the squad or by the user.** The `squad-issue` and `squad-spec`
  skills open a PR after the Lead's approval; outside the squad, a PR is opened only when the user
  explicitly asks for one (e.g. by running the `create-pr` skill). Never open a PR on your own initiative.

---

## Pull Requests

- Title and description are always written in **English**, regardless of the language used in the
  conversation.
- Never mention Claude, Anthropic, Copilot, or any other AI assistant in the PR title or description.
  Do not add "Generated with …", "Co-Authored-By: Claude …", session links, or similar attribution —
  the description only describes the change itself.

---

## Build, test, and format

Use the solution file at the repository root (`.slnx` format):

- Restore: `dotnet restore PlexToJellyfinSync.slnx`
- Format source: `reihitsu-format ./`
- Build: `dotnet build PlexToJellyfinSync.slnx -c Release --no-restore`
- Run all tests: `dotnet test PlexToJellyfinSync.slnx -c Release --no-build`
- Run one test project: `dotnet test tests/PlexToJellyfinSync.Tests/PlexToJellyfinSync.Tests.csproj -c Release --no-build`
- Run one test method: `dotnet test tests/PlexToJellyfinSync.Tests/PlexToJellyfinSync.Tests.csproj --filter "FullyQualifiedName~Namespace.ClassName.MethodName"`

Run `reihitsu-format ./` after source changes and before running a build. CI does **not** check
formatting, so it must be clean before a push; in the squad skills only the Code Officer runs it. The command is available as
a .NET tool and can be installed with `dotnet tool install -g Reihitsu.Cli --prerelease` if it is
missing; `--prerelease` keeps the CLI in sync with the prerelease **Reihitsu.Analyzer** pinned in
`Directory.Packages.props`.
Static analysis runs during build through the **Reihitsu.Analyzer** and the **SonarAnalyzer.CSharp** rules
(both added to every project), so SonarQube issues surface in the local build, not first in the CI
analysis. There is **no StyleCop.Analyzers**.

A build must finish with **zero Reihitsu (`RH####`) warnings and errors** and no SonarQube (`S####`)
diagnostic in a changed file. Treat every such diagnostic as a failure and fix it before considering the
work done — do not leave analyzer warnings behind (in the squad skills, the Code Officer owns this).

New or changed production code needs **at least 80 % line coverage**
(`.squad/tools/coverage-check.py`, see [`UNIT_TESTS.md`](../docs/UNIT_TESTS.md#code-coverage)).

---

## Project Structure & Configuration

- **Target Framework**: `net10.0`
- **Nullable Reference Types**: Always enabled (`<Nullable>enable</Nullable>`)
- **Implicit Usings**: Enabled (`<ImplicitUsings>enable</ImplicitUsings>`)
- **Documentation XML**: Enabled (`<GenerateDocumentationFile>true</GenerateDocumentationFile>`)
- **Central Package Management**: `Directory.Packages.props` with `<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>`
- **Code Analysis**: `Reihitsu.Analyzer` and `SonarAnalyzer.CSharp` as dev dependencies in every project (via `Directory.Build.props`)
- **Solution Format**: `.slnx` (XML-based)

### Multi-Project Architecture

| Project | Purpose |
|---|---|
| `PlexToJellyfinSync.Core` | Interfaces, enums, options, domain and view models |
| `PlexToJellyfinSync.Data` | Plex JSON DTOs |
| `PlexToJellyfinSync.Service` | PlexClient, NfoWriter, PathMapper, WatchAggregator, SyncOrchestrator, StateStore, status/log services |
| `PlexToJellyfinSync` | ASP.NET + Blazor Server host: `Program.cs`, `Worker`, dashboard components, token auth |
| `PlexToJellyfinSync.Tests` | MSTest unit tests |

---

## Testing

**Unit tests are mandatory for newly written code.** Full conventions — including the project's
hand-written fake/stub pattern (no mocking library) and a checklist to run before committing a new
test — are documented in [`UNIT_TESTS.md`](../docs/UNIT_TESTS.md). Summary:

- **Framework**: MSTest (`Microsoft.VisualStudio.TestTools.UnitTesting`)
- **Assertions**: MSTest `Assert` class only — **do NOT use FluentAssertions**
- **Mocking**: none — use real objects or a hand-written fake/stub implementing the relevant
  `Core.Abstractions` interface (see `FakePlexClient`, `StubPathMapper`, `RecordingNfoWriter`,
  etc. in `tests/PlexToJellyfinSync.Tests`), not NSubstitute/Moq
- **Test class naming**: `{Feature}Tests`
- **Test method naming**: `{Class}{Scenario}{ExpectedResult}` in PascalCase **without underscores**
  (e.g. `WatchAggregatorAllWatchedReturnsWatched`, not `WatchAggregator_AllWatched_ReturnsWatched`)
- **Assert messages** are always provided

---

## Related skills

Project-specific workflow skills live under `.claude/skills/`, mirrored identically under
`.github/skills/`:

- `create-pr` — verify (format, build, tests), review the change locally, then open a PR
  following [`pull_request_template.md`](pull_request_template.md).
- `squad-issue` — fix a GitHub issue with the squad: the Lead plans, Security reviews the plan, the
  Tester writes failing tests first, the Dev implements to ≥ 80 % coverage, the Code Officer clears
  format, Reihitsu and Sonar diagnostics, Reviewer
  and Security review the diff, the Lead approves, then a PR referencing the issue is opened.
- `squad-spec` — the same squad pipeline for a new feature, planned as `spec.md`, `plan.md` and
  `tasks.md` under `specs/`.
- `review-pr` — review an open pull request against this project's C#, analyzer, security and
  unit-test conventions, and post the findings with an explicit verdict.

Review runs as a subagent defined in `.claude/agents/plextojellyfinsync-reviewer.md` (read-only,
pinned to Opus, fresh context). `create-pr` and the squad skills call it *before* pushing, so a change
is reviewed while it is still local; `review-pr` calls the same agent for a pull request that is
already open. The review checklist, the integration-surface sweep, the blocking/non-blocking
severity model and the "round 1 is a full review, later rounds review only the delta" rule live in
that one file, so they are identical either way. An agent without subagent support follows the same
file inline.

The squad skills run a multi-role pipeline defined in [`.squad/`](../.squad/team.md) — Lead (plan, decisions,
PR approval), Security (plan and diff), Tester (tests first, coverage), Dev, Code Officer (format, Reihitsu, Sonar) and Reviewer — as
subagents under `.claude/agents/squad-*.md`, with the loop limits and escalation rules in
[`.squad/routing.md`](../.squad/routing.md). Their working records (`plan.md`, `log.md`, for features also
`spec.md` and `tasks.md`) live under `specs/`. The user acts as Product Manager and is only asked when
the Lead escalates.

The reasoning behind code decisions — why something was built the way it was — is recorded by the Lead
as one decision record per decision in [`docs/decisions/`](../docs/decisions/README.md) (append-only,
superseded rather than rewritten), not in `ARCHITECTURE.md`. Read the relevant records before changing
code they cover, and do not contradict an accepted record without superseding it.

Two rules these skills enforce that are easy to get wrong:

- **A pull request documents the change, not how it was produced.** The internal review loop — its
  pass count, its findings, the commits that resolved them — never appears in the PR title, body or
  commit messages.
- **A finding posted as a review comment gets worked in that pull request**, blocking or not. It is
  never deferred to "the next change that touches this code": no such change is scheduled, and the
  session holding the context to act on it will not exist later. If it really should not be fixed
  here, reply with the reason or open a linked issue now — then resolve the thread.

---

## Pull requests, contributing and architecture

Follow [`CONTRIBUTING.md`](../docs/CONTRIBUTING.md) for branch/PR naming (`[area] Description`), the PR
checklist in [`pull_request_template.md`](pull_request_template.md), and the stability policy.
Consult [`ARCHITECTURE.md`](../docs/ARCHITECTURE.md) before changing the sync pipeline, path mapping,
NFO writing, or the dashboard's auth model — several behaviors there (e.g. NFO files are only ever
touched in their watch fields, an unmapped path is always skipped rather than passed through) are
deliberate guarantees, not incidental behavior.

---

## Code style (summary)

Follow the shared C# style conventions:

- File-scoped namespaces; one top-level type per file
- Using directives outside the namespace, System group first
- Allman braces; braces always required; 4-space indentation; CRLF; no trailing newline
- `var` preferred; language keywords over BCL types; LINQ method syntax only
- Use `condition == false` instead of `!condition`; `is null` / `is not null`
- No primary constructors; constructor injection with private readonly `_camelCase` fields
- Wrap every type's members in `#region` blocks **as you write the code**, never only after an
  analyzer warning, and never leave a type un-regioned. Group by member kind (`Constants`, `Fields`,
  `Constructors`, `Properties`, `Events`, `Methods`, …). For a region that groups a class's interface
  implementation, name it after the interface (e.g. `#region IPathMapper`); the region description must
  **not** end with the word "implementation"
- XML documentation on public, internal and private members; documentation language English; no `<remarks>`
- In service/infrastructure/data code append `.ConfigureAwait(false)` to awaited tasks
