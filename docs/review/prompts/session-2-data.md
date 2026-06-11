# Review-Session 2: Data (Plex-DTOs)

Führe ein Code-Review der unten gelisteten 14 Dateien des Projekts `PlexToJellyfinSync.Data`
durch. Du analysierst nur — **ändere keinen Produktiv-Code**. Die einzige Datei, die du
bearbeiten darfst, ist `docs/review/ergebnisse/00-report.md`.

## Vorbereitung

1. Lies `docs/review/00-vorgehensweise.md` vollständig (Kriterienkatalog A–E, Schweregrade,
   Befund-Konvention).
2. Lies `.claude/CLAUDE.md` (Projektregeln, auf die Kriterium B prüft).
3. Kontext: Diese DTOs werden von `src/PlexToJellyfinSync.Service/PlexClient.cs` mit
   `System.Text.Json` deserialisiert (Optionen in `PlexJsonOptions.cs`). Ziehe beide Dateien
   bei Bedarf lesend hinzu.

## Zu prüfende Dateien (14 — jede einzelne lesen und bewerten)

Prüftiefe **tief** (Kriterien A–D), `.csproj` **kurz** (Kriterium E + CPM-Check):

1. `src/PlexToJellyfinSync.Data/Plex/PlexAccount.cs`
2. `src/PlexToJellyfinSync.Data/Plex/PlexAccountsContainer.cs`
3. `src/PlexToJellyfinSync.Data/Plex/PlexAccountsResponse.cs`
4. `src/PlexToJellyfinSync.Data/Plex/PlexDirectory.cs`
5. `src/PlexToJellyfinSync.Data/Plex/PlexGuid.cs`
6. `src/PlexToJellyfinSync.Data/Plex/PlexLibrariesContainer.cs`
7. `src/PlexToJellyfinSync.Data/Plex/PlexLibrariesResponse.cs`
8. `src/PlexToJellyfinSync.Data/Plex/PlexMedia.cs`
9. `src/PlexToJellyfinSync.Data/Plex/PlexMetadata.cs`
10. `src/PlexToJellyfinSync.Data/Plex/PlexMetadataContainer.cs`
11. `src/PlexToJellyfinSync.Data/Plex/PlexMetadataResponse.cs`
12. `src/PlexToJellyfinSync.Data/Plex/PlexPart.cs`
13. `src/PlexToJellyfinSync.Data/Plex/PlexTag.cs`
14. `src/PlexToJellyfinSync.Data/PlexToJellyfinSync.Data.csproj`

## Schwerpunkte dieser Session

- **A**: Nullability vs. reale Plex-Antworten — welche Felder können fehlen? Stimmen
  `JsonPropertyName`-Attribute mit der Plex-API überein (Groß-/Kleinschreibung)? Korrekte
  Typen für Timestamps (`viewedAt`, `lastViewedAt`), Zähler (`viewCount`) und IDs
  (`ratingKey` als string vs. int)?
- **B**: `#region`-Blöcke und XML-Doku auch auf DTO-Properties; file-scoped namespaces;
  ein Typ pro Datei.
- **D**: Referenziert Data nur Core (oder gar nichts) — keine Service-/Host-Abhängigkeit?
  Sind Container/Response-Wrapper konsistent aufgebaut?
- `.csproj`: keine Paketversionen (CPM), Reihitsu.Analyzer eingebunden.

## Ergebnis festhalten

1. Trage jeden Befund unter `## Befunde → ### Session 2 — Data` in
   `docs/review/ergebnisse/00-report.md` ein. Befund-IDs: `F-201`, `F-202`, …
   Format gemäß Vorgehensweise (Datei+Zeile, Kriterium, Schweregrad, Beschreibung, Empfehlung).
2. Setze in der Datei-Checkliste den Status aller 14 Dateien dieser Session auf ✅ und trage
   die zugehörigen Befund-IDs in die Spalte „Befunde" ein („keine", falls befundfrei).
3. Entferne den Platzhalter „_Noch nicht durchgeführt._" der Session-Überschrift.

## Abschluss

1. Selbstkontrolle: Sind alle 14 Dateien aus der Liste oben gelesen, bewertet und in der
   Checkliste auf ✅? Falls nein, nacharbeiten.
2. Zeige mir eine kurze Zusammenfassung der Befunde.
3. Frage mich um Bestätigung und committe erst danach
   (Commit-Message: `Review Session 2: Data`) und pushe den aktuellen Branch.
