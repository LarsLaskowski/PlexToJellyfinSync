# Review-Session 3: Service I (PlexClient, WatchAggregator, PathMapper)

Führe ein Code-Review der unten gelisteten 4 Dateien des Projekts `PlexToJellyfinSync.Service`
durch. Du analysierst nur — **ändere keinen Produktiv-Code**. Die einzige Datei, die du
bearbeiten darfst, ist `docs/review/ergebnisse/00-report.md`.

## Vorbereitung

1. Lies `docs/review/00-vorgehensweise.md` vollständig (Kriterienkatalog A–E, Schweregrade,
   Befund-Konvention).
2. Lies `.claude/CLAUDE.md` (Projektregeln, auf die Kriterium B prüft).
3. Kontext: Die zugehörigen Interfaces liegen in `src/PlexToJellyfinSync.Core/Abstractions/`,
   die DTOs in `src/PlexToJellyfinSync.Data/Plex/`. Ziehe sie bei Bedarf lesend hinzu.

## Zu prüfende Dateien (4 — jede einzelne lesen und bewerten)

Prüftiefe **tief** (Kriterien A–D):

1. `src/PlexToJellyfinSync.Service/PlexClient.cs`
2. `src/PlexToJellyfinSync.Service/PlexJsonOptions.cs`
3. `src/PlexToJellyfinSync.Service/WatchAggregator.cs`
4. `src/PlexToJellyfinSync.Service/PathMapper.cs`

## Schwerpunkte dieser Session

- **A (PlexClient)**: HttpClient-Nutzung (Lebenszyklus, Timeouts), Verhalten bei
  HTTP-Fehlern/Timeouts/ungültigem JSON, `.ConfigureAwait(false)` auf jedem `await`,
  `CancellationToken` durchgängig, Paginierung/große Bibliotheken.
- **C (PlexClient)**: Wie wird der Plex-Token übertragen (Header `X-Plex-Token` vs.
  Query-String)? Taucht der Token in Log-Meldungen, Exceptions oder URLs auf?
- **A (WatchAggregator)**: Aggregationslogik über Episoden/Staffeln — Randfälle: leere
  Historie, mehrfach gesehene Items, fehlende `viewCount`/`lastViewedAt`-Werte, Zeitzonen-
  bzw. Unix-Timestamp-Konvertierung.
- **A/C (PathMapper)**: Mapping Plex-Pfad → Jellyfin-Pfad: Trennzeichen Windows/Linux,
  Groß-/Kleinschreibung, längstes-Präfix-Matching, Verhalten bei nicht gemappten Pfaden;
  Path-Traversal (`..` im Quellpfad darf nicht aus der Zielwurzel herausführen).
- **B**: `#region`-Blöcke, XML-Doku, `== false`, `is null`, `_camelCase`-readonly-Felder,
  LINQ-Methodensyntax.
- **D**: Testbarkeit; Abgleich mit vorhandenen Tests (`PathMapperTests`,
  `WatchAggregatorTests`) — welche Logik ist ungetestet?

## Ergebnis festhalten

1. Trage jeden Befund unter `## Befunde → ### Session 3 — Service I (Plex-Anbindung)` in
   `docs/review/ergebnisse/00-report.md` ein. Befund-IDs: `F-301`, `F-302`, …
   Format gemäß Vorgehensweise (Datei+Zeile, Kriterium, Schweregrad, Beschreibung, Empfehlung).
2. Setze in der Datei-Checkliste den Status aller 4 Dateien dieser Session auf ✅ und trage
   die zugehörigen Befund-IDs in die Spalte „Befunde" ein („keine", falls befundfrei).
3. Entferne den Platzhalter „_Noch nicht durchgeführt._" der Session-Überschrift.

## Abschluss

1. Selbstkontrolle: Sind alle 4 Dateien aus der Liste oben gelesen, bewertet und in der
   Checkliste auf ✅? Falls nein, nacharbeiten.
2. Zeige mir eine kurze Zusammenfassung der Befunde.
3. Frage mich um Bestätigung und committe erst danach
   (Commit-Message: `Review Session 3: Service I (Plex-Anbindung)`) und pushe den aktuellen Branch.
