# Review-Report: Vollständige Repo-Analyse PlexToJellyfinSync

Zentrale Ergebnisdatei des Reviews. Vorgehen und Kriterien: siehe
[`docs/review/00-vorgehensweise.md`](../00-vorgehensweise.md). Jede Review-Session trägt ihre
Befunde unter ihrer Überschrift ein und setzt den Status der geprüften Dateien in der
Checkliste auf ✅.

## Zusammenfassung

_Wird nach Abschluss aller Sessions befüllt._

| Schweregrad | Anzahl |
|---|---|
| 🔴 Kritisch | – |
| 🟠 Hoch | – |
| 🟡 Mittel | – |
| 🔵 Niedrig | – |
| ⚪ Hinweis | – |

## Befunde

Format je Befund:

> ### F-XYZ — Kurztitel
> **Datei:** `pfad/zur/datei.cs:Zeile` · **Kriterium:** A–E · **Schweregrad:** 🔴/🟠/🟡/🔵/⚪
>
> Beschreibung des Problems.
>
> **Empfehlung:** Vorgeschlagene Lösung.

### Session 1 — Core

_Noch nicht durchgeführt._

### Session 2 — Data

_Noch nicht durchgeführt._

### Session 3 — Service I (Plex-Anbindung)

_Noch nicht durchgeführt._

### Session 4 — Service II (Sync & Persistenz)

_Noch nicht durchgeführt._

### Session 5 — Service III (Logging & DI)

_Noch nicht durchgeführt._

### Session 6 — Host (Blazor, Worker, Security)

_Noch nicht durchgeführt._

### Session 7 — Tests

_Noch nicht durchgeführt._

### Session 8 — Infrastruktur

_Noch nicht durchgeführt._

## Datei-Checkliste

Status: ⬜ offen · ✅ geprüft. Prüftiefe: **tief** = Kriterien A–D, **kurz** = Kriterium E
(Configs/Workflows zusätzlich C).

| Datei | Session | Prüftiefe | Status | Befunde |
|---|---|---|---|---|
| `src/PlexToJellyfinSync.Core/Abstractions/ILogStore.cs` | 1 | tief | ⬜ | |
| `src/PlexToJellyfinSync.Core/Abstractions/INfoWriter.cs` | 1 | tief | ⬜ | |
| `src/PlexToJellyfinSync.Core/Abstractions/IPathMapper.cs` | 1 | tief | ⬜ | |
| `src/PlexToJellyfinSync.Core/Abstractions/IPlexClient.cs` | 1 | tief | ⬜ | |
| `src/PlexToJellyfinSync.Core/Abstractions/IStateStore.cs` | 1 | tief | ⬜ | |
| `src/PlexToJellyfinSync.Core/Abstractions/ISyncOrchestrator.cs` | 1 | tief | ⬜ | |
| `src/PlexToJellyfinSync.Core/Abstractions/ISyncStatusProvider.cs` | 1 | tief | ⬜ | |
| `src/PlexToJellyfinSync.Core/Enums/MediaKind.cs` | 1 | tief | ⬜ | |
| `src/PlexToJellyfinSync.Core/Enums/MovieNfoFilenameStrategy.cs` | 1 | tief | ⬜ | |
| `src/PlexToJellyfinSync.Core/Enums/NfoWriteOutcome.cs` | 1 | tief | ⬜ | |
| `src/PlexToJellyfinSync.Core/Models/LogEntry.cs` | 1 | tief | ⬜ | |
| `src/PlexToJellyfinSync.Core/Models/MediaItem.cs` | 1 | tief | ⬜ | |
| `src/PlexToJellyfinSync.Core/Models/PlexHistoryEntry.cs` | 1 | tief | ⬜ | |
| `src/PlexToJellyfinSync.Core/Models/PlexLibrary.cs` | 1 | tief | ⬜ | |
| `src/PlexToJellyfinSync.Core/Models/SyncStatusViewData.cs` | 1 | tief | ⬜ | |
| `src/PlexToJellyfinSync.Core/Models/UniqueId.cs` | 1 | tief | ⬜ | |
| `src/PlexToJellyfinSync.Core/Models/WatchInfo.cs` | 1 | tief | ⬜ | |
| `src/PlexToJellyfinSync.Core/Options/DashboardOptions.cs` | 1 | tief | ⬜ | |
| `src/PlexToJellyfinSync.Core/Options/NfoOptions.cs` | 1 | tief | ⬜ | |
| `src/PlexToJellyfinSync.Core/Options/PathMapping.cs` | 1 | tief | ⬜ | |
| `src/PlexToJellyfinSync.Core/Options/PlexOptions.cs` | 1 | tief | ⬜ | |
| `src/PlexToJellyfinSync.Core/Options/StateOptions.cs` | 1 | tief | ⬜ | |
| `src/PlexToJellyfinSync.Core/Options/SyncOptions.cs` | 1 | tief | ⬜ | |
| `src/PlexToJellyfinSync.Core/PlexToJellyfinSync.Core.csproj` | 1 | kurz | ⬜ | |
| `src/PlexToJellyfinSync.Data/Plex/PlexAccount.cs` | 2 | tief | ⬜ | |
| `src/PlexToJellyfinSync.Data/Plex/PlexAccountsContainer.cs` | 2 | tief | ⬜ | |
| `src/PlexToJellyfinSync.Data/Plex/PlexAccountsResponse.cs` | 2 | tief | ⬜ | |
| `src/PlexToJellyfinSync.Data/Plex/PlexDirectory.cs` | 2 | tief | ⬜ | |
| `src/PlexToJellyfinSync.Data/Plex/PlexGuid.cs` | 2 | tief | ⬜ | |
| `src/PlexToJellyfinSync.Data/Plex/PlexLibrariesContainer.cs` | 2 | tief | ⬜ | |
| `src/PlexToJellyfinSync.Data/Plex/PlexLibrariesResponse.cs` | 2 | tief | ⬜ | |
| `src/PlexToJellyfinSync.Data/Plex/PlexMedia.cs` | 2 | tief | ⬜ | |
| `src/PlexToJellyfinSync.Data/Plex/PlexMetadata.cs` | 2 | tief | ⬜ | |
| `src/PlexToJellyfinSync.Data/Plex/PlexMetadataContainer.cs` | 2 | tief | ⬜ | |
| `src/PlexToJellyfinSync.Data/Plex/PlexMetadataResponse.cs` | 2 | tief | ⬜ | |
| `src/PlexToJellyfinSync.Data/Plex/PlexPart.cs` | 2 | tief | ⬜ | |
| `src/PlexToJellyfinSync.Data/Plex/PlexTag.cs` | 2 | tief | ⬜ | |
| `src/PlexToJellyfinSync.Data/PlexToJellyfinSync.Data.csproj` | 2 | kurz | ⬜ | |
| `src/PlexToJellyfinSync.Service/PlexClient.cs` | 3 | tief | ⬜ | |
| `src/PlexToJellyfinSync.Service/PlexJsonOptions.cs` | 3 | tief | ⬜ | |
| `src/PlexToJellyfinSync.Service/WatchAggregator.cs` | 3 | tief | ⬜ | |
| `src/PlexToJellyfinSync.Service/PathMapper.cs` | 3 | tief | ⬜ | |
| `src/PlexToJellyfinSync.Service/NfoWriter.cs` | 4 | tief | ⬜ | |
| `src/PlexToJellyfinSync.Service/SyncOrchestrator.cs` | 4 | tief | ⬜ | |
| `src/PlexToJellyfinSync.Service/State/StateStore.cs` | 4 | tief | ⬜ | |
| `src/PlexToJellyfinSync.Service/State/SyncStateFile.cs` | 4 | tief | ⬜ | |
| `src/PlexToJellyfinSync.Service/SyncStatusService.cs` | 4 | tief | ⬜ | |
| `src/PlexToJellyfinSync.Service/Logging/InMemoryLogProvider.cs` | 5 | tief | ⬜ | |
| `src/PlexToJellyfinSync.Service/Logging/InMemoryLogStore.cs` | 5 | tief | ⬜ | |
| `src/PlexToJellyfinSync.Service/Logging/InMemoryLogger.cs` | 5 | tief | ⬜ | |
| `src/PlexToJellyfinSync.Service/ServiceCollectionExtensions.cs` | 5 | tief | ⬜ | |
| `src/PlexToJellyfinSync.Service/PlexToJellyfinSync.Service.csproj` | 5 | kurz | ⬜ | |
| `src/GlobalSuppressions.cs` | 6 | tief | ⬜ | |
| `src/PlexToJellyfinSync/Program.cs` | 6 | tief | ⬜ | |
| `src/PlexToJellyfinSync/Worker.cs` | 6 | tief | ⬜ | |
| `src/PlexToJellyfinSync/Security/LoginPage.cs` | 6 | tief | ⬜ | |
| `src/PlexToJellyfinSync/Security/TokenAuthMiddleware.cs` | 6 | tief | ⬜ | |
| `src/PlexToJellyfinSync/Components/App.razor` | 6 | tief | ⬜ | |
| `src/PlexToJellyfinSync/Components/Routes.razor` | 6 | tief | ⬜ | |
| `src/PlexToJellyfinSync/Components/_Imports.razor` | 6 | tief | ⬜ | |
| `src/PlexToJellyfinSync/Components/Layout/MainLayout.razor` | 6 | tief | ⬜ | |
| `src/PlexToJellyfinSync/Components/Layout/MainLayout.razor.css` | 6 | kurz | ⬜ | |
| `src/PlexToJellyfinSync/Components/Layout/ReconnectModal.razor` | 6 | tief | ⬜ | |
| `src/PlexToJellyfinSync/Components/Layout/ReconnectModal.razor.css` | 6 | kurz | ⬜ | |
| `src/PlexToJellyfinSync/Components/Layout/ReconnectModal.razor.js` | 6 | kurz | ⬜ | |
| `src/PlexToJellyfinSync/Components/Pages/Dashboard.razor` | 6 | tief | ⬜ | |
| `src/PlexToJellyfinSync/Components/Pages/Error.razor` | 6 | tief | ⬜ | |
| `src/PlexToJellyfinSync/Components/Pages/Logs.razor` | 6 | tief | ⬜ | |
| `src/PlexToJellyfinSync/Components/Pages/NotFound.razor` | 6 | tief | ⬜ | |
| `src/PlexToJellyfinSync/wwwroot/app.css` | 6 | kurz | ⬜ | |
| `src/PlexToJellyfinSync/appsettings.json` | 6 | kurz | ⬜ | |
| `src/PlexToJellyfinSync/appsettings.Development.json` | 6 | kurz | ⬜ | |
| `src/PlexToJellyfinSync/Properties/launchSettings.json` | 6 | kurz | ⬜ | |
| `src/PlexToJellyfinSync/PlexToJellyfinSync.csproj` | 6 | kurz | ⬜ | |
| `tests/PlexToJellyfinSync.Tests/MSTestSettings.cs` | 7 | tief | ⬜ | |
| `tests/PlexToJellyfinSync.Tests/NfoWriterTests.cs` | 7 | tief | ⬜ | |
| `tests/PlexToJellyfinSync.Tests/PathMapperTests.cs` | 7 | tief | ⬜ | |
| `tests/PlexToJellyfinSync.Tests/PlexMetadataDeserializationTests.cs` | 7 | tief | ⬜ | |
| `tests/PlexToJellyfinSync.Tests/WatchAggregatorTests.cs` | 7 | tief | ⬜ | |
| `tests/PlexToJellyfinSync.Tests/PlexToJellyfinSync.Tests.csproj` | 7 | kurz | ⬜ | |
| `.claude/CLAUDE.md` | 8 | kurz | ⬜ | |
| `.claude/hooks/sonar-secrets/build-scripts/pretool-secrets.ps1` | 8 | kurz | ⬜ | |
| `.claude/hooks/sonar-secrets/build-scripts/prompt-secrets.ps1` | 8 | kurz | ⬜ | |
| `.claude/settings.json` | 8 | kurz | ⬜ | |
| `.claude/skills/publish-pr/prompt.md` | 8 | kurz | ⬜ | |
| `.dockerignore` | 8 | kurz | ⬜ | |
| `.editorconfig` | 8 | kurz | ⬜ | |
| `.gitattributes` | 8 | kurz | ⬜ | |
| `.gitignore` | 8 | kurz | ⬜ | |
| `.github/ISSUE_TEMPLATE/bug_report.md` | 8 | kurz | ⬜ | |
| `.github/ISSUE_TEMPLATE/feature_request.md` | 8 | kurz | ⬜ | |
| `.github/copilot-instructions.md` | 8 | kurz | ⬜ | |
| `.github/dependabot.yml` | 8 | kurz | ⬜ | |
| `.github/skills/create-pr/SKILL.md` | 8 | kurz | ⬜ | |
| `.github/skills/publish-pr/SKILL.md` | 8 | kurz | ⬜ | |
| `.github/workflows/ci.yml` | 8 | kurz | ⬜ | |
| `.github/workflows/codeql.yml` | 8 | kurz | ⬜ | |
| `.github/workflows/release.yml` | 8 | kurz | ⬜ | |
| `Directory.Build.props` | 8 | kurz | ⬜ | |
| `Directory.Packages.props` | 8 | kurz | ⬜ | |
| `Dockerfile` | 8 | kurz | ⬜ | |
| `LICENSE.md` | 8 | kurz | ⬜ | |
| `PlexToJellyfinSync.Debug.ruleset` | 8 | kurz | ⬜ | |
| `PlexToJellyfinSync.Release.ruleset` | 8 | kurz | ⬜ | |
| `PlexToJellyfinSync.slnx` | 8 | kurz | ⬜ | |
| `README.md` | 8 | kurz | ⬜ | |
| `SECURITY.md` | 8 | kurz | ⬜ | |

## Vollständigkeitsnachweis

_Wird in Session 8 ausgefüllt: Abgleich der Checkliste gegen `git ls-files` (Soll: 107/107
Dateien des Review-Stichtags, plus Behandlung später hinzugekommener Dateien gemäß
`00-vorgehensweise.md`)._
