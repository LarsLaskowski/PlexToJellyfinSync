# CLAUDE.md — PlexToJellyfinSync

Project guidance for Claude when working in this repository. These rules mirror
`.github/copilot-instructions.md` and `AGENTS.md`; keep all three in sync. This file is a summary;
the binding, detailed references are [`ARCHITECTURE.md`](docs/ARCHITECTURE.md) (how the system is put
together and why), [`CONTRIBUTING.md`](docs/CONTRIBUTING.md) (workflow, PR conventions, versioning) and
[`UNIT_TESTS.md`](docs/UNIT_TESTS.md) (test conventions — **unit tests are mandatory for new code**).
Read those three documents before making a non-trivial change; when this file and one of them
appear to disagree, treat that as a sync bug to fix, not as license to pick either one.

## What this project is

A .NET 10 worker with an ASP.NET / Blazor Server host that **cyclically reads the Plex watch state
and writes it into Jellyfin `.nfo` files**. Polling only (no webhooks), single user (the Plex owner),
runs as a Docker container, and exposes a web dashboard (status + live logs). See
[`ARCHITECTURE.md`](docs/ARCHITECTURE.md) for how the sync pipeline, dashboard and deployment fit
together.

## Golden rules

- **Never** run `git commit`, `git push`, create branches/tags, or any Git write operation without
  explicit user approval. Read-only Git (`status`, `diff`, `log`) is always fine.
- Run `reihitsu-format ./` after editing C# and before building.
- Add new C# packages via **Central Package Management** (`Directory.Packages.props`); do not put
  version numbers in individual `.csproj` files.
- Every C# project uses the **Reihitsu.Analyzer** (no StyleCop.Analyzers).
- A build must finish with **zero Reihitsu (`RH####`) warnings and errors**. Treat every `RH`
  diagnostic as a failure and fix it before considering the work done.
- Wrap every type's members in `#region` blocks **as you write the code** — never leave a type
  un-regioned and never add the regions only after an analyzer warning. Group by member kind
  (`Constants`, `Fields`, `Constructors`, `Properties`, `Events`, `Methods`, …). For a region that
  groups a class's interface implementation, name it after the interface (e.g. `#region IPathMapper`);
  the region description must **not** end with the word "implementation".

## Commit messages

- Keep the subject line to a single summary of **no more than 80 characters** and do not end it with a
  period.
- Do not write the message in the first person.
- Keep the body to **3–5 sentences**, depending on the number of changes.

## Pull requests

- Title and description are always written in **English**, regardless of the language used in the
  conversation.
- Never mention Claude, Anthropic, Copilot, or any other AI assistant in the PR title or description.
  Do not add "Generated with …", "Co-Authored-By: Claude …", session links, or similar attribution —
  the description only describes the change itself.

## Commands

```bash
dotnet restore PlexToJellyfinSync.slnx
reihitsu-format ./                                          # dotnet tool install -g Reihitsu.Cli --prerelease
dotnet build PlexToJellyfinSync.slnx -c Release --no-restore
dotnet test PlexToJellyfinSync.slnx -c Release --no-build
```

Single test: `dotnet test tests/PlexToJellyfinSync.Tests/PlexToJellyfinSync.Tests.csproj --filter "FullyQualifiedName~ClassName.MethodName"`

## Architecture

- `PlexToJellyfinSync.Core` — interfaces, enums, options, domain/view models
- `PlexToJellyfinSync.Data` — Plex JSON DTOs
- `PlexToJellyfinSync.Service` — PlexClient, NfoWriter, PathMapper, WatchAggregator, SyncOrchestrator,
  StateStore, SyncStatusService, in-memory log store/provider, DI registration
- `PlexToJellyfinSync` — Blazor Server host: `Program.cs`, `Worker` (BackgroundService), dashboard
  components (`Dashboard.razor`, `Logs.razor`), optional `TokenAuthMiddleware`
- `PlexToJellyfinSync.Tests` — MSTest

## Project configuration

- **Target framework** `net10.0`; **nullable reference types**, **implicit usings**, and
  **documentation XML** generation are all enabled.
- **Central Package Management** via `Directory.Packages.props`
  (`<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>`); never put versions in
  individual `.csproj` files.
- **Reihitsu.Analyzer** is a dev dependency in every project (via `Directory.Build.props`); there is no
  StyleCop.Analyzers.
- **Solution format** is `.slnx` (XML-based) at the repository root.

## Code style

File-scoped namespaces; one top-level type per file; `using` outside namespace (System first);
Allman braces, always required; 4-space indent; CRLF; no trailing newline; `var` preferred; language
keywords over BCL types; LINQ method syntax only; `== false` instead of `!`; `is null` /`is not null`;
no primary constructors; constructor injection with `_camelCase` readonly fields; `#region` blocks;
XML docs on all members (English, no `<remarks>`); `.ConfigureAwait(false)` in service/data code.

## Testing

**Unit tests are mandatory for newly written code.** MSTest only (no FluentAssertions, no mocking
library — use real objects or the hand-written fakes/stubs in `tests/PlexToJellyfinSync.Tests`).
Classes `{Feature}Tests`, methods `{Class}{Scenario}{ExpectedResult}` in PascalCase **without
underscores** (e.g. `WatchAggregatorAllWatchedReturnsWatched`, not
`WatchAggregator_AllWatched_ReturnsWatched`); always pass an assert message. Full conventions,
including the project's test-double pattern and the checklist to run before committing a new
test, are in [`UNIT_TESTS.md`](docs/UNIT_TESTS.md).

## Pull requests, contributing and architecture

Follow [`CONTRIBUTING.md`](docs/CONTRIBUTING.md) for branch/PR naming (`[area] Description`), the PR
checklist in [`.github/pull_request_template.md`](.github/pull_request_template.md), and the
stability policy. Consult [`ARCHITECTURE.md`](docs/ARCHITECTURE.md) before changing the sync pipeline,
path mapping, NFO writing, or the dashboard's auth model — several behaviors there (e.g. NFO files
are only ever touched in their watch fields, an unmapped path is always skipped rather than passed
through) are deliberate guarantees, not incidental behavior.
