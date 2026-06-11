# Review-Session 6: Host (Program, Worker, Security, Blazor-Komponenten)

Führe ein Code-Review der unten gelisteten 22 Dateien des Host-Projekts `PlexToJellyfinSync`
durch. Du analysierst nur — **ändere keinen Produktiv-Code**. Die einzige Datei, die du
bearbeiten darfst, ist `docs/review/ergebnisse/00-report.md`.

## Vorbereitung

1. Lies `docs/review/00-vorgehensweise.md` vollständig (Kriterienkatalog A–E, Schweregrade,
   Befund-Konvention).
2. Lies `.claude/CLAUDE.md` (Projektregeln, auf die Kriterium B prüft).

## Zu prüfende Dateien (22 — jede einzelne lesen und bewerten)

Prüftiefe **tief** (Kriterien A–D) für C#/Razor; **kurz** (Kriterium E, bei JSON-Configs
zusätzlich C) für CSS/JS/JSON:

1. `src/GlobalSuppressions.cs` — tief
2. `src/PlexToJellyfinSync/Program.cs` — tief
3. `src/PlexToJellyfinSync/Worker.cs` — tief
4. `src/PlexToJellyfinSync/Security/LoginPage.cs` — tief
5. `src/PlexToJellyfinSync/Security/TokenAuthMiddleware.cs` — tief
6. `src/PlexToJellyfinSync/Components/App.razor` — tief
7. `src/PlexToJellyfinSync/Components/Routes.razor` — tief
8. `src/PlexToJellyfinSync/Components/_Imports.razor` — tief
9. `src/PlexToJellyfinSync/Components/Layout/MainLayout.razor` — tief
10. `src/PlexToJellyfinSync/Components/Layout/MainLayout.razor.css` — kurz
11. `src/PlexToJellyfinSync/Components/Layout/ReconnectModal.razor` — tief
12. `src/PlexToJellyfinSync/Components/Layout/ReconnectModal.razor.css` — kurz
13. `src/PlexToJellyfinSync/Components/Layout/ReconnectModal.razor.js` — kurz
14. `src/PlexToJellyfinSync/Components/Pages/Dashboard.razor` — tief
15. `src/PlexToJellyfinSync/Components/Pages/Error.razor` — tief
16. `src/PlexToJellyfinSync/Components/Pages/Logs.razor` — tief
17. `src/PlexToJellyfinSync/Components/Pages/NotFound.razor` — tief
18. `src/PlexToJellyfinSync/wwwroot/app.css` — kurz
19. `src/PlexToJellyfinSync/appsettings.json` — kurz
20. `src/PlexToJellyfinSync/appsettings.Development.json` — kurz
21. `src/PlexToJellyfinSync/Properties/launchSettings.json` — kurz
22. `src/PlexToJellyfinSync/PlexToJellyfinSync.csproj` — kurz

## Schwerpunkte dieser Session

- **C (Security)**: `TokenAuthMiddleware` — timing-sicherer Token-Vergleich
  (`CryptographicOperations.FixedTimeEquals`?), Cookie-Flags (HttpOnly, Secure, SameSite),
  Bypass-Pfade (statische Dateien, Blazor-/SignalR-Endpunkte, `_framework`), Verhalten bei
  deaktiviertem Token. `LoginPage` — Token im HTML/Query-String sichtbar? CSRF?
- **A (Worker)**: BackgroundService-Loop — Exception im Zyklus darf den Worker nicht beenden,
  `CancellationToken`-Behandlung beim Shutdown, Delay-/Intervall-Logik aus `SyncOptions`.
- **A (Program)**: Reihenfolge der Middleware (Auth vor Endpoints?), Options-Bindung und
  -Validierung beim Start, Kestrel-/Forwarded-Headers-Konfiguration für Container-Betrieb.
- **A (Blazor)**: `Dashboard.razor`/`Logs.razor` — Aktualisierung über `InvokeAsync(StateHasChanged)`
  aus fremden Threads? Event-Handler abgemeldet (`IDisposable`)? Memory-Leaks bei
  Circuit-Abbruch?
- **C (Configs)**: Keine echten Tokens/Secrets in `appsettings*.json` und
  `launchSettings.json` (auch keine „Beispiel"-Werte, die echt aussehen).
- **B**: `#region`-Blöcke und XML-Doku in den C#-Dateien; Code-Behind-Stil in Razor-`@code`-Blöcken.
- `.csproj`: keine Paketversionen (CPM); `GlobalSuppressions.cs`: jede Unterdrückung
  mit nachvollziehbarer Begründung?

## Ergebnis festhalten

1. Trage jeden Befund unter `## Befunde → ### Session 6 — Host (Blazor, Worker, Security)` in
   `docs/review/ergebnisse/00-report.md` ein. Befund-IDs: `F-601`, `F-602`, …
   Format gemäß Vorgehensweise (Datei+Zeile, Kriterium, Schweregrad, Beschreibung, Empfehlung).
2. Setze in der Datei-Checkliste den Status aller 22 Dateien dieser Session auf ✅ und trage
   die zugehörigen Befund-IDs in die Spalte „Befunde" ein („keine", falls befundfrei).
3. Entferne den Platzhalter „_Noch nicht durchgeführt._" der Session-Überschrift.

## Abschluss

1. Selbstkontrolle: Sind alle 22 Dateien aus der Liste oben gelesen, bewertet und in der
   Checkliste auf ✅? Falls nein, nacharbeiten.
2. Zeige mir eine kurze Zusammenfassung der Befunde.
3. Frage mich um Bestätigung und committe erst danach
   (Commit-Message: `Review Session 6: Host`) und pushe den aktuellen Branch.
