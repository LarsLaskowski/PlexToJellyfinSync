# Review Session 6: Host (Program, Worker, Security, Blazor Components)

Perform a code review of the 22 files listed below from the host project `PlexToJellyfinSync`.
You only analyze — **do not change any production code**. The only file you may edit is
`docs/review/results/00-report.md`.

## Preparation

1. Read `docs/review/00-process.md` in full (criteria catalog A–E, severity levels,
   finding convention).
2. Read `.claude/CLAUDE.md` (project rules that criterion B checks against).

## Files to Review (22 — read and assess each one)

Review depth **deep** (criteria A–D) for C#/Razor; **quick** (criterion E, plus C for JSON
configs) for CSS/JS/JSON:

1. `src/GlobalSuppressions.cs` — deep
2. `src/PlexToJellyfinSync/Program.cs` — deep
3. `src/PlexToJellyfinSync/Worker.cs` — deep
4. `src/PlexToJellyfinSync/Security/LoginPage.cs` — deep
5. `src/PlexToJellyfinSync/Security/TokenAuthMiddleware.cs` — deep
6. `src/PlexToJellyfinSync/Components/App.razor` — deep
7. `src/PlexToJellyfinSync/Components/Routes.razor` — deep
8. `src/PlexToJellyfinSync/Components/_Imports.razor` — deep
9. `src/PlexToJellyfinSync/Components/Layout/MainLayout.razor` — deep
10. `src/PlexToJellyfinSync/Components/Layout/MainLayout.razor.css` — quick
11. `src/PlexToJellyfinSync/Components/Layout/ReconnectModal.razor` — deep
12. `src/PlexToJellyfinSync/Components/Layout/ReconnectModal.razor.css` — quick
13. `src/PlexToJellyfinSync/Components/Layout/ReconnectModal.razor.js` — quick
14. `src/PlexToJellyfinSync/Components/Pages/Dashboard.razor` — deep
15. `src/PlexToJellyfinSync/Components/Pages/Error.razor` — deep
16. `src/PlexToJellyfinSync/Components/Pages/Logs.razor` — deep
17. `src/PlexToJellyfinSync/Components/Pages/NotFound.razor` — deep
18. `src/PlexToJellyfinSync/wwwroot/app.css` — quick
19. `src/PlexToJellyfinSync/appsettings.json` — quick
20. `src/PlexToJellyfinSync/appsettings.Development.json` — quick
21. `src/PlexToJellyfinSync/Properties/launchSettings.json` — quick
22. `src/PlexToJellyfinSync/PlexToJellyfinSync.csproj` — quick

## Focus Areas for This Session

- **C (Security)**: `TokenAuthMiddleware` — timing-safe token comparison
  (`CryptographicOperations.FixedTimeEquals`?), cookie flags (HttpOnly, Secure, SameSite),
  bypass paths (static files, Blazor/SignalR endpoints, `_framework`), behavior when the
  token is disabled. `LoginPage` — token visible in HTML/query string? CSRF?
- **A (Worker)**: BackgroundService loop — an exception in the cycle must not terminate the
  Worker, `CancellationToken` handling on shutdown, delay/interval logic from `SyncOptions`.
- **A (Program)**: Middleware order (auth before endpoints?), options binding and
  validation at startup, Kestrel/forwarded-headers configuration for container operation.
- **A (Blazor)**: `Dashboard.razor`/`Logs.razor` — updates via `InvokeAsync(StateHasChanged)`
  from foreign threads? Event handlers unsubscribed (`IDisposable`)? Memory leaks on
  circuit teardown?
- **C (Configs)**: No real tokens/secrets in `appsettings*.json` and
  `launchSettings.json` (also no "example" values that look real).
- **B**: `#region` blocks and XML docs in the C# files; code-behind style in Razor `@code` blocks.
- `.csproj`: no package versions (CPM); `GlobalSuppressions.cs`: each suppression
  with a traceable justification?

## Record Results

1. Record each finding under `## Findings → ### Session 6 — Host (Blazor, Worker, Security)` in
   `docs/review/results/00-report.md`. Finding IDs: `F-601`, `F-602`, …
   Format per the process doc (file+line, criterion, severity, description, recommendation).
2. In the file checklist, set the status of all 22 files of this session to ✅ and enter the
   associated finding IDs in the "Findings" column ("none" if there are no findings).
3. Remove the "_Not yet performed._" placeholder under the session heading.

## Wrap-up

1. Self-check: Have all 22 files in the list above been read, assessed, and marked ✅ in the
   checklist? If not, follow up.
2. Show me a short summary of the findings.
3. Ask me for confirmation and only then commit
   (commit message: `Review Session 6: Host`) and push the current branch.
