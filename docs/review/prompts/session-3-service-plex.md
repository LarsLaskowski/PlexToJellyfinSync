# Review Session 3: Service I (PlexClient, WatchAggregator, PathMapper)

Perform a code review of the 4 files listed below from the `PlexToJellyfinSync.Service` project.
You only analyze — **do not change any production code**. The only file you may edit is
`docs/review/results/00-report.md`.

## Preparation

1. Read `docs/review/00-process.md` in full (criteria catalog A–E, severity levels,
   finding convention).
2. Read `.claude/CLAUDE.md` (project rules that criterion B checks against).
3. Context: The associated interfaces live in `src/PlexToJellyfinSync.Core/Abstractions/`,
   the DTOs in `src/PlexToJellyfinSync.Data/Plex/`. Read them as needed.

## Files to Review (4 — read and assess each one)

Review depth **deep** (criteria A–D):

1. `src/PlexToJellyfinSync.Service/PlexClient.cs`
2. `src/PlexToJellyfinSync.Service/PlexJsonOptions.cs`
3. `src/PlexToJellyfinSync.Service/WatchAggregator.cs`
4. `src/PlexToJellyfinSync.Service/PathMapper.cs`

## Focus Areas for This Session

- **A (PlexClient)**: HttpClient usage (lifecycle, timeouts), behavior on HTTP
  errors/timeouts/invalid JSON, `.ConfigureAwait(false)` on every `await`,
  `CancellationToken` throughout, pagination/large libraries.
- **C (PlexClient)**: How is the Plex token transmitted (header `X-Plex-Token` vs.
  query string)? Does the token appear in log messages, exceptions, or URLs?
- **A (WatchAggregator)**: Aggregation logic across episodes/seasons — edge cases: empty
  history, items watched multiple times, missing `viewCount`/`lastViewedAt` values, time-zone
  and Unix-timestamp conversion.
- **A/C (PathMapper)**: Mapping Plex path → Jellyfin path: separators Windows/Linux,
  casing, longest-prefix matching, behavior for unmapped paths;
  path traversal (`..` in the source path must not escape the target root).
- **B**: `#region` blocks, XML docs, `== false`, `is null`, `_camelCase` readonly fields,
  LINQ method syntax.
- **D**: Testability; cross-check with existing tests (`PathMapperTests`,
  `WatchAggregatorTests`) — which logic is untested?

## Record Results

1. Record each finding under `## Findings → ### Session 3 — Service I (Plex Integration)` in
   `docs/review/results/00-report.md`. Finding IDs: `F-301`, `F-302`, …
   Format per the process doc (file+line, criterion, severity, description, recommendation).
2. In the file checklist, set the status of all 4 files of this session to ✅ and enter the
   associated finding IDs in the "Findings" column ("none" if there are no findings).
3. Remove the "_Not yet performed._" placeholder under the session heading.

## Wrap-up

1. Self-check: Have all 4 files in the list above been read, assessed, and marked ✅ in the
   checklist? If not, follow up.
2. Show me a short summary of the findings.
3. Ask me for confirmation and only then commit
   (commit message: `Review Session 3: Service I (Plex Integration)`) and push the current branch.
