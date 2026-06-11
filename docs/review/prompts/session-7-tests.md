# Review-Session 7: Tests (MSTest)

Führe ein Code-Review der unten gelisteten 6 Dateien des Projekts `PlexToJellyfinSync.Tests`
durch. Du analysierst nur — **ändere keinen Produktiv- oder Test-Code**. Die einzige Datei,
die du bearbeiten darfst, ist `docs/review/ergebnisse/00-report.md`.

## Vorbereitung

1. Lies `docs/review/00-vorgehensweise.md` vollständig (Kriterienkatalog A–E, Schweregrade,
   Befund-Konvention).
2. Lies `.claude/CLAUDE.md`, insbesondere den Abschnitt **Testing** (Namensschema,
   Assert-Messages, kein FluentAssertions).
3. Kontext: Die getesteten Klassen liegen in `src/PlexToJellyfinSync.Service/` und
   `src/PlexToJellyfinSync.Data/`. Ziehe sie bei Bedarf lesend hinzu.

## Zu prüfende Dateien (6 — jede einzelne lesen und bewerten)

Prüftiefe **tief** (Kriterien A–D), `.csproj` **kurz** (Kriterium E + CPM-Check):

1. `tests/PlexToJellyfinSync.Tests/MSTestSettings.cs`
2. `tests/PlexToJellyfinSync.Tests/NfoWriterTests.cs`
3. `tests/PlexToJellyfinSync.Tests/PathMapperTests.cs`
4. `tests/PlexToJellyfinSync.Tests/PlexMetadataDeserializationTests.cs`
5. `tests/PlexToJellyfinSync.Tests/WatchAggregatorTests.cs`
6. `tests/PlexToJellyfinSync.Tests/PlexToJellyfinSync.Tests.csproj`

## Schwerpunkte dieser Session

- **B (Testkonventionen)**: Klassennamen `{Feature}Tests`; Methodennamen
  `{Class}{Scenario}{ExpectedResult}` in PascalCase **ohne Unterstriche**; jede Assertion
  mit Assert-Message; nur MSTest (kein FluentAssertions); `#region`-Blöcke und XML-Doku
  auch in Testklassen.
- **A (Testqualität)**: Testen die Tests das richtige Verhalten oder nur
  Implementierungsdetails? Aufräumen von Temp-Dateien (`NfoWriterTests` schreibt vermutlich
  auf Platte — `TestCleanup`?), deterministische Tests (keine Zeitzonen-/Kultur-Abhängigkeit).
- **D (Abdeckungslücken)**: Dokumentiere als Befunde, welche Service-Klassen **keine** Tests
  haben (z.B. `SyncOrchestrator`, `StateStore`, `PlexClient`, `SyncStatusService`,
  Logging-Klassen) und welche Randfälle in bestehenden Testklassen fehlen.
  **Schreibe keine neuen Tests** — nur dokumentieren.
- `.csproj`: keine Paketversionen (CPM), korrekte Projektreferenzen.

## Ergebnis festhalten

1. Trage jeden Befund unter `## Befunde → ### Session 7 — Tests` in
   `docs/review/ergebnisse/00-report.md` ein. Befund-IDs: `F-701`, `F-702`, …
   Format gemäß Vorgehensweise (Datei+Zeile, Kriterium, Schweregrad, Beschreibung, Empfehlung).
2. Setze in der Datei-Checkliste den Status aller 6 Dateien dieser Session auf ✅ und trage
   die zugehörigen Befund-IDs in die Spalte „Befunde" ein („keine", falls befundfrei).
3. Entferne den Platzhalter „_Noch nicht durchgeführt._" der Session-Überschrift.

## Abschluss

1. Selbstkontrolle: Sind alle 6 Dateien aus der Liste oben gelesen, bewertet und in der
   Checkliste auf ✅? Falls nein, nacharbeiten.
2. Zeige mir eine kurze Zusammenfassung der Befunde.
3. Frage mich um Bestätigung und committe erst danach
   (Commit-Message: `Review Session 7: Tests`) und pushe den aktuellen Branch.
