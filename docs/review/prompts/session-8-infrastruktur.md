# Review-Session 8: Infrastruktur (Root, .github/, .claude/)

Führe eine Kurzprüfung der unten gelisteten 27 Infrastruktur-Dateien durch und schließe das
Gesamt-Review mit dem Vollständigkeitsnachweis ab. Du analysierst nur — **ändere keine der
geprüften Dateien**. Die einzige Datei, die du bearbeiten darfst, ist
`docs/review/ergebnisse/00-report.md`.

## Vorbereitung

1. Lies `docs/review/00-vorgehensweise.md` vollständig (Kriterienkatalog A–E, Schweregrade,
   Befund-Konvention).
2. Diese Session ist idealerweise die **letzte** — sie enthält den Vollständigkeitsnachweis
   über alle Sessions.

## Zu prüfende Dateien (27 — jede einzelne lesen und bewerten)

Prüftiefe **kurz** (Kriterium E; bei Dockerfile, Workflows und Configs zusätzlich C):

1. `.claude/CLAUDE.md`
2. `.claude/hooks/sonar-secrets/build-scripts/pretool-secrets.ps1`
3. `.claude/hooks/sonar-secrets/build-scripts/prompt-secrets.ps1`
4. `.claude/settings.json`
5. `.claude/skills/publish-pr/prompt.md`
6. `.dockerignore`
7. `.editorconfig`
8. `.gitattributes`
9. `.gitignore`
10. `.github/ISSUE_TEMPLATE/bug_report.md`
11. `.github/ISSUE_TEMPLATE/feature_request.md`
12. `.github/copilot-instructions.md`
13. `.github/dependabot.yml`
14. `.github/skills/create-pr/SKILL.md`
15. `.github/skills/publish-pr/SKILL.md`
16. `.github/workflows/ci.yml`
17. `.github/workflows/codeql.yml`
18. `.github/workflows/release.yml`
19. `Directory.Build.props`
20. `Directory.Packages.props`
21. `Dockerfile`
22. `LICENSE.md`
23. `PlexToJellyfinSync.Debug.ruleset`
24. `PlexToJellyfinSync.Release.ruleset`
25. `PlexToJellyfinSync.slnx`
26. `README.md`
27. `SECURITY.md`

## Schwerpunkte dieser Session

- **E (Konsistenz)**: `.claude/CLAUDE.md` ↔ `.github/copilot-instructions.md` — sind beide
  synchron (das fordert CLAUDE.md selbst)? README ↔ tatsächliche Optionen in
  `src/PlexToJellyfinSync.Core/Options/` und `appsettings.json`? `.editorconfig` ↔
  dokumentierter Stil (CRLF, 4 Leerzeichen, kein abschließender Zeilenumbruch)?
  Skill-Dateien `.claude/skills/publish-pr/` ↔ `.github/skills/publish-pr/` deckungsgleich?
- **E (Build/CI)**: Bauen `ci.yml`/`release.yml` das, was dokumentiert ist
  (`PlexToJellyfinSync.slnx`, Release-Konfiguration, Tests)? Stimmen .NET-Versionen in
  Workflows, `Directory.Build.props` und Dockerfile überein? `Directory.Packages.props`:
  zentrale Versionen vollständig, keine offensichtlich veralteten Pakete?
- **C (Dockerfile)**: Non-Root-User, gepinntes Basis-Image, keine Secrets in Build-Args/Layern.
- **C (Workflows)**: minimale `permissions:`, Actions gepinnt (SHA oder Major-Version),
  keine Secrets im Klartext. **C (PowerShell-Hooks)**: keine eingebetteten Zugangsdaten.
- **E (Plausibilität)**: `.gitignore`/`.dockerignore` decken Build-Artefakte ab; Rulesets
  Debug/Release konsistent zueinander; dependabot-Konfiguration deckt NuGet und
  GitHub Actions ab; Issue-Templates und SECURITY.md aktuell.

## Ergebnis festhalten

1. Trage jeden Befund unter `## Befunde → ### Session 8 — Infrastruktur` in
   `docs/review/ergebnisse/00-report.md` ein. Befund-IDs: `F-801`, `F-802`, …
   Format gemäß Vorgehensweise (Datei+Zeile, Kriterium, Schweregrad, Beschreibung, Empfehlung).
2. Setze in der Datei-Checkliste den Status aller 27 Dateien dieser Session auf ✅ und trage
   die zugehörigen Befund-IDs in die Spalte „Befunde" ein („keine", falls befundfrei).
3. Entferne den Platzhalter „_Noch nicht durchgeführt._" der Session-Überschrift.

## Abschluss des Gesamt-Reviews (Vollständigkeitsnachweis)

1. Prüfe per `git ls-files`, ob **alle** versionierten Dateien in der Checkliste stehen.
   Dateien, die seit Erstellung des Review-Frameworks hinzugekommen sind (inkl. der Dateien
   unter `docs/review/` selbst), in die Checkliste aufnehmen und entweder kurz mitprüfen
   oder als „außerhalb des Review-Stichtags" markieren.
2. Sind noch Sessions offen (Status ⬜ in der Checkliste)? Dann im Report vermerken, welche.
3. Wenn alle 8 Sessions abgeschlossen sind: Fülle die Tabelle unter `## Zusammenfassung`
   (Befundanzahl je Schweregrad) und den Abschnitt `## Vollständigkeitsnachweis` aus und
   schreibe 3–5 Sätze Gesamteinschätzung.
4. Selbstkontrolle: Sind alle 27 Dateien aus der Liste oben gelesen, bewertet und in der
   Checkliste auf ✅? Falls nein, nacharbeiten.
5. Zeige mir eine kurze Zusammenfassung der Befunde.
6. Frage mich um Bestätigung und committe erst danach
   (Commit-Message: `Review Session 8: Infrastruktur + Abschluss`) und pushe den
   aktuellen Branch.
