# Tasks: Migrate the solution to .NET 11

Plan: [plan.md](plan.md)

| # | Group | Task | Files | Criteria | Depends on | Owner | Done |
| - | ----- | ---- | ----- | -------- | ---------- | ----- | ---- |
| 1 | G1 | Skeleton: no new API; target `net11.0`, SDK 11.0.100, packages 11.0.0, remove the Logging.Abstractions reference and its `PackageVersion` (afterwards the old test calls report SYSLIB0066) | `src/*/*.csproj`, `tests/PlexToJellyfinSync.Tests/PlexToJellyfinSync.Tests.csproj`, `global.json`, `Directory.Packages.props` | AC1, AC5 | — | Dev | [x] |
| 2 | G1 | Adapt the startup-validation tests to `IAsyncStartupValidator` | `tests/PlexToJellyfinSync.Tests/ServiceCollectionExtensionsTests.cs` | AC2 | 1 | Tester | [x] |
| 3 | G2 | Skeleton: no new API | — | AC3 | 2 | Dev | [x] |
| 4 | G2 | Dockerfile images and index digest; run the AC3 checks | `Dockerfile` | AC3 | 3 | Dev | [x] |
| 5 | G3 | Skeleton: no new API | — | AC4 | 4 | Dev | [x] |
| 6 | G3 | Documentation, Dependabot comments and the `.squad/stack.md` text, then the AC4 sweep and `config-check.py` | `README.md`, `docs/CONTRIBUTING.md`, `docs/ARCHITECTURE.md`, `AGENTS.md`, `.github/copilot-instructions.md`, `.github/dependabot.yml`, `.squad/stack.md` (text reported, written by the Orchestrator) | AC4 | 5 | Dev | [x] |
