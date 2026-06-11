# Vorgehensweise: Vollständige Repo-Analyse PlexToJellyfinSync

Dieses Dokument definiert das Vorgehen, die Kriterien und die Konventionen für ein vollständiges
Review aller **107 versionierten Dateien** dieses Repositories. Es wird von den Session-Prompts
unter `docs/review/prompts/` referenziert.

## Ziel

Jede versionierte Datei wird genau einmal geprüft — keine wird ausgelassen. C#- und Razor-Dateien
werden **tief** geprüft (Kriterien A–D), Nicht-Code-Dateien (Configs, Workflows, Doku, CSS, JSON)
bekommen eine **Kurzprüfung** (Kriterium E, bei Configs/Workflows zusätzlich C). Alle Befunde und
der Prüfstatus jeder Datei werden zentral in `docs/review/ergebnisse/00-report.md` festgehalten.

## Ablauf

Das Review ist in **8 unabhängig ausführbare Sessions** entlang des Datenflusses aufgeteilt
(Core → Data → Service → Host → Tests → Infrastruktur). Pro Session existiert ein eigenständiger
Prompt unter `docs/review/prompts/`, der manuell in einer neuen Claude-Code-Session ausgeführt wird:

| Session | Prompt-Datei | Umfang | Dateien |
|---|---|---|---|
| 1 | `session-1-core.md` | Core: Abstractions, Enums, Models, Options | 24 |
| 2 | `session-2-data.md` | Data: Plex-DTOs | 14 |
| 3 | `session-3-service-plex.md` | Service I: PlexClient, WatchAggregator, PathMapper | 4 |
| 4 | `session-4-service-sync.md` | Service II: NfoWriter, SyncOrchestrator, State, Status | 5 |
| 5 | `session-5-service-infra.md` | Service III: Logging, DI-Registrierung | 5 |
| 6 | `session-6-host.md` | Host: Program, Worker, Security, Blazor-Komponenten | 22 |
| 7 | `session-7-tests.md` | Tests: MSTest-Klassen | 6 |
| 8 | `session-8-infrastruktur.md` | Infrastruktur: Root, `.github/`, `.claude/` | 27 |

Summe: **107 Dateien** (24 + 14 + 4 + 5 + 5 + 22 + 6 + 27).

Die Sessions können in beliebiger Reihenfolge ausgeführt werden; die empfohlene Reihenfolge ist
1 → 8, da spätere Sessions vom Kontextwissen der Schichten darunter profitieren.

## Kriterienkatalog

### A — Korrektheit & Bugs

- Logikfehler und Randfälle: `null`, leere Listen, fehlende oder unerwartete Plex-Felder,
  ungültige/nicht gemappte Pfade.
- Fehlerbehandlung: geschluckte Exceptions, zu breite `catch`-Blöcke, fehlende Fehlerpfade,
  Verhalten bei nicht erreichbarem Plex-Server.
- Async-Korrektheit: `.ConfigureAwait(false)` in Service-/Data-Code, durchgereichte
  `CancellationToken`, kein `async void`, kein blockierendes `.Result`/`.Wait()`.
- Ressourcen: `IDisposable`/`await using`, HttpClient-Lebenszyklus, Datei-Handles beim
  NFO-/State-Schreiben.
- Nebenläufigkeit: Thread-Sicherheit von Zuständen, auf die Worker (schreibend) und
  Blazor-Circuits (lesend) parallel zugreifen (StateStore, InMemoryLogStore, SyncStatusService).
- Datenintegrität: State-Datei atomar geschrieben? Bestehende NFO-Inhalte beim Schreiben erhalten?

### B — Regel-/Stil-Konformität (gemäß `.claude/CLAUDE.md`)

- `#region`-Blöcke um alle Member, gruppiert nach Member-Art (`Constants`, `Fields`,
  `Constructors`, `Properties`, `Events`, `Methods`, …); Interface-Regionen nach dem Interface
  benannt, Beschreibung endet nicht auf „implementation".
- XML-Dokumentation auf allen Membern, Englisch, kein `<remarks>`.
- `== false` statt `!`, `is null` / `is not null`, `var` bevorzugt, language keywords statt
  BCL-Typen, ausschließlich LINQ-Methodensyntax.
- Keine primary constructors; Constructor-Injection mit `_camelCase`-readonly-Feldern.
- File-scoped namespaces, ein Top-Level-Typ pro Datei, `using` außerhalb des Namespace
  (System zuerst), Allman-Klammern, 4 Leerzeichen, CRLF, kein abschließender Zeilenumbruch.
- Paketversionen ausschließlich in `Directory.Packages.props` (Central Package Management),
  keine Versionsnummern in `.csproj`-Dateien.
- Tests: nur MSTest, Klassennamen `{Feature}Tests`, Methodennamen
  `{Class}{Scenario}{ExpectedResult}` in PascalCase **ohne Unterstriche**, jede Assertion mit
  Assert-Message.

### C — Sicherheit

- Plex-Token: Leaks in Logs, Anzeige im Dashboard, Übertragung (Header vs. Query-String),
  Token in Fehlermeldungen/Exceptions.
- `TokenAuthMiddleware` / `LoginPage`: timing-sicherer Vergleich, Cookie-Flags (HttpOnly,
  Secure, SameSite), Bypass-Pfade (statische Dateien, Reconnect-Endpunkte).
- Pfad-Sicherheit: `PathMapper`/`NfoWriter` gegen Path-Traversal; kein Schreiben außerhalb der
  gemappten Library-Wurzeln.
- Keine Secrets in `appsettings*.json`, `launchSettings.json`, Workflows, Dockerfile.
- Dockerfile: Non-Root-User, Basis-Image-Pinning; GitHub-Workflows: minimale `permissions:`,
  Pinning der Actions (SHA oder zumindest Major-Version).

### D — Architektur & Design

- Schichtentrennung eingehalten: Core ← Data ← Service ← Host, keine Rückwärts-Referenzen.
- Abstraktionen sinnvoll geschnitten; DI-Registrierung vollständig, Lifetimes korrekt
  (Singleton vs. Scoped vs. Transient, besonders im Zusammenspiel Worker/Blazor).
- Verantwortlichkeiten: tun große Klassen (z.B. `SyncOrchestrator`) zu viel? Testbarkeit.
- Options-Pattern: Validierung beim Start, `IOptionsMonitor` wo Reload sinnvoll wäre.
- Testlücken dokumentieren (welche Service-Klassen sind ungetestet?) — **keine** neuen Tests
  im Rahmen des Reviews schreiben.

### E — Nicht-Code-Dateien (Kurzprüfung)

- Konsistenz: `.claude/CLAUDE.md` ↔ `.github/copilot-instructions.md` synchron?
  README ↔ tatsächliche Optionen/Konfiguration? `.editorconfig` ↔ dokumentierter Stil
  (CRLF, 4 Leerzeichen)?
- CI-/Release-Workflows: bauen und testen sie das, was dokumentiert ist
  (`PlexToJellyfinSync.slnx`, Release-Konfiguration)?
- Plausibilität: `.gitignore`/`.dockerignore` passend, Rulesets konsistent zu den
  Reihitsu-Regeln, dependabot-Konfiguration sinnvoll, Issue-Templates aktuell.

## Schweregrade

| Stufe | Bedeutung |
|---|---|
| 🔴 Kritisch | Datenverlust, Sicherheitslücke, Sync schreibt falsche Watch-States |
| 🟠 Hoch | Fehlverhalten in realistischen Szenarien, ernste Robustheitslücke |
| 🟡 Mittel | Fehler in Randfällen, klare Regelverstöße gegen CLAUDE.md |
| 🔵 Niedrig | Kleinere Stil-/Konsistenzabweichungen, Verbesserungspotenzial |
| ⚪ Hinweis | Beobachtung ohne Handlungsdruck, Doku-/Konsistenzanmerkung |

## Befund-Konvention

- Befund-IDs: `F-{Session}{laufende Nr. zweistellig}`, z.B. `F-101` (Session 1, Befund 1),
  `F-403` (Session 4, Befund 3).
- Jeder Befund enthält: **Datei + Zeile(n)**, **Kriterium** (A–E), **Schweregrad**,
  **Beschreibung**, **Empfehlung**.
- Das Review ist **rein analytisch**: Es wird kein Produktiv-Code geändert. Einzige erlaubte
  Schreiboperation ist die Pflege von `docs/review/ergebnisse/00-report.md`.

## Vollständigkeitsnachweis

Nach Abschluss aller 8 Sessions müssen in der Checkliste in
`docs/review/ergebnisse/00-report.md` alle 107 Dateien den Status ✅ haben. Abschlussprüfung:

```bash
git ls-files | sort > /tmp/soll.txt
# Pfade aus der Checkliste extrahieren (Spalte „Datei") und gegen /tmp/soll.txt diffen
```

Der Diff muss leer sein. Dateien, die nach Erstellung dieses Frameworks neu ins Repo kommen
(inkl. der Review-Dateien unter `docs/review/` selbst), werden bei der Abschlussprüfung der
letzten Session in die Checkliste aufgenommen und mit geprüft oder explizit als
„außerhalb des Review-Stichtags" markiert.
