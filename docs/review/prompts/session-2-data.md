# Review Session 2: Data (Plex DTOs)

Perform a code review of the 14 files listed below from the `PlexToJellyfinSync.Data` project.
You only analyze — **do not change any production code**. The only file you may edit is
`docs/review/results/00-report.md`.

## Preparation

1. Read `docs/review/00-process.md` in full (criteria catalog A–E, severity levels,
   finding convention).
2. Read `.claude/CLAUDE.md` (project rules that criterion B checks against).
3. Context: These DTOs are deserialized by `src/PlexToJellyfinSync.Service/PlexClient.cs` with
   `System.Text.Json` (options in `PlexJsonOptions.cs`). Read both files as needed.

## Files to Review (14 — read and assess each one)

Review depth **deep** (criteria A–D), `.csproj` **quick** (criterion E + CPM check):

1. `src/PlexToJellyfinSync.Data/Plex/PlexAccount.cs`
2. `src/PlexToJellyfinSync.Data/Plex/PlexAccountsContainer.cs`
3. `src/PlexToJellyfinSync.Data/Plex/PlexAccountsResponse.cs`
4. `src/PlexToJellyfinSync.Data/Plex/PlexDirectory.cs`
5. `src/PlexToJellyfinSync.Data/Plex/PlexGuid.cs`
6. `src/PlexToJellyfinSync.Data/Plex/PlexLibrariesContainer.cs`
7. `src/PlexToJellyfinSync.Data/Plex/PlexLibrariesResponse.cs`
8. `src/PlexToJellyfinSync.Data/Plex/PlexMedia.cs`
9. `src/PlexToJellyfinSync.Data/Plex/PlexMetadata.cs`
10. `src/PlexToJellyfinSync.Data/Plex/PlexMetadataContainer.cs`
11. `src/PlexToJellyfinSync.Data/Plex/PlexMetadataResponse.cs`
12. `src/PlexToJellyfinSync.Data/Plex/PlexPart.cs`
13. `src/PlexToJellyfinSync.Data/Plex/PlexTag.cs`
14. `src/PlexToJellyfinSync.Data/PlexToJellyfinSync.Data.csproj`

## Focus Areas for This Session

- **A**: Nullability vs. real Plex responses — which fields can be missing? Do the
  `JsonPropertyName` attributes match the Plex API (casing)? Correct types for timestamps
  (`viewedAt`, `lastViewedAt`), counters (`viewCount`) and IDs
  (`ratingKey` as string vs. int)?
- **B**: `#region` blocks and XML docs on DTO properties too; file-scoped namespaces;
  one type per file.
- **D**: Does Data reference only Core (or nothing) — no Service/Host dependency?
  Are the container/response wrappers built consistently?
- `.csproj`: no package versions (CPM), Reihitsu.Analyzer wired in.

## Record Results

1. Record each finding under `## Findings → ### Session 2 — Data` in
   `docs/review/results/00-report.md`. Finding IDs: `F-201`, `F-202`, …
   Format per the process doc (file+line, criterion, severity, description, recommendation).
2. In the file checklist, set the status of all 14 files of this session to ✅ and enter the
   associated finding IDs in the "Findings" column ("none" if there are no findings).
3. Remove the "_Not yet performed._" placeholder under the session heading.

## Wrap-up

1. Self-check: Have all 14 files in the list above been read, assessed, and marked ✅ in the
   checklist? If not, follow up.
2. Show me a short summary of the findings.
3. Ask me for confirmation and only then commit
   (commit message: `Review Session 2: Data`) and push the current branch.
