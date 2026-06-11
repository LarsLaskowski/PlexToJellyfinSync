# Review-Session 4: Service II (NfoWriter, SyncOrchestrator, State, Status)

Führe ein Code-Review der unten gelisteten 5 Dateien des Projekts `PlexToJellyfinSync.Service`
durch. Das sind die beiden größten Klassen des Repos (`SyncOrchestrator` ~420 Zeilen,
`NfoWriter` ~385 Zeilen) — plane entsprechend Sorgfalt ein. Du analysierst nur — **ändere
keinen Produktiv-Code**. Die einzige Datei, die du bearbeiten darfst, ist
`docs/review/ergebnisse/00-report.md`.

## Vorbereitung

1. Lies `docs/review/00-vorgehensweise.md` vollständig (Kriterienkatalog A–E, Schweregrade,
   Befund-Konvention).
2. Lies `.claude/CLAUDE.md` (Projektregeln, auf die Kriterium B prüft).
3. Kontext: Interfaces in `src/PlexToJellyfinSync.Core/Abstractions/`, Optionen in
   `src/PlexToJellyfinSync.Core/Options/`, Aufrufer ist `src/PlexToJellyfinSync/Worker.cs`.
   Ziehe sie bei Bedarf lesend hinzu.

## Zu prüfende Dateien (5 — jede einzelne lesen und bewerten)

Prüftiefe **tief** (Kriterien A–D):

1. `src/PlexToJellyfinSync.Service/NfoWriter.cs`
2. `src/PlexToJellyfinSync.Service/SyncOrchestrator.cs`
3. `src/PlexToJellyfinSync.Service/State/StateStore.cs`
4. `src/PlexToJellyfinSync.Service/State/SyncStateFile.cs`
5. `src/PlexToJellyfinSync.Service/SyncStatusService.cs`

## Schwerpunkte dieser Session

- **A (NfoWriter)**: Bestehende `.nfo`-Inhalte müssen erhalten bleiben (nur Watch-Felder
  ändern) — XML-Parsing-Robustheit bei kaputten/fremden NFOs, Encoding/BOM, atomares
  Schreiben (temp + rename?), Verhalten bei fehlender Zieldatei, `MovieNfoFilenameStrategy`
  korrekt umgesetzt.
- **C (NfoWriter/PathMapper-Zusammenspiel)**: Kann ein manipulierter Plex-Pfad zum Schreiben
  außerhalb der Library-Wurzel führen?
- **A (SyncOrchestrator)**: Gesamtablauf eines Sync-Zyklus — Fehler in einem Item darf den
  Zyklus nicht abbrechen; `CancellationToken` überall; `.ConfigureAwait(false)`;
  Idempotenz (zweiter Lauf ohne Änderungen = keine Schreibvorgänge).
- **A (StateStore/SyncStateFile)**: Atomares Persistieren, Verhalten bei korrupter
  State-Datei, Thread-Sicherheit (Worker schreibt, Dashboard liest), Migrations-/
  Versionsstrategie des Dateiformats.
- **A (SyncStatusService)**: Thread-Sicherheit der Statusdaten, die Blazor-Circuits parallel
  lesen, während der Worker sie aktualisiert.
- **B**: `#region`-Blöcke, XML-Doku, `== false`, `is null`, LINQ-Methodensyntax — bei großen
  Klassen besonders gründlich prüfen.
- **D**: Macht `SyncOrchestrator` zu viel (God-Class)? Testlücken dokumentieren: für keine
  dieser fünf Klassen außer `NfoWriter` existieren Tests?

## Ergebnis festhalten

1. Trage jeden Befund unter `## Befunde → ### Session 4 — Service II (Sync & Persistenz)` in
   `docs/review/ergebnisse/00-report.md` ein. Befund-IDs: `F-401`, `F-402`, …
   Format gemäß Vorgehensweise (Datei+Zeile, Kriterium, Schweregrad, Beschreibung, Empfehlung).
2. Setze in der Datei-Checkliste den Status aller 5 Dateien dieser Session auf ✅ und trage
   die zugehörigen Befund-IDs in die Spalte „Befunde" ein („keine", falls befundfrei).
3. Entferne den Platzhalter „_Noch nicht durchgeführt._" der Session-Überschrift.

## Abschluss

1. Selbstkontrolle: Sind alle 5 Dateien aus der Liste oben gelesen, bewertet und in der
   Checkliste auf ✅? Falls nein, nacharbeiten.
2. Zeige mir eine kurze Zusammenfassung der Befunde.
3. Frage mich um Bestätigung und committe erst danach
   (Commit-Message: `Review Session 4: Service II (Sync & Persistenz)`) und pushe den
   aktuellen Branch.
