# Review Report: Full Repository Analysis of PlexToJellyfinSync

Central results file of the review. For process and criteria, see
[`docs/review/00-process.md`](../00-process.md). Each review session records its findings under its
heading and sets the status of the reviewed files in the checklist to ✅.

## Summary

_Filled in after all sessions are complete._

| Severity | Count |
|---|---|
| 🔴 Critical | – |
| 🟠 High | – |
| 🟡 Medium | – |
| 🔵 Low | – |
| ⚪ Note | – |

## Findings

Format per finding:

> ### F-XYZ — Short title
> **File:** `path/to/file.cs:line` · **Criterion:** A–E · **Severity:** 🔴/🟠/🟡/🔵/⚪
>
> Description of the problem.
>
> **Recommendation:** Proposed solution.

### Session 1 — Core

_Not yet performed._

### Session 2 — Data

_Not yet performed._

### Session 3 — Service I (Plex Integration)

_Not yet performed._

### Session 4 — Service II (Sync & Persistence)

_Not yet performed._

### Session 5 — Service III (Logging & DI)

_Not yet performed._

### Session 6 — Host (Blazor, Worker, Security)

_Not yet performed._

### Session 7 — Tests

_Not yet performed._

### Session 8 — Infrastructure

_Not yet performed._

## File Checklist

Status: ⬜ open · ✅ reviewed. Review depth: **deep** = criteria A–D, **quick** = criterion E
(configs/workflows additionally C).

| File | Session | Depth | Status | Findings |
|---|---|---|---|---|
| `src/PlexToJellyfinSync.Core/Abstractions/ILogStore.cs` | 1 | deep | ⬜ | |
| `src/PlexToJellyfinSync.Core/Abstractions/INfoWriter.cs` | 1 | deep | ⬜ | |
| `src/PlexToJellyfinSync.Core/Abstractions/IPathMapper.cs` | 1 | deep | ⬜ | |
| `src/PlexToJellyfinSync.Core/Abstractions/IPlexClient.cs` | 1 | deep | ⬜ | |
| `src/PlexToJellyfinSync.Core/Abstractions/IStateStore.cs` | 1 | deep | ⬜ | |
| `src/PlexToJellyfinSync.Core/Abstractions/ISyncOrchestrator.cs` | 1 | deep | ⬜ | |
| `src/PlexToJellyfinSync.Core/Abstractions/ISyncStatusProvider.cs` | 1 | deep | ⬜ | |
| `src/PlexToJellyfinSync.Core/Enums/MediaKind.cs` | 1 | deep | ⬜ | |
| `src/PlexToJellyfinSync.Core/Enums/MovieNfoFilenameStrategy.cs` | 1 | deep | ⬜ | |
| `src/PlexToJellyfinSync.Core/Enums/NfoWriteOutcome.cs` | 1 | deep | ⬜ | |
| `src/PlexToJellyfinSync.Core/Models/LogEntry.cs` | 1 | deep | ⬜ | |
| `src/PlexToJellyfinSync.Core/Models/MediaItem.cs` | 1 | deep | ⬜ | |
| `src/PlexToJellyfinSync.Core/Models/PlexHistoryEntry.cs` | 1 | deep | ⬜ | |
| `src/PlexToJellyfinSync.Core/Models/PlexLibrary.cs` | 1 | deep | ⬜ | |
| `src/PlexToJellyfinSync.Core/Models/SyncStatusViewData.cs` | 1 | deep | ⬜ | |
| `src/PlexToJellyfinSync.Core/Models/UniqueId.cs` | 1 | deep | ⬜ | |
| `src/PlexToJellyfinSync.Core/Models/WatchInfo.cs` | 1 | deep | ⬜ | |
| `src/PlexToJellyfinSync.Core/Options/DashboardOptions.cs` | 1 | deep | ⬜ | |
| `src/PlexToJellyfinSync.Core/Options/NfoOptions.cs` | 1 | deep | ⬜ | |
| `src/PlexToJellyfinSync.Core/Options/PathMapping.cs` | 1 | deep | ⬜ | |
| `src/PlexToJellyfinSync.Core/Options/PlexOptions.cs` | 1 | deep | ⬜ | |
| `src/PlexToJellyfinSync.Core/Options/StateOptions.cs` | 1 | deep | ⬜ | |
| `src/PlexToJellyfinSync.Core/Options/SyncOptions.cs` | 1 | deep | ⬜ | |
| `src/PlexToJellyfinSync.Core/PlexToJellyfinSync.Core.csproj` | 1 | quick | ⬜ | |
| `src/PlexToJellyfinSync.Data/Plex/PlexAccount.cs` | 2 | deep | ⬜ | |
| `src/PlexToJellyfinSync.Data/Plex/PlexAccountsContainer.cs` | 2 | deep | ⬜ | |
| `src/PlexToJellyfinSync.Data/Plex/PlexAccountsResponse.cs` | 2 | deep | ⬜ | |
| `src/PlexToJellyfinSync.Data/Plex/PlexDirectory.cs` | 2 | deep | ⬜ | |
| `src/PlexToJellyfinSync.Data/Plex/PlexGuid.cs` | 2 | deep | ⬜ | |
| `src/PlexToJellyfinSync.Data/Plex/PlexLibrariesContainer.cs` | 2 | deep | ⬜ | |
| `src/PlexToJellyfinSync.Data/Plex/PlexLibrariesResponse.cs` | 2 | deep | ⬜ | |
| `src/PlexToJellyfinSync.Data/Plex/PlexMedia.cs` | 2 | deep | ⬜ | |
| `src/PlexToJellyfinSync.Data/Plex/PlexMetadata.cs` | 2 | deep | ⬜ | |
| `src/PlexToJellyfinSync.Data/Plex/PlexMetadataContainer.cs` | 2 | deep | ⬜ | |
| `src/PlexToJellyfinSync.Data/Plex/PlexMetadataResponse.cs` | 2 | deep | ⬜ | |
| `src/PlexToJellyfinSync.Data/Plex/PlexPart.cs` | 2 | deep | ⬜ | |
| `src/PlexToJellyfinSync.Data/Plex/PlexTag.cs` | 2 | deep | ⬜ | |
| `src/PlexToJellyfinSync.Data/PlexToJellyfinSync.Data.csproj` | 2 | quick | ⬜ | |
| `src/PlexToJellyfinSync.Service/PlexClient.cs` | 3 | deep | ⬜ | |
| `src/PlexToJellyfinSync.Service/PlexJsonOptions.cs` | 3 | deep | ⬜ | |
| `src/PlexToJellyfinSync.Service/WatchAggregator.cs` | 3 | deep | ⬜ | |
| `src/PlexToJellyfinSync.Service/PathMapper.cs` | 3 | deep | ⬜ | |
| `src/PlexToJellyfinSync.Service/NfoWriter.cs` | 4 | deep | ⬜ | |
| `src/PlexToJellyfinSync.Service/SyncOrchestrator.cs` | 4 | deep | ⬜ | |
| `src/PlexToJellyfinSync.Service/State/StateStore.cs` | 4 | deep | ⬜ | |
| `src/PlexToJellyfinSync.Service/State/SyncStateFile.cs` | 4 | deep | ⬜ | |
| `src/PlexToJellyfinSync.Service/SyncStatusService.cs` | 4 | deep | ⬜ | |
| `src/PlexToJellyfinSync.Service/Logging/InMemoryLogProvider.cs` | 5 | deep | ⬜ | |
| `src/PlexToJellyfinSync.Service/Logging/InMemoryLogStore.cs` | 5 | deep | ⬜ | |
| `src/PlexToJellyfinSync.Service/Logging/InMemoryLogger.cs` | 5 | deep | ⬜ | |
| `src/PlexToJellyfinSync.Service/ServiceCollectionExtensions.cs` | 5 | deep | ⬜ | |
| `src/PlexToJellyfinSync.Service/PlexToJellyfinSync.Service.csproj` | 5 | quick | ⬜ | |
| `src/GlobalSuppressions.cs` | 6 | deep | ⬜ | |
| `src/PlexToJellyfinSync/Program.cs` | 6 | deep | ⬜ | |
| `src/PlexToJellyfinSync/Worker.cs` | 6 | deep | ⬜ | |
| `src/PlexToJellyfinSync/Security/LoginPage.cs` | 6 | deep | ⬜ | |
| `src/PlexToJellyfinSync/Security/TokenAuthMiddleware.cs` | 6 | deep | ⬜ | |
| `src/PlexToJellyfinSync/Components/App.razor` | 6 | deep | ⬜ | |
| `src/PlexToJellyfinSync/Components/Routes.razor` | 6 | deep | ⬜ | |
| `src/PlexToJellyfinSync/Components/_Imports.razor` | 6 | deep | ⬜ | |
| `src/PlexToJellyfinSync/Components/Layout/MainLayout.razor` | 6 | deep | ⬜ | |
| `src/PlexToJellyfinSync/Components/Layout/MainLayout.razor.css` | 6 | quick | ⬜ | |
| `src/PlexToJellyfinSync/Components/Layout/ReconnectModal.razor` | 6 | deep | ⬜ | |
| `src/PlexToJellyfinSync/Components/Layout/ReconnectModal.razor.css` | 6 | quick | ⬜ | |
| `src/PlexToJellyfinSync/Components/Layout/ReconnectModal.razor.js` | 6 | quick | ⬜ | |
| `src/PlexToJellyfinSync/Components/Pages/Dashboard.razor` | 6 | deep | ⬜ | |
| `src/PlexToJellyfinSync/Components/Pages/Error.razor` | 6 | deep | ⬜ | |
| `src/PlexToJellyfinSync/Components/Pages/Logs.razor` | 6 | deep | ⬜ | |
| `src/PlexToJellyfinSync/Components/Pages/NotFound.razor` | 6 | deep | ⬜ | |
| `src/PlexToJellyfinSync/wwwroot/app.css` | 6 | quick | ⬜ | |
| `src/PlexToJellyfinSync/appsettings.json` | 6 | quick | ⬜ | |
| `src/PlexToJellyfinSync/appsettings.Development.json` | 6 | quick | ⬜ | |
| `src/PlexToJellyfinSync/Properties/launchSettings.json` | 6 | quick | ⬜ | |
| `src/PlexToJellyfinSync/PlexToJellyfinSync.csproj` | 6 | quick | ⬜ | |
| `tests/PlexToJellyfinSync.Tests/MSTestSettings.cs` | 7 | deep | ⬜ | |
| `tests/PlexToJellyfinSync.Tests/NfoWriterTests.cs` | 7 | deep | ⬜ | |
| `tests/PlexToJellyfinSync.Tests/PathMapperTests.cs` | 7 | deep | ⬜ | |
| `tests/PlexToJellyfinSync.Tests/PlexMetadataDeserializationTests.cs` | 7 | deep | ⬜ | |
| `tests/PlexToJellyfinSync.Tests/WatchAggregatorTests.cs` | 7 | deep | ⬜ | |
| `tests/PlexToJellyfinSync.Tests/PlexToJellyfinSync.Tests.csproj` | 7 | quick | ⬜ | |
| `.claude/CLAUDE.md` | 8 | quick | ⬜ | |
| `.claude/hooks/sonar-secrets/build-scripts/pretool-secrets.ps1` | 8 | quick | ⬜ | |
| `.claude/hooks/sonar-secrets/build-scripts/prompt-secrets.ps1` | 8 | quick | ⬜ | |
| `.claude/settings.json` | 8 | quick | ⬜ | |
| `.claude/skills/publish-pr/prompt.md` | 8 | quick | ⬜ | |
| `.dockerignore` | 8 | quick | ⬜ | |
| `.editorconfig` | 8 | quick | ⬜ | |
| `.gitattributes` | 8 | quick | ⬜ | |
| `.gitignore` | 8 | quick | ⬜ | |
| `.github/ISSUE_TEMPLATE/bug_report.md` | 8 | quick | ⬜ | |
| `.github/ISSUE_TEMPLATE/feature_request.md` | 8 | quick | ⬜ | |
| `.github/copilot-instructions.md` | 8 | quick | ⬜ | |
| `.github/dependabot.yml` | 8 | quick | ⬜ | |
| `.github/skills/create-pr/SKILL.md` | 8 | quick | ⬜ | |
| `.github/skills/publish-pr/SKILL.md` | 8 | quick | ⬜ | |
| `.github/workflows/ci.yml` | 8 | quick | ⬜ | |
| `.github/workflows/codeql.yml` | 8 | quick | ⬜ | |
| `.github/workflows/release.yml` | 8 | quick | ⬜ | |
| `Directory.Build.props` | 8 | quick | ⬜ | |
| `Directory.Packages.props` | 8 | quick | ⬜ | |
| `Dockerfile` | 8 | quick | ⬜ | |
| `LICENSE.md` | 8 | quick | ⬜ | |
| `PlexToJellyfinSync.Debug.ruleset` | 8 | quick | ⬜ | |
| `PlexToJellyfinSync.Release.ruleset` | 8 | quick | ⬜ | |
| `PlexToJellyfinSync.slnx` | 8 | quick | ⬜ | |
| `README.md` | 8 | quick | ⬜ | |
| `SECURITY.md` | 8 | quick | ⬜ | |

## Completeness Proof

_Filled in during Session 8: reconcile the checklist against `git ls-files` (target: 107/107 files
of the review baseline, plus handling of files added later per `00-process.md`)._
