# Review Report: Full Repository Analysis of PlexToJellyfinSync

Central results file of the review. For process and criteria, see
[`docs/review/00-process.md`](../00-process.md). Each review session records its findings under its
heading and sets the status of the reviewed files in the checklist to ✅.

## Summary

_Filled in after all sessions are complete._

| Severity | Count |
|---|---|
| 🔴 Critical | – |
| 🟠 High | – |
| 🟡 Medium | – |
| 🔵 Low | – |
| ⚪ Note | – |

## Findings

Format per finding:

> ### F-XYZ — Short title
> **File:** `path/to/file.cs:line` · **Criterion:** A–E · **Severity:** 🔴/🟠/🟡/🔵/⚪
>
> Description of the problem.
>
> **Recommendation:** Proposed solution.

### Session 1 — Core

The Core project is clean with respect to criterion B: every type uses a file-scoped namespace,
holds exactly one top-level type, wraps its members in `#region` blocks grouped by member kind, and
carries English XML documentation without `<remarks>`. `using` directives sit outside the namespace
and `ImplicitUsings` covers the `System` imports. The `.csproj` is correct under CPM (no inline
package versions) and the Reihitsu.Analyzer is wired in centrally via `Directory.Build.props`. The
abstractions are sensibly scoped and Core references no sibling layer (Data/Service/Host); its only
external coupling is `Microsoft.Extensions.Logging.Abstractions` (see F-104). The findings below
concern options validatability (D), value semantics of the models (A), a security default (C) and a
build-reproducibility setting (E).

#### F-101 — Options classes are not self-validating; required fields and value ranges are unguarded

> **File:** `src/PlexToJellyfinSync.Core/Options/PlexOptions.cs:22-27`,
> `src/PlexToJellyfinSync.Core/Options/SyncOptions.cs:22-27`,
> `src/PlexToJellyfinSync.Core/Options/StateOptions.cs:22`,
> `src/PlexToJellyfinSync.Core/Options/DashboardOptions.cs:32` · **Criterion:** D · **Severity:** 🟡 Medium
>
> The options types carry no way to distinguish required from optional settings and no value-range
> constraints. `PlexOptions.BaseUrl` and `PlexOptions.Token` are de-facto mandatory but default to
> `string.Empty`, so a missing configuration is only discovered when the first HTTP call fails rather
> than at startup. Numeric settings (`SyncOptions.PollIntervalSeconds`,
> `SyncOptions.FullReconcileIntervalHours`, `DashboardOptions.LogBufferSize`) accept zero or negative
> values that would break the polling timer or the ring buffer. `StateOptions.Directory` is likewise
> unvalidated. There are no DataAnnotations and the process doc (criterion D) explicitly asks for
> options that are validatable at startup.
>
> **Recommendation:** Annotate mandatory/ranged members with DataAnnotations
> (`[Required]`, `[Url]`, `[Range(1, …)]`) and have the host register them with
> `ValidateDataAnnotations().ValidateOnStart()` (the wiring itself belongs to Session 6 / Host). At
> minimum, document and enforce minimum values for the numeric settings.

#### F-102 — Domain models used as value carriers lack value equality

> **File:** `src/PlexToJellyfinSync.Core/Models/UniqueId.cs:6`,
> `src/PlexToJellyfinSync.Core/Models/WatchInfo.cs:6`,
> `src/PlexToJellyfinSync.Core/Models/MediaItem.cs:8` · **Criterion:** A · **Severity:** 🔵 Low
>
> `UniqueId`, `WatchInfo` and `MediaItem` are mutable `sealed class`es without `Equals`/`GetHashCode`,
> so they compare by reference. If any consumer compares a `WatchInfo` (or the `UniqueIds` list)
> against a previously written value to decide whether the NFO content changed — a natural pattern for
> the "no change required → Skipped" path in `NfoWriteOutcome` — reference equality will report a
> difference even when the values are identical, causing redundant writes (or, for set/dictionary use,
> subtle lookup bugs). This needs confirmation against the Service layer (Sessions 3–4).
>
> **Recommendation:** If these types are ever compared by value, give them structural equality (e.g.
> convert `UniqueId`/`WatchInfo` to `record class` with `set`/`init` accessors, or implement
> `Equals`/`GetHashCode`). If they are only ever data carriers, leave them as-is — the point is to make
> the intent explicit so a value comparison is not introduced later by accident.

#### F-103 — Dashboard is publicly reachable by default (empty token)

> **File:** `src/PlexToJellyfinSync.Core/Options/DashboardOptions.cs:27` · **Criterion:** C · **Severity:** 🔵 Low
>
> `Token` defaults to `string.Empty`, and the XML doc states that an empty token means the dashboard
> is publicly reachable. The dashboard exposes live logs and sync status, which can include
> operational detail. An insecure-by-default posture means a misconfiguration silently leaves the UI
> open rather than failing closed.
>
> **Recommendation:** Consider defaulting to authentication-required (or at least surface a prominent
> startup warning when `Dashboard.Enabled` is true and `Token` is empty). The enforcement lives in the
> Host (`TokenAuthMiddleware`, Session 6); this finding flags the default that originates here.

#### F-104 — Core couples to the logging framework via `LogEntry.Level`

> **File:** `src/PlexToJellyfinSync.Core/Models/LogEntry.cs:1,20` · **Criterion:** D · **Severity:** ⚪ Note
>
> `LogEntry` is a Core domain model but exposes `LogLevel` from `Microsoft.Extensions.Logging`,
> pulling `Microsoft.Extensions.Logging.Abstractions` into the Core project. This is not a layer
> violation (it is a framework abstraction, not Data/Service/Host) and is a pragmatic choice, but it
> does mean the otherwise dependency-free Core carries a logging-infrastructure reference for a single
> property.
>
> **Recommendation:** Acceptable as-is. If strict Core purity is desired, model the level as a
> Core-owned enum and map at the logging boundary. No action required otherwise.

#### F-105 — `Deterministic` disabled in both build configurations

> **File:** `src/PlexToJellyfinSync.Core/PlexToJellyfinSync.Core.csproj:8,13` · **Criterion:** E · **Severity:** 🔵 Low
>
> Both the Debug and Release property groups set `<Deterministic>False</Deterministic>`. Deterministic
> compilation is the default and is desirable for reproducible/CI builds; disabling it is unusual and
> appears to be inherited from a project template (the same pattern is likely repeated across the
> other `.csproj` files — to be confirmed in Session 8).
>
> **Recommendation:** Remove the override (or set it to `True`) unless there is a deliberate reason
> (e.g. wildcard assembly versions) that requires non-deterministic builds.

### Session 2 — Data

The Plex DTO layer is clean and disciplined. Criterion B is fully satisfied across all 13 source
files: each uses a file-scoped namespace, holds exactly one top-level `public sealed class`, wraps
its members in a single `#region Properties` block, and carries English XML documentation on every
property without `<remarks>`. `using System.Text.Json.Serialization;` sits outside the namespace,
files are CRLF with no trailing newline, and Allman braces / 4-space indent are used throughout.

Criterion A is in good shape. Every `[JsonPropertyName]` casing matches the Plex API exactly — which
matters because `PlexJsonOptions.Default` deserializes with `PropertyNameCaseInsensitive = false`
(case-sensitive on purpose, so the lowercase scalar `guid` is not confused with the uppercase `Guid`
array). The PascalCase container/array keys (`MediaContainer`, `Account`, `Directory`, `Metadata`,
`Part`, `Genre`, `Guid`, `Media`) and the mixed-case scalar keys (`ratingKey`, `titleSort`,
`grandparentRatingKey`, `accountID`, …) are all correct. Timestamps (`addedAt`, `lastViewedAt`,
`viewedAt`) are modelled as `long?` epoch seconds, counters (`viewCount`, `year`, `index`) as `int?`,
and `ratingKey`/`grandparentRatingKey` as `string?` — all consistent with the real Plex payloads and
with how `PlexClient` consumes them. Pervasive nullability is the right defensive choice for fields
Plex omits when empty (e.g. `viewCount`/`lastViewedAt` on unwatched items), and
`NumberHandling.AllowReadingFromString` covers Plex's habit of emitting numbers as JSON strings.

The findings below are minor: an unused project reference (D), the build-determinism setting carried
over from the template (E, mirrors F-105), and two notes on wrapper duplication and an unmodelled
field.

#### F-201 — Data references Core but uses nothing from it

> **File:** `src/PlexToJellyfinSync.Data/PlexToJellyfinSync.Data.csproj:18` · **Criterion:** D · **Severity:** 🔵 Low
>
> The project declares `<ProjectReference Include="..\PlexToJellyfinSync.Core\..." />`, but no `.cs`
> file in the Data project references any Core type (`grep -rn "PlexToJellyfinSync.Core"` over the
> sources is empty). The DTOs are self-contained and depend only on `System.Text.Json`. Criterion D
> explicitly allows Data to reference "only Core (or nothing)"; here "nothing" is the accurate state,
> so the reference is dead weight that blurs the layering (it implies a coupling that does not exist).
>
> **Recommendation:** Remove the unused `ProjectReference` so Data has zero project dependencies,
> making the Core ← Data direction explicit and preventing accidental future coupling.

#### F-202 — `Deterministic` disabled in both build configurations

> **File:** `src/PlexToJellyfinSync.Data/PlexToJellyfinSync.Data.csproj:8,13` · **Criterion:** E · **Severity:** 🔵 Low
>
> Both the Debug and Release property groups set `<Deterministic>False</Deterministic>`. This is the
> same pattern flagged for Core in F-105 and confirms the suspicion there that the override is
> repeated across the project files (likely inherited from a template). Deterministic compilation is
> the default and is desirable for reproducible/CI builds.
>
> **Recommendation:** Remove the override (or set it to `True`) here and project-wide unless a
> deliberate reason requires non-deterministic builds. Consider centralising the decision in
> `Directory.Build.props`.

#### F-203 — Three near-identical Response/Container wrapper pairs

> **File:** `src/PlexToJellyfinSync.Data/Plex/PlexAccountsResponse.cs`,
> `PlexAccountsContainer.cs`, `PlexLibrariesResponse.cs`, `PlexLibrariesContainer.cs`,
> `PlexMetadataResponse.cs`, `PlexMetadataContainer.cs` · **Criterion:** D · **Severity:** ⚪ Note
>
> The Accounts, Libraries and Metadata responses each consist of a `*Response` type with a single
> `MediaContainer` property plus a `*Container` type holding one inner list. The three pairs differ
> only in the inner list's property name/type (`Account`/`Directory`/`Metadata`). The pattern is
> consistent and applied uniformly (good), but it is also six files of boilerplate that could be a
> single generic `PlexResponse<TContainer>` wrapper.
>
> **Recommendation:** Optional. A generic root wrapper would cut the duplication, but the inner array
> keys differ ("Account" vs "Directory" vs "Metadata") under case-sensitive deserialization, so the
> container types would still be needed. The current explicit form is clear and low-risk; leave as-is
> unless the wrapper count grows.

#### F-204 — Resume/partial-watch fields are not modelled

> **File:** `src/PlexToJellyfinSync.Data/Plex/PlexMetadata.cs:78-94` · **Criterion:** A · **Severity:** ⚪ Note
>
> `PlexMetadata` models `viewCount` and `lastViewedAt` but not `viewOffset` (the resume position Plex
> reports for partially-watched items). This is consistent with the current design — `PlexClient`
> treats `viewCount > 0` as "watched", so an in-progress item is correctly counted as unwatched — and
> is therefore not a bug. It is noted only so the gap is a conscious choice: if partial-watch state
> ever needs to be propagated to Jellyfin, the field is not available on the DTO.
>
> **Recommendation:** No action required for the current scope. Add `viewOffset` (and possibly the
> scalar `guid`) only if/when partial-watch propagation becomes a requirement.

### Session 3 — Service I (Plex Integration)

_Not yet performed._

### Session 4 — Service II (Sync & Persistence)

_Not yet performed._

### Session 5 — Service III (Logging & DI)

_Not yet performed._

### Session 6 — Host (Blazor, Worker, Security)

_Not yet performed._

### Session 7 — Tests

_Not yet performed._

### Session 8 — Infrastructure

_Not yet performed._

## File Checklist

Status: ⬜ open · ✅ reviewed. Review depth: **deep** = criteria A–D, **quick** = criterion E
(configs/workflows additionally C).

| File | Session | Depth | Status | Findings |
|---|---|---|---|---|
| `src/PlexToJellyfinSync.Core/Abstractions/ILogStore.cs` | 1 | deep | ✅ | none |
| `src/PlexToJellyfinSync.Core/Abstractions/INfoWriter.cs` | 1 | deep | ✅ | none |
| `src/PlexToJellyfinSync.Core/Abstractions/IPathMapper.cs` | 1 | deep | ✅ | none |
| `src/PlexToJellyfinSync.Core/Abstractions/IPlexClient.cs` | 1 | deep | ✅ | none |
| `src/PlexToJellyfinSync.Core/Abstractions/IStateStore.cs` | 1 | deep | ✅ | none |
| `src/PlexToJellyfinSync.Core/Abstractions/ISyncOrchestrator.cs` | 1 | deep | ✅ | none |
| `src/PlexToJellyfinSync.Core/Abstractions/ISyncStatusProvider.cs` | 1 | deep | ✅ | none |
| `src/PlexToJellyfinSync.Core/Enums/MediaKind.cs` | 1 | deep | ✅ | none |
| `src/PlexToJellyfinSync.Core/Enums/MovieNfoFilenameStrategy.cs` | 1 | deep | ✅ | none |
| `src/PlexToJellyfinSync.Core/Enums/NfoWriteOutcome.cs` | 1 | deep | ✅ | none |
| `src/PlexToJellyfinSync.Core/Models/LogEntry.cs` | 1 | deep | ✅ | F-104 |
| `src/PlexToJellyfinSync.Core/Models/MediaItem.cs` | 1 | deep | ✅ | F-102 |
| `src/PlexToJellyfinSync.Core/Models/PlexHistoryEntry.cs` | 1 | deep | ✅ | none |
| `src/PlexToJellyfinSync.Core/Models/PlexLibrary.cs` | 1 | deep | ✅ | none |
| `src/PlexToJellyfinSync.Core/Models/SyncStatusViewData.cs` | 1 | deep | ✅ | none |
| `src/PlexToJellyfinSync.Core/Models/UniqueId.cs` | 1 | deep | ✅ | F-102 |
| `src/PlexToJellyfinSync.Core/Models/WatchInfo.cs` | 1 | deep | ✅ | F-102 |
| `src/PlexToJellyfinSync.Core/Options/DashboardOptions.cs` | 1 | deep | ✅ | F-101, F-103 |
| `src/PlexToJellyfinSync.Core/Options/NfoOptions.cs` | 1 | deep | ✅ | none |
| `src/PlexToJellyfinSync.Core/Options/PathMapping.cs` | 1 | deep | ✅ | none |
| `src/PlexToJellyfinSync.Core/Options/PlexOptions.cs` | 1 | deep | ✅ | F-101 |
| `src/PlexToJellyfinSync.Core/Options/StateOptions.cs` | 1 | deep | ✅ | F-101 |
| `src/PlexToJellyfinSync.Core/Options/SyncOptions.cs` | 1 | deep | ✅ | F-101 |
| `src/PlexToJellyfinSync.Core/PlexToJellyfinSync.Core.csproj` | 1 | quick | ✅ | F-105 |
| `src/PlexToJellyfinSync.Data/Plex/PlexAccount.cs` | 2 | deep | ✅ | none |
| `src/PlexToJellyfinSync.Data/Plex/PlexAccountsContainer.cs` | 2 | deep | ✅ | F-203 |
| `src/PlexToJellyfinSync.Data/Plex/PlexAccountsResponse.cs` | 2 | deep | ✅ | F-203 |
| `src/PlexToJellyfinSync.Data/Plex/PlexDirectory.cs` | 2 | deep | ✅ | none |
| `src/PlexToJellyfinSync.Data/Plex/PlexGuid.cs` | 2 | deep | ✅ | none |
| `src/PlexToJellyfinSync.Data/Plex/PlexLibrariesContainer.cs` | 2 | deep | ✅ | F-203 |
| `src/PlexToJellyfinSync.Data/Plex/PlexLibrariesResponse.cs` | 2 | deep | ✅ | F-203 |
| `src/PlexToJellyfinSync.Data/Plex/PlexMedia.cs` | 2 | deep | ✅ | none |
| `src/PlexToJellyfinSync.Data/Plex/PlexMetadata.cs` | 2 | deep | ✅ | F-204 |
| `src/PlexToJellyfinSync.Data/Plex/PlexMetadataContainer.cs` | 2 | deep | ✅ | F-203 |
| `src/PlexToJellyfinSync.Data/Plex/PlexMetadataResponse.cs` | 2 | deep | ✅ | F-203 |
| `src/PlexToJellyfinSync.Data/Plex/PlexPart.cs` | 2 | deep | ✅ | none |
| `src/PlexToJellyfinSync.Data/Plex/PlexTag.cs` | 2 | deep | ✅ | none |
| `src/PlexToJellyfinSync.Data/PlexToJellyfinSync.Data.csproj` | 2 | quick | ✅ | F-201, F-202 |
| `src/PlexToJellyfinSync.Service/PlexClient.cs` | 3 | deep | ⬜ | |
| `src/PlexToJellyfinSync.Service/PlexJsonOptions.cs` | 3 | deep | ⬜ | |
| `src/PlexToJellyfinSync.Service/WatchAggregator.cs` | 3 | deep | ⬜ | |
| `src/PlexToJellyfinSync.Service/PathMapper.cs` | 3 | deep | ⬜ | |
| `src/PlexToJellyfinSync.Service/NfoWriter.cs` | 4 | deep | ⬜ | |
| `src/PlexToJellyfinSync.Service/SyncOrchestrator.cs` | 4 | deep | ⬜ | |
| `src/PlexToJellyfinSync.Service/State/StateStore.cs` | 4 | deep | ⬜ | |
| `src/PlexToJellyfinSync.Service/State/SyncStateFile.cs` | 4 | deep | ⬜ | |
| `src/PlexToJellyfinSync.Service/SyncStatusService.cs` | 4 | deep | ⬜ | |
| `src/PlexToJellyfinSync.Service/Logging/InMemoryLogProvider.cs` | 5 | deep | ⬜ | |
| `src/PlexToJellyfinSync.Service/Logging/InMemoryLogStore.cs` | 5 | deep | ⬜ | |
| `src/PlexToJellyfinSync.Service/Logging/InMemoryLogger.cs` | 5 | deep | ⬜ | |
| `src/PlexToJellyfinSync.Service/ServiceCollectionExtensions.cs` | 5 | deep | ⬜ | |
| `src/PlexToJellyfinSync.Service/PlexToJellyfinSync.Service.csproj` | 5 | quick | ⬜ | |
| `src/GlobalSuppressions.cs` | 6 | deep | ⬜ | |
| `src/PlexToJellyfinSync/Program.cs` | 6 | deep | ⬜ | |
| `src/PlexToJellyfinSync/Worker.cs` | 6 | deep | ⬜ | |
| `src/PlexToJellyfinSync/Security/LoginPage.cs` | 6 | deep | ⬜ | |
| `src/PlexToJellyfinSync/Security/TokenAuthMiddleware.cs` | 6 | deep | ⬜ | |
| `src/PlexToJellyfinSync/Components/App.razor` | 6 | deep | ⬜ | |
| `src/PlexToJellyfinSync/Components/Routes.razor` | 6 | deep | ⬜ | |
| `src/PlexToJellyfinSync/Components/_Imports.razor` | 6 | deep | ⬜ | |
| `src/PlexToJellyfinSync/Components/Layout/MainLayout.razor` | 6 | deep | ⬜ | |
| `src/PlexToJellyfinSync/Components/Layout/MainLayout.razor.css` | 6 | quick | ⬜ | |
| `src/PlexToJellyfinSync/Components/Layout/ReconnectModal.razor` | 6 | deep | ⬜ | |
| `src/PlexToJellyfinSync/Components/Layout/ReconnectModal.razor.css` | 6 | quick | ⬜ | |
| `src/PlexToJellyfinSync/Components/Layout/ReconnectModal.razor.js` | 6 | quick | ⬜ | |
| `src/PlexToJellyfinSync/Components/Pages/Dashboard.razor` | 6 | deep | ⬜ | |
| `src/PlexToJellyfinSync/Components/Pages/Error.razor` | 6 | deep | ⬜ | |
| `src/PlexToJellyfinSync/Components/Pages/Logs.razor` | 6 | deep | ⬜ | |
| `src/PlexToJellyfinSync/Components/Pages/NotFound.razor` | 6 | deep | ⬜ | |
| `src/PlexToJellyfinSync/wwwroot/app.css` | 6 | quick | ⬜ | |
| `src/PlexToJellyfinSync/appsettings.json` | 6 | quick | ⬜ | |
| `src/PlexToJellyfinSync/appsettings.Development.json` | 6 | quick | ⬜ | |
| `src/PlexToJellyfinSync/Properties/launchSettings.json` | 6 | quick | ⬜ | |
| `src/PlexToJellyfinSync/PlexToJellyfinSync.csproj` | 6 | quick | ⬜ | |
| `tests/PlexToJellyfinSync.Tests/MSTestSettings.cs` | 7 | deep | ⬜ | |
| `tests/PlexToJellyfinSync.Tests/NfoWriterTests.cs` | 7 | deep | ⬜ | |
| `tests/PlexToJellyfinSync.Tests/PathMapperTests.cs` | 7 | deep | ⬜ | |
| `tests/PlexToJellyfinSync.Tests/PlexMetadataDeserializationTests.cs` | 7 | deep | ⬜ | |
| `tests/PlexToJellyfinSync.Tests/WatchAggregatorTests.cs` | 7 | deep | ⬜ | |
| `tests/PlexToJellyfinSync.Tests/PlexToJellyfinSync.Tests.csproj` | 7 | quick | ⬜ | |
| `.claude/CLAUDE.md` | 8 | quick | ⬜ | |
| `.claude/hooks/sonar-secrets/build-scripts/pretool-secrets.ps1` | 8 | quick | ⬜ | |
| `.claude/hooks/sonar-secrets/build-scripts/prompt-secrets.ps1` | 8 | quick | ⬜ | |
| `.claude/settings.json` | 8 | quick | ⬜ | |
| `.claude/skills/publish-pr/prompt.md` | 8 | quick | ⬜ | |
| `.dockerignore` | 8 | quick | ⬜ | |
| `.editorconfig` | 8 | quick | ⬜ | |
| `.gitattributes` | 8 | quick | ⬜ | |
| `.gitignore` | 8 | quick | ⬜ | |
| `.github/ISSUE_TEMPLATE/bug_report.md` | 8 | quick | ⬜ | |
| `.github/ISSUE_TEMPLATE/feature_request.md` | 8 | quick | ⬜ | |
| `.github/copilot-instructions.md` | 8 | quick | ⬜ | |
| `.github/dependabot.yml` | 8 | quick | ⬜ | |
| `.github/skills/create-pr/SKILL.md` | 8 | quick | ⬜ | |
| `.github/skills/publish-pr/SKILL.md` | 8 | quick | ⬜ | |
| `.github/workflows/ci.yml` | 8 | quick | ⬜ | |
| `.github/workflows/codeql.yml` | 8 | quick | ⬜ | |
| `.github/workflows/release.yml` | 8 | quick | ⬜ | |
| `Directory.Build.props` | 8 | quick | ⬜ | |
| `Directory.Packages.props` | 8 | quick | ⬜ | |
| `Dockerfile` | 8 | quick | ⬜ | |
| `LICENSE.md` | 8 | quick | ⬜ | |
| `PlexToJellyfinSync.Debug.ruleset` | 8 | quick | ⬜ | |
| `PlexToJellyfinSync.Release.ruleset` | 8 | quick | ⬜ | |
| `PlexToJellyfinSync.slnx` | 8 | quick | ⬜ | |
| `README.md` | 8 | quick | ⬜ | |
| `SECURITY.md` | 8 | quick | ⬜ | |

## Completeness Proof

_Filled in during Session 8: reconcile the checklist against `git ls-files` (target: 107/107 files
of the review baseline, plus handling of files added later per `00-process.md`)._
