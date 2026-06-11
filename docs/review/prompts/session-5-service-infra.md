# Review-Session 5: Service III (Logging, DI-Registrierung)

Führe ein Code-Review der unten gelisteten 5 Dateien des Projekts `PlexToJellyfinSync.Service`
durch. Du analysierst nur — **ändere keinen Produktiv-Code**. Die einzige Datei, die du
bearbeiten darfst, ist `docs/review/ergebnisse/00-report.md`.

## Vorbereitung

1. Lies `docs/review/00-vorgehensweise.md` vollständig (Kriterienkatalog A–E, Schweregrade,
   Befund-Konvention).
2. Lies `.claude/CLAUDE.md` (Projektregeln, auf die Kriterium B prüft).
3. Kontext: Der In-Memory-Log-Store speist die Live-Log-Ansicht
   (`src/PlexToJellyfinSync/Components/Pages/Logs.razor`); die DI-Registrierung wird von
   `src/PlexToJellyfinSync/Program.cs` aufgerufen. Ziehe beide bei Bedarf lesend hinzu.

## Zu prüfende Dateien (5 — jede einzelne lesen und bewerten)

Prüftiefe **tief** (Kriterien A–D), `.csproj` **kurz** (Kriterium E + CPM-Check):

1. `src/PlexToJellyfinSync.Service/Logging/InMemoryLogProvider.cs`
2. `src/PlexToJellyfinSync.Service/Logging/InMemoryLogStore.cs`
3. `src/PlexToJellyfinSync.Service/Logging/InMemoryLogger.cs`
4. `src/PlexToJellyfinSync.Service/ServiceCollectionExtensions.cs`
5. `src/PlexToJellyfinSync.Service/PlexToJellyfinSync.Service.csproj`

## Schwerpunkte dieser Session

- **A (Logging)**: Thread-Sicherheit des Stores (viele Logger schreiben, Blazor-Circuits
  lesen), Begrenzung der Einträge (Ringpuffer? Memory-Leak bei Dauerbetrieb?),
  `IDisposable`/Scope-Handling im Provider, korrekte `ILoggerProvider`-Implementierung
  (Kategorie-Caching, `IsEnabled`).
- **C (Logging)**: Können Plex-Token oder andere Secrets über Log-Nachrichten in der
  Web-Ansicht landen?
- **A/D (ServiceCollectionExtensions)**: Vollständigkeit der Registrierungen (jedes
  Core-Interface gegen genau eine Implementierung), korrekte Lifetimes — Singleton für
  Zustände, die Worker und Blazor teilen; HttpClient via `AddHttpClient`?
  Options-Bindung und -Validierung (`ValidateOnStart`?).
- **B**: `#region`-Blöcke, XML-Doku, `== false`, `is null`, `_camelCase`-readonly-Felder.
- `.csproj`: keine Paketversionen (CPM), Reihitsu.Analyzer eingebunden, Projektreferenzen
  nur auf Core und Data.

## Ergebnis festhalten

1. Trage jeden Befund unter `## Befunde → ### Session 5 — Service III (Logging & DI)` in
   `docs/review/ergebnisse/00-report.md` ein. Befund-IDs: `F-501`, `F-502`, …
   Format gemäß Vorgehensweise (Datei+Zeile, Kriterium, Schweregrad, Beschreibung, Empfehlung).
2. Setze in der Datei-Checkliste den Status aller 5 Dateien dieser Session auf ✅ und trage
   die zugehörigen Befund-IDs in die Spalte „Befunde" ein („keine", falls befundfrei).
3. Entferne den Platzhalter „_Noch nicht durchgeführt._" der Session-Überschrift.

## Abschluss

1. Selbstkontrolle: Sind alle 5 Dateien aus der Liste oben gelesen, bewertet und in der
   Checkliste auf ✅? Falls nein, nacharbeiten.
2. Zeige mir eine kurze Zusammenfassung der Befunde.
3. Frage mich um Bestätigung und committe erst danach
   (Commit-Message: `Review Session 5: Service III (Logging & DI)`) und pushe den
   aktuellen Branch.
