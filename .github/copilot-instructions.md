# PlexToJellyfinSync Project Instructions

This file describes project-specific conventions and configuration for PlexToJellyfinSync.
Copilot and other AI assistants must follow these guidelines when working in this repository.

---

## Project purpose

A .NET 10 worker (with an ASP.NET / Blazor Server host) that cyclically reads the Plex watch state
and writes it into Jellyfin `.nfo` files. Polling only (no webhooks), single user (the Plex owner),
runs as a Docker container, and exposes a web dashboard for status and logs.

---

## Commit Messages

- The first line should be a one-line summary of no more than 80 characters
- Do not end the subject line with a period
- Do not write the text in the first person
- Keep the main body to a maximum of 3–5 sentences, depending on the number of changes

---

## Git Workflow

- **Never commit or push automatically**: Do **not** run `git commit`, `git push`, or any combination
  thereof without **explicit user approval**
- After making changes, present a summary of the modifications and **ask the user** before committing
- Do **not** create branches, tags, or perform any other Git write operations without being explicitly
  told to do so
- Read-only Git commands (`git status`, `git diff`, `git log`, etc.) are always allowed

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

Run `reihitsu-format ./` after source changes and before running a build. The command is available as
a .NET tool and can be installed with `dotnet tool install -g Reihitsu.Cli` if it is missing.
Static analysis runs during build through the **Reihitsu.Analyzer** (added to every project). There is
**no StyleCop.Analyzers**.

A build must finish with **zero Reihitsu (`RH####`) warnings and errors**. Treat every `RH` diagnostic
as a failure and fix it before considering the work done — do not leave analyzer warnings behind.

---

## Project Structure & Configuration

- **Target Framework**: `net10.0`
- **Nullable Reference Types**: Always enabled (`<Nullable>enable</Nullable>`)
- **Implicit Usings**: Enabled (`<ImplicitUsings>enable</ImplicitUsings>`)
- **Documentation XML**: Enabled (`<GenerateDocumentationFile>true</GenerateDocumentationFile>`)
- **Central Package Management**: `Directory.Packages.props` with `<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>`
- **Code Analysis**: `Reihitsu.Analyzer` as a dev dependency in every project (via `Directory.Build.props`)
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

- **Framework**: MSTest (`Microsoft.VisualStudio.TestTools.UnitTesting`)
- **Assertions**: MSTest `Assert` class only — **do NOT use FluentAssertions**
- **Test class naming**: `{Feature}Tests`
- **Test method naming**: `{Class}{Scenario}{ExpectedResult}` in PascalCase **without underscores**
  (e.g. `WatchAggregatorAllWatchedReturnsWatched`, not `WatchAggregator_AllWatched_ReturnsWatched`)
- **Assert messages** are always provided

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
