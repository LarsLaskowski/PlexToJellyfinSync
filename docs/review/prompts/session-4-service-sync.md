# Review Session 4: Service II (NfoWriter, SyncOrchestrator, State, Status)

Perform a code review of the 5 files listed below from the `PlexToJellyfinSync.Service` project.
These are the two largest classes in the repo (`SyncOrchestrator` ~420 lines, `NfoWriter`
~385 lines) — plan for appropriate care. You only analyze — **do not change any production
code**. The only file you may edit is `docs/review/results/00-report.md`.

## Preparation

1. Read `docs/review/00-process.md` in full (criteria catalog A–E, severity levels,
   finding convention).
2. Read `.claude/CLAUDE.md` (project rules that criterion B checks against).
3. Context: Interfaces in `src/PlexToJellyfinSync.Core/Abstractions/`, options in
   `src/PlexToJellyfinSync.Core/Options/`, the caller is `src/PlexToJellyfinSync/Worker.cs`.
   Read them as needed.

## Files to Review (5 — read and assess each one)

Review depth **deep** (criteria A–D):

1. `src/PlexToJellyfinSync.Service/NfoWriter.cs`
2. `src/PlexToJellyfinSync.Service/SyncOrchestrator.cs`
3. `src/PlexToJellyfinSync.Service/State/StateStore.cs`
4. `src/PlexToJellyfinSync.Service/State/SyncStateFile.cs`
5. `src/PlexToJellyfinSync.Service/SyncStatusService.cs`

## Focus Areas for This Session

- **A (NfoWriter)**: Existing `.nfo` content must be preserved (only change watch fields) —
  XML-parsing robustness against broken/foreign NFOs, encoding/BOM, atomic
  writing (temp + rename?), behavior when the target file is missing, `MovieNfoFilenameStrategy`
  correctly implemented.
- **C (NfoWriter/PathMapper interplay)**: Can a manipulated Plex path lead to writing
  outside the library root?
- **A (SyncOrchestrator)**: Overall flow of a sync cycle — an error in one item must not abort
  the cycle; `CancellationToken` everywhere; `.ConfigureAwait(false)`;
  idempotency (a second run without changes = no writes).
- **A (StateStore/SyncStateFile)**: Atomic persistence, behavior with a corrupt
  state file, thread safety (Worker writes, dashboard reads), migration/version strategy of the
  file format.
- **A (SyncStatusService)**: Thread safety of the status data that Blazor circuits read in
  parallel while the Worker updates it.
- **B**: `#region` blocks, XML docs, `== false`, `is null`, LINQ method syntax — check
  especially thoroughly in large classes.
- **D**: Does `SyncOrchestrator` do too much (god class)? Document test gaps: do none of these
  five classes except `NfoWriter` have tests?

## Record Results

1. Record each finding under `## Findings → ### Session 4 — Service II (Sync & Persistence)` in
   `docs/review/results/00-report.md`. Finding IDs: `F-401`, `F-402`, …
   Format per the process doc (file+line, criterion, severity, description, recommendation).
2. In the file checklist, set the status of all 5 files of this session to ✅ and enter the
   associated finding IDs in the "Findings" column ("none" if there are no findings).
3. Remove the "_Not yet performed._" placeholder under the session heading.

## Wrap-up

1. Self-check: Have all 5 files in the list above been read, assessed, and marked ✅ in the
   checklist? If not, follow up.
2. Show me a short summary of the findings.
3. Ask me for confirmation and only then commit
   (commit message: `Review Session 4: Service II (Sync & Persistence)`) and push the
   current branch.
