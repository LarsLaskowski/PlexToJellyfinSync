# Review Session 8: Infrastructure (Root, .github/, .claude/)

Perform a quick review of the 27 infrastructure files listed below and close out the overall
review with the completeness proof. You only analyze — **do not change any of the reviewed
files**. The only file you may edit is `docs/review/results/00-report.md`.

## Preparation

1. Read `docs/review/00-process.md` in full (criteria catalog A–E, severity levels,
   finding convention).
2. This session is ideally the **last** — it contains the completeness proof across all
   sessions.

## Files to Review (27 — read and assess each one)

Review depth **quick** (criterion E; for the Dockerfile, workflows, and configs additionally C):

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

## Focus Areas for This Session

- **E (Consistency)**: `.claude/CLAUDE.md` ↔ `.github/copilot-instructions.md` — are both
  in sync (CLAUDE.md itself requires this)? README ↔ actual options in
  `src/PlexToJellyfinSync.Core/Options/` and `appsettings.json`? `.editorconfig` ↔
  documented style (CRLF, 4 spaces, no trailing newline)?
  Skill files `.claude/skills/publish-pr/` ↔ `.github/skills/publish-pr/` matching?
- **E (Build/CI)**: Do `ci.yml`/`release.yml` build what is documented
  (`PlexToJellyfinSync.slnx`, Release configuration, tests)? Do the .NET versions in
  the workflows, `Directory.Build.props`, and Dockerfile match? `Directory.Packages.props`:
  central versions complete, no obviously outdated packages?
- **C (Dockerfile)**: Non-root user, pinned base image, no secrets in build args/layers.
- **C (Workflows)**: minimal `permissions:`, actions pinned (SHA or major version),
  no secrets in cleartext. **C (PowerShell hooks)**: no embedded credentials.
- **E (Plausibility)**: `.gitignore`/`.dockerignore` cover build artifacts; rulesets
  Debug/Release consistent with each other; dependabot configuration covers NuGet and
  GitHub Actions; issue templates and SECURITY.md current.

## Record Results

1. Record each finding under `## Findings → ### Session 8 — Infrastructure` in
   `docs/review/results/00-report.md`. Finding IDs: `F-801`, `F-802`, …
   Format per the process doc (file+line, criterion, severity, description, recommendation).
2. In the file checklist, set the status of all 27 files of this session to ✅ and enter the
   associated finding IDs in the "Findings" column ("none" if there are no findings).
3. Remove the "_Not yet performed._" placeholder under the session heading.

## Closing the Overall Review (Completeness Proof)

1. Use `git ls-files` to check whether **all** version-controlled files are in the checklist.
   Add files that were added since the review framework was created (including the files
   under `docs/review/` themselves) to the checklist and either review them briefly
   or mark them as "outside the review baseline".
2. Are any sessions still open (status ⬜ in the checklist)? If so, note in the report which.
3. When all 8 sessions are complete: fill in the table under `## Summary`
   (finding count per severity) and the `## Completeness Proof` section, and
   write 3–5 sentences of overall assessment.
4. Self-check: Have all 27 files in the list above been read, assessed, and marked ✅ in the
   checklist? If not, follow up.
5. Show me a short summary of the findings.
6. Ask me for confirmation and only then commit
   (commit message: `Review Session 8: Infrastructure + Closeout`) and push the
   current branch.
