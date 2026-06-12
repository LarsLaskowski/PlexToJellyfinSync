# Review Session 1: Core (Abstractions, Enums, Models, Options)

Perform a code review of the 24 files listed below from the `PlexToJellyfinSync.Core` project.
You only analyze — **do not change any production code**. The only file you may edit is
`docs/review/results/00-report.md`.

## Preparation

1. Read `docs/review/00-process.md` in full (criteria catalog A–E, severity levels,
   finding convention).
2. Read `.claude/CLAUDE.md` (project rules that criterion B checks against).

## Files to Review (24 — read and assess each one)

Review depth **deep** (criteria A–D), `.csproj` **quick** (criterion E + CPM check):

1. `src/PlexToJellyfinSync.Core/Abstractions/ILogStore.cs`
2. `src/PlexToJellyfinSync.Core/Abstractions/INfoWriter.cs`
3. `src/PlexToJellyfinSync.Core/Abstractions/IPathMapper.cs`
4. `src/PlexToJellyfinSync.Core/Abstractions/IPlexClient.cs`
5. `src/PlexToJellyfinSync.Core/Abstractions/IStateStore.cs`
6. `src/PlexToJellyfinSync.Core/Abstractions/ISyncOrchestrator.cs`
7. `src/PlexToJellyfinSync.Core/Abstractions/ISyncStatusProvider.cs`
8. `src/PlexToJellyfinSync.Core/Enums/MediaKind.cs`
9. `src/PlexToJellyfinSync.Core/Enums/MovieNfoFilenameStrategy.cs`
10. `src/PlexToJellyfinSync.Core/Enums/NfoWriteOutcome.cs`
11. `src/PlexToJellyfinSync.Core/Models/LogEntry.cs`
12. `src/PlexToJellyfinSync.Core/Models/MediaItem.cs`
13. `src/PlexToJellyfinSync.Core/Models/PlexHistoryEntry.cs`
14. `src/PlexToJellyfinSync.Core/Models/PlexLibrary.cs`
15. `src/PlexToJellyfinSync.Core/Models/SyncStatusViewData.cs`
16. `src/PlexToJellyfinSync.Core/Models/UniqueId.cs`
17. `src/PlexToJellyfinSync.Core/Models/WatchInfo.cs`
18. `src/PlexToJellyfinSync.Core/Options/DashboardOptions.cs`
19. `src/PlexToJellyfinSync.Core/Options/NfoOptions.cs`
20. `src/PlexToJellyfinSync.Core/Options/PathMapping.cs`
21. `src/PlexToJellyfinSync.Core/Options/PlexOptions.cs`
22. `src/PlexToJellyfinSync.Core/Options/StateOptions.cs`
23. `src/PlexToJellyfinSync.Core/Options/SyncOptions.cs`
24. `src/PlexToJellyfinSync.Core/PlexToJellyfinSync.Core.csproj`

## Focus Areas for This Session

- **B**: `#region` blocks, XML docs (English, no `<remarks>`), file-scoped namespaces,
  one type per file — small interfaces/models are where this is most often violated.
- **D**: Are the abstractions sensibly scoped? Does Core really reference no other layer
  (no references to Data/Service/Host)? Are the Options classes validatable
  (sensible defaults, required fields recognizable)?
- **A**: Nullability of the model properties (does the `?` annotation match the actual
  population?), value semantics of `UniqueId`/`WatchInfo` (Equals/GetHashCode needed?).
- `.csproj`: no package versions (CPM), Reihitsu.Analyzer wired in.

## Record Results

1. Record each finding under `## Findings → ### Session 1 — Core` in
   `docs/review/results/00-report.md`. Finding IDs: `F-101`, `F-102`, …
   Format per the process doc (file+line, criterion, severity, description, recommendation).
2. In the file checklist, set the status of all 24 files of this session to ✅ and enter the
   associated finding IDs in the "Findings" column ("none" if there are no findings).
3. Remove the "_Not yet performed._" placeholder under the session heading.

## Wrap-up

1. Self-check: Have all 24 files in the list above been read, assessed, and marked ✅ in the
   checklist? If not, follow up.
2. Show me a short summary of the findings.
3. Ask me for confirmation and only then commit
   (commit message: `Review Session 1: Core`) and push the current branch.
