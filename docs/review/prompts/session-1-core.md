# Review-Session 1: Core (Abstractions, Enums, Models, Options)

Führe ein Code-Review der unten gelisteten 24 Dateien des Projekts `PlexToJellyfinSync.Core`
durch. Du analysierst nur — **ändere keinen Produktiv-Code**. Die einzige Datei, die du
bearbeiten darfst, ist `docs/review/ergebnisse/00-report.md`.

## Vorbereitung

1. Lies `docs/review/00-vorgehensweise.md` vollständig (Kriterienkatalog A–E, Schweregrade,
   Befund-Konvention).
2. Lies `.claude/CLAUDE.md` (Projektregeln, auf die Kriterium B prüft).

## Zu prüfende Dateien (24 — jede einzelne lesen und bewerten)

Prüftiefe **tief** (Kriterien A–D), `.csproj` **kurz** (Kriterium E + CPM-Check):

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

## Schwerpunkte dieser Session

- **B**: `#region`-Blöcke, XML-Doku (Englisch, kein `<remarks>`), file-scoped namespaces,
  ein Typ pro Datei — gerade bei kleinen Interfaces/Models wird das gern verletzt.
- **D**: Sind die Abstraktionen sinnvoll geschnitten? Referenziert Core wirklich keine andere
  Schicht (keine Verweise auf Data/Service/Host)? Sind Options-Klassen validierbar
  (sinnvolle Defaults, Pflichtfelder erkennbar)?
- **A**: Nullability der Model-Properties (passt `?`-Annotation zur realen Befüllung?),
  Wertesemantik von `UniqueId`/`WatchInfo` (Equals/GetHashCode nötig?).
- `.csproj`: keine Paketversionen (CPM), Reihitsu.Analyzer eingebunden.

## Ergebnis festhalten

1. Trage jeden Befund unter `## Befunde → ### Session 1 — Core` in
   `docs/review/ergebnisse/00-report.md` ein. Befund-IDs: `F-101`, `F-102`, …
   Format gemäß Vorgehensweise (Datei+Zeile, Kriterium, Schweregrad, Beschreibung, Empfehlung).
2. Setze in der Datei-Checkliste den Status aller 24 Dateien dieser Session auf ✅ und trage
   die zugehörigen Befund-IDs in die Spalte „Befunde" ein („keine", falls befundfrei).
3. Entferne den Platzhalter „_Noch nicht durchgeführt._" der Session-Überschrift.

## Abschluss

1. Selbstkontrolle: Sind alle 24 Dateien aus der Liste oben gelesen, bewertet und in der
   Checkliste auf ✅? Falls nein, nacharbeiten.
2. Zeige mir eine kurze Zusammenfassung der Befunde.
3. Frage mich um Bestätigung und committe erst danach
   (Commit-Message: `Review Session 1: Core`) und pushe den aktuellen Branch.
