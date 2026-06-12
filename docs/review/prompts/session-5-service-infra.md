# Review Session 5: Service III (Logging, DI Registration)

Perform a code review of the 5 files listed below from the `PlexToJellyfinSync.Service` project.
You only analyze — **do not change any production code**. The only file you may edit is
`docs/review/results/00-report.md`.

## Preparation

1. Read `docs/review/00-process.md` in full (criteria catalog A–E, severity levels,
   finding convention).
2. Read `.claude/CLAUDE.md` (project rules that criterion B checks against).
3. Context: The in-memory log store feeds the live log view
   (`src/PlexToJellyfinSync/Components/Pages/Logs.razor`); the DI registration is called by
   `src/PlexToJellyfinSync/Program.cs`. Read both as needed.

## Files to Review (5 — read and assess each one)

Review depth **deep** (criteria A–D), `.csproj` **quick** (criterion E + CPM check):

1. `src/PlexToJellyfinSync.Service/Logging/InMemoryLogProvider.cs`
2. `src/PlexToJellyfinSync.Service/Logging/InMemoryLogStore.cs`
3. `src/PlexToJellyfinSync.Service/Logging/InMemoryLogger.cs`
4. `src/PlexToJellyfinSync.Service/ServiceCollectionExtensions.cs`
5. `src/PlexToJellyfinSync.Service/PlexToJellyfinSync.Service.csproj`

## Focus Areas for This Session

- **A (Logging)**: Thread safety of the store (many loggers write, Blazor circuits
  read), bounding of entries (ring buffer? memory leak in long-running operation?),
  `IDisposable`/scope handling in the provider, correct `ILoggerProvider` implementation
  (category caching, `IsEnabled`).
- **C (Logging)**: Can Plex tokens or other secrets end up in the web view via log
  messages?
- **A/D (ServiceCollectionExtensions)**: Completeness of registrations (each Core interface
  against exactly one implementation), correct lifetimes — Singleton for state shared by
  Worker and Blazor; HttpClient via `AddHttpClient`?
  Options binding and validation (`ValidateOnStart`?).
- **B**: `#region` blocks, XML docs, `== false`, `is null`, `_camelCase` readonly fields.
- `.csproj`: no package versions (CPM), Reihitsu.Analyzer wired in, project references
  only to Core and Data.

## Record Results

1. Record each finding under `## Findings → ### Session 5 — Service III (Logging & DI)` in
   `docs/review/results/00-report.md`. Finding IDs: `F-501`, `F-502`, …
   Format per the process doc (file+line, criterion, severity, description, recommendation).
2. In the file checklist, set the status of all 5 files of this session to ✅ and enter the
   associated finding IDs in the "Findings" column ("none" if there are no findings).
3. Remove the "_Not yet performed._" placeholder under the session heading.

## Wrap-up

1. Self-check: Have all 5 files in the list above been read, assessed, and marked ✅ in the
   checklist? If not, follow up.
2. Show me a short summary of the findings.
3. Ask me for confirmation and only then commit
   (commit message: `Review Session 5: Service III (Logging & DI)`) and push the
   current branch.
