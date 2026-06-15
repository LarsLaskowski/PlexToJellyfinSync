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

The Plex integration layer is in good shape on the criteria that matter most for an HTTP client.
**Async correctness is exemplary:** every `await` in `PlexClient` carries `.ConfigureAwait(false)`,
the `CancellationToken` is threaded all the way through (`GetAsync` → `HttpClient.GetAsync` →
`ReadAsStreamAsync` → `JsonSerializer.DeserializeAsync`), there is no `async void` and no blocking
`.Result`/`.Wait()`. **Resource disposal is correct:** the `HttpResponseMessage` is `using` and the
content stream is `await using`. **Security (criterion C) is clean for the token:** the
`X-Plex-Token` is sent as an HTTP *header* (configured in `ServiceCollectionExtensions`, not in
Session 3's files) and never appears in a request URL, log message, or exception — the only error
log (`GetOwnerAccountIdAsync`) logs the framework exception, which carries the status code but not
the request URI or headers, so the token does not leak. `PlexJsonOptions` is deliberately
case-sensitive (so the lowercase scalar `guid` is not confused with the uppercase `Guid` array) and
the shared frozen `JsonSerializerOptions` instance is the recommended pattern — it is clean and
carries no findings. Criterion B is satisfied across all four files (`#region` grouping, interface
region named `IPlexClient`, English XML docs, `== false`, `is null`/`is not null`, LINQ method
syntax, `_camelCase` readonly fields).

The findings below concern pagination/large-library handling and an over-broad `catch` in
`PlexClient` (A), the HttpClient lifetime when the typed client is captured by a singleton (A/D),
cross-platform path matching and the textual traversal guard in `PathMapper` (A/C), the
information-collapsing aggregation in `WatchAggregator` (A), and the test coverage gaps for this
layer (D).

#### F-301 — `GetHistorySinceAsync` hard-caps the history at 500 entries with no pagination

> **File:** `src/PlexToJellyfinSync.Service/PlexClient.cs:305` · **Criterion:** A · **Severity:** 🟠 High
>
> The history URL hardcodes `X-Plex-Container-Start=0&X-Plex-Container-Size=500` and the method never
> loops to fetch further pages. Results are sorted `viewedAt:desc`, so when more than 500 history
> entries exist after `since`, only the **500 most recent** are returned and the *oldest* ones — the
> entries closest to `since` — are silently dropped. Because the incremental sync advances its
> watermark past the processed window, those dropped watch events are never reprocessed, so the
> corresponding items keep a stale (unwatched) state in Jellyfin. The periodic full reconcile
> (`GetLibraryItemsAsync`) mitigates this only partially and on a long interval, and it has its own
> pagination caveat (F-302). This is realistic after downtime or on the first run with an early
> `since`.
>
> **Recommendation:** Page through the history with `X-Plex-Container-Start`/`-Size` until fewer than
> a full page is returned (or until `viewedAt <= since`), or sort ascending and page forward from
> `since`. At minimum, detect a full 500-row page and log a warning that history may be truncated.

#### F-302 — Library/episode reads have no pagination and load the full result set into memory

> **File:** `src/PlexToJellyfinSync.Service/PlexClient.cs:343,357` · **Criterion:** A · **Severity:** 🟡 Medium
>
> `GetEpisodesAsync` (`/library/metadata/{key}/allLeaves`) and `GetLibraryItemsAsync`
> (`/library/sections/{key}/all`) send no container-size/start parameters and do not page. They rely
> on the Plex server returning the *entire* section in a single response and then materialize it with
> `entries.Select(MapMediaItem).ToList()`. For large libraries this both risks truncation (if a server
> applies a default container size) and forces the whole library into memory at once, which scales
> poorly for the explicitly in-scope "large libraries" case.
>
> **Recommendation:** Page these endpoints with `X-Plex-Container-Start`/`-Size` and, ideally, stream
> results to the caller (e.g. `IAsyncEnumerable<MediaItem>`) so the orchestrator processes items in
> batches rather than holding the full library in memory.

#### F-303 — `GetOwnerAccountIdAsync` catches `Exception` broadly and swallows cancellation

> **File:** `src/PlexToJellyfinSync.Service/PlexClient.cs:273-278` · **Criterion:** A · **Severity:** 🟡 Medium
>
> The auto-detect path wraps the HTTP call in `catch (Exception ex)` and, on any failure, logs a
> warning and returns the fallback id `1`. This also catches `OperationCanceledException` /
> `TaskCanceledException`: when the host is shutting down (or the per-request timeout fires), the
> cancellation is swallowed, logged as a generic warning, and the method returns a *real* account id
> of `1` instead of propagating the cancellation. A genuinely cancelled sync then proceeds with a
> guessed owner id. The broad catch also masks configuration errors (e.g. 401 from a bad token) as a
> benign "could not auto-detect" warning.
>
> **Recommendation:** Re-throw on cancellation (`catch (OperationCanceledException) { throw; }` first,
> or check `cancellationToken.IsCancellationRequested`) and narrow the remaining catch to
> `HttpRequestException`/`JsonException`. Consider distinguishing an auth failure (401/403) from a
> "no accounts returned" case so a misconfiguration is visible rather than silently defaulting to `1`.

#### F-304 — Typed `HttpClient` client is captured by a singleton consumer (captive dependency)

> **File:** `src/PlexToJellyfinSync.Service/PlexClient.cs:24,38`
> (consumer: `SyncOrchestrator` registered `AddSingleton` in
> `ServiceCollectionExtensions.cs:42,44`) · **Criterion:** A/D · **Severity:** 🔵 Low
>
> `PlexClient` is registered as a typed client via `AddHttpClient<IPlexClient, PlexClient>` (transient)
> and injected into `SyncOrchestrator`, which is a **singleton** (`SyncOrchestrator.cs:18` holds
> `IPlexClient _plexClient`). The single `PlexClient`/`HttpClient` instance is therefore captured for
> the application's lifetime, so `IHttpClientFactory`'s handler rotation (`HandlerLifetime`) never
> takes effect and a `PrimaryHttpMessageHandler`/DNS-refresh policy would not apply. For a long-running
> worker talking to one fixed internal Plex host the practical impact is small, but it defeats the
> reason for using the typed-client factory in the first place. (Registration belongs to Session 5;
> recorded here because it governs `PlexClient`'s HttpClient lifetime, a focus area of this session.)
>
> **Recommendation:** Either inject `IHttpClientFactory`/an `IPlexClient` factory and create a
> short-lived client per sync cycle, or accept the captive lifetime deliberately and drop the
> handler-rotation expectation (document it). Confirm the final call in Session 5.

#### F-305 — `PathMapper` prefix matching is always case-sensitive (Ordinal)

> **File:** `src/PlexToJellyfinSync.Service/PathMapper.cs:76-77` · **Criterion:** A · **Severity:** 🔵 Low
>
> Prefix comparison uses `StringComparison.Ordinal`, so a mapping whose casing differs from the path
> Plex reports will not match. On case-insensitive sources (Plex running on Windows reporting e.g.
> `D:\Media\…` while the mapping is configured as `d:\media`) the lookup returns `null` and the item is
> treated as unmapped. The project targets Linux/Docker, where case-sensitive matching is correct,
> which keeps this Low — but the behavior is silent (an unmapped path just yields `null`).
>
> **Recommendation:** Make the comparison platform-aware (`OrdinalIgnoreCase` when the source is
> case-insensitive) or document explicitly that mapping prefixes must match Plex's exact casing. Add a
> case-mismatch test to pin the chosen behavior.

#### F-306 — Traversal guard is textual and misses a leading `..` segment

> **File:** `src/PlexToJellyfinSync.Service/PathMapper.cs:59` · **Criterion:** C · **Severity:** 🔵 Low
>
> The guard rejects `"/../"` in the middle and a trailing `"/.."`, but a path that *starts* with a
> traversal segment (`"../etc/passwd"`, or the bare input `".."`) is not caught — neither substring
> matches. Such inputs are only stopped incidentally because they fail to match an absolute mapping
> prefix; with a relative mapping prefix they could slip through. The check is purely textual and does
> not canonicalize (`.` segments, symlinks) the result, so it cannot by itself prove the mapped path
> stays under the local root.
>
> **Recommendation:** Also reject `StartsWith("../", Ordinal)` and an input equal to `".."`, and treat
> path-traversal defense as defense-in-depth — have `NfoWriter` (Session 4) verify the *resolved*
> output path is rooted under the configured Local prefix before writing.

#### F-307 — Mapped output is always forward-slash and never converted to the OS separator

> **File:** `src/PlexToJellyfinSync.Service/PathMapper.cs:91-94` · **Criterion:** A · **Severity:** ⚪ Note
>
> Both the local prefix and the remainder are normalized to `/`, and the result
> (`localPrefix + remainder`) is returned with forward slashes regardless of platform. A `Local`
> prefix configured with backslashes (`C:\media`) comes back as `C:/media/…`. On the Linux/Docker
> target this is correct and harmless; on Windows it relies on the .NET IO layer accepting forward
> slashes. It is noted so the single-target assumption is explicit.
>
> **Recommendation:** No action for the current Linux/Docker scope. If Windows hosting is ever
> supported, convert the result with `Path.DirectorySeparatorChar` (or build it via `Path.Combine`).

#### F-308 — `WatchAggregator` collapses play count and reports `LastPlayed` for unwatched aggregates

> **File:** `src/PlexToJellyfinSync.Service/WatchAggregator.cs:30-37` · **Criterion:** A · **Severity:** ⚪ Note
>
> `PlayCount` is set to `allWatched ? 1 : 0`, discarding the children's actual counts — an item
> watched many times and one watched once both aggregate to `1`, and any partially-watched season
> reports `PlayCount = 0` even though some episodes were played. Independently, `LastPlayed` is the max
> over *all* children, so a not-fully-watched season still surfaces a `LastPlayed` timestamp
> (`Watched = false`, `PlayCount = 0`, `LastPlayed = <date>`). This appears intentional (a season/series
> is "watched" only when every child is), but the information loss and the watched/last-played
> mismatch are worth confirming against how `NfoWriter` consumes them. The `lastPlayed == default`
> sentinel also conflates "no child had a value" with the (in practice unreachable) `DateTimeOffset.MinValue`.
>
> **Recommendation:** Confirm the intended season/series semantics. If a meaningful play count is
> wanted, derive it from the children (e.g. min/most-common). Consider only emitting `LastPlayed` when
> `allWatched`, and replace the `== default` sentinel with the nullable returned by
> `Where(...).Select(...).Max()` over a nullable projection.

#### F-309 — Test coverage gaps in the Plex integration layer

> **File:** `src/PlexToJellyfinSync.Service/PlexClient.cs`,
> `WatchAggregator.cs`, `PathMapper.cs` · **Criterion:** D · **Severity:** 🔵 Low
>
> `PlexClient` has **no** unit tests (only DTO deserialization is covered in
> `PlexMetadataDeserializationTests`, Session 7): the mapping logic (`MapMediaItem`, `MapKind`,
> `ParseUniqueIds`, the `FromEpoch` epoch→`DateTimeOffset` conversion, the runtime `/60000` division),
> the history filtering/ordering, and the owner-detection fallback are untested. The private static
> helpers are not reachable for testing as written. `WatchAggregatorTests` covers all-watched,
> partially-watched and empty, but not the `LastPlayed` max with mixed `null` values, the
> `PlayCount` collapse, or the `== default` branch. `PathMapperTests` is solid but does not cover
> casing (F-305), a leading-`..` input (F-306), an empty/whitespace `Plex` prefix being skipped, or
> equal-length competing prefixes.
>
> **Recommendation:** Add `PlexClient` tests behind a mocked `HttpMessageHandler` for the mapping and
> history paths (this also pins F-301/F-303), and extend the aggregator/mapper tests for the edge
> cases above. Per the process doc, do not write the tests as part of this review — track them as gaps.

#### F-310 — `PathMapper` uses `new List<PathMapping>()` instead of a collection expression

> **File:** `src/PlexToJellyfinSync.Service/PathMapper.cs:27` · **Criterion:** B · **Severity:** 🔵 Low
>
> The constructor falls back to `new List<PathMapping>()` for a null options value, whereas the rest of
> the service layer uses the collection-expression form `[]` for empty collections (e.g. the `return []`
> paths in `PlexClient`). This is a small internal-consistency nit, not a rule in `.claude/CLAUDE.md`.
>
> **Recommendation:** Use `mappings.Value ?? []` for consistency with the surrounding code.

### Session 4 — Service II (Sync & Persistence)

_Not yet performed._

### Session 5 — Service III (Logging & DI)

_Not yet performed._

### Session 6 — Host (Blazor, Worker, Security)

_Not yet performed._

### Session 7 — Tests

The MSTest suite is small but disciplined and conforms to criterion B almost completely. All four
test classes follow the `{Feature}Tests` naming scheme (`NfoWriterTests`, `PathMapperTests`,
`PlexMetadataDeserializationTests`, `WatchAggregatorTests`) and every test method is PascalCase
**without underscores** in the `{Class}{Scenario}{ExpectedResult}` form
(`WatchAggregatorAllWatchedReturnsWatched`, `PathMapperOverlappingPrefixesUsesLongestMatch`, …).
**Every single assertion carries an assert message** — including the `Assert.HasCount` / `Assert.IsNull`
/ `StringAssert.Contains` calls — so the assert-message rule is fully satisfied. The suite is pure
MSTest with no FluentAssertions (the `Microsoft.VisualStudio.TestTools.UnitTesting` namespace is a
global `Using` in the `.csproj`), each class is `sealed`, carries an English XML class summary, and
wraps its members in `#region` blocks. Determinism is good: `PathMapperTests`, `WatchAggregatorTests`
and `PlexMetadataDeserializationTests` use fixed inputs (`DateTimeOffset.UnixEpoch`, literal paths, an
inline JSON constant) with no culture/time-zone coupling, and `NfoWriterTests` isolates disk I/O in a
per-test `Guid`-named temp directory that is removed in `[TestCleanup]` — which also makes the
method-level parallelization configured in `MSTestSettings.cs` safe (state is per-instance, the only
shared statics are immutable). `NfoWriterExistingNfoPreservesOtherNodes` is a genuine
behavior test (it pins the data-integrity guarantee that unrelated NFO nodes survive a write) rather
than an implementation-detail test.

One stylistic note (not a `.claude/CLAUDE.md` rule, so recorded here rather than as a finding): the
production code separates `#region Static methods` from `#region Methods`, but the test classes place
their `private static` factory helpers (`CreateWriter`, `CreateMapper`) inside the same
`#region Methods` as the instance test methods. The CLAUDE.md region rule groups by member *kind*, and
"Methods" is a single kind there, so this is internal inconsistency rather than a violation.

The findings below are a missing XML doc on a test constant (B), the broad service-layer test-coverage
gaps and the edge-case gaps inside the existing test classes (D/A), and the build-determinism setting
carried over from the project template (E, mirrors F-105/F-202).

#### F-701 — `MetadataJson` test constant has no XML documentation

> **File:** `tests/PlexToJellyfinSync.Tests/PlexMetadataDeserializationTests.cs:16` · **Criterion:** B · **Severity:** 🟡 Medium
>
> Inside `#region Constants` the `private const string MetadataJson` is declared without an XML doc
> comment, whereas every other member in the test project (classes, test methods, the `Initialize`/
> `Cleanup` lifecycle methods, the `CreateWriter`/`CreateMapper` helpers) carries one. `.claude/CLAUDE.md`
> requires "XML docs on all members" without exempting test code or private members, and the test
> project sets `<GenerateDocumentationFile>true</GenerateDocumentationFile>`. This is the single
> member-documentation gap in the session.
>
> **Recommendation:** Add a one-line `/// <summary>` describing the sample payload (e.g. "Sample Plex
> metadata response carrying both the legacy lowercase `guid` and the uppercase `Guid` array"). Confirm
> whether the Reihitsu analyzer is configured to enforce docs on private members; if it is, this is also
> a build (`RH####`) gap.

#### F-702 — Core sync, persistence, status and logging classes have no tests at all

> **File:** `src/PlexToJellyfinSync.Service/SyncOrchestrator.cs`,
> `src/PlexToJellyfinSync.Service/State/StateStore.cs`,
> `src/PlexToJellyfinSync.Service/SyncStatusService.cs`,
> `src/PlexToJellyfinSync.Service/Logging/InMemoryLogStore.cs`,
> `src/PlexToJellyfinSync.Service/Logging/InMemoryLogProvider.cs`,
> `src/PlexToJellyfinSync.Service/Logging/InMemoryLogger.cs`,
> `src/PlexToJellyfinSync.Service/ServiceCollectionExtensions.cs`,
> `src/PlexToJellyfinSync.Service/PlexClient.cs` · **Criterion:** D · **Severity:** 🟡 Medium
>
> The suite covers `NfoWriter`, `PathMapper`, `WatchAggregator` and (indirectly) `PlexJsonOptions`, but
> the highest-risk classes are entirely untested. `SyncOrchestrator` (the central orchestration logic,
> watermark advancement, the history→aggregation→write pipeline) has **no** tests, so the very behavior
> the process doc lists as critical — "sync writes wrong watch states", history truncation (F-301),
> cancellation handling (F-303) — is unverified. `StateStore` is untested even though it owns the
> state-file persistence whose **atomic write / data integrity** is a criterion-A focus area. The
> concurrency-sensitive `InMemoryLogStore`/`SyncStatusService` (written by the Worker, read by Blazor
> circuits) have no tests pinning their thread-safety. `PlexClient` has no tests beyond DTO
> deserialization (already noted as F-309). The DI registration in `ServiceCollectionExtensions` is
> unverified (no test asserting the container resolves the graph with the intended lifetimes — relevant
> to the captive-dependency F-304).
>
> **Recommendation:** Prioritise tests for `SyncOrchestrator` (behind mocked `IPlexClient`/`INfoWriter`/
> `IStateStore`) and `StateStore` (round-trip + atomic-write/corrupt-file behavior), then the
> log-store/status concurrency. Per the process doc, do **not** write the tests as part of this review —
> tracked here as a coverage gap for the relevant sessions (4/5).

#### F-703 — Edge-case gaps inside the existing test classes

> **File:** `tests/PlexToJellyfinSync.Tests/NfoWriterTests.cs`,
> `tests/PlexToJellyfinSync.Tests/PathMapperTests.cs`,
> `tests/PlexToJellyfinSync.Tests/WatchAggregatorTests.cs`,
> `tests/PlexToJellyfinSync.Tests/PlexMetadataDeserializationTests.cs` · **Criterion:** A/D · **Severity:** 🔵 Low
>
> Each existing class verifies the happy path but leaves notable branches uncovered:
> - **`NfoWriterTests`** exercises only `MediaKind.Movie`. The other root names and target-path rules
>   (`episodedetails`, `season.nfo`, `tvshow.nfo`) and the `MovieFilenameStrategy` branches
>   (`MovieNfo` / `VideoFileName` / `Auto`-picks-`movie.nfo`-when-present) are untested. The
>   **idempotent "no change → `Skipped`"** path (an existing NFO whose watch state already matches, so
>   `ApplyWatchState` returns `false`) is never asserted — only `Created`, `Updated` and
>   "missing + creation disabled → `Skipped`" are. The malformed-XML branch (`document.Root is null`,
>   `NfoWriter.cs:344`) and the `lastplayed`/`dateadded` formatting are also uncovered. Minor smell:
>   `NfoWriterMovieWithoutNfoCreatesFile` seeds `LastPlayed = DateTimeOffset.Now`; it does not assert on
>   the rendered timestamp so it is not flaky, but a fixed instant would make intent clearer.
> - **`PathMapperTests`** does not cover a leading `..` segment / bare `".."` input (F-306), the
>   `Ordinal` case-sensitivity behavior (F-305), an empty/whitespace `Plex` prefix being skipped, or
>   two equal-length competing prefixes.
> - **`WatchAggregatorTests`** never asserts `PlayCount` (the `allWatched ? 1 : 0` collapse, F-308),
>   the `LastPlayed` max across **mixed null/non-null** children, or the single-child case.
> - **`PlexMetadataDeserializationTests`** covers only the `guid`/`Guid` case-sensitivity. The
>   `NumberHandling.AllowReadingFromString` path (Plex emitting numbers as JSON strings), an
>   unwatched item (`viewCount`/`lastViewedAt` absent → `null`), and the Accounts/Libraries responses
>   are untested.
>
> **Recommendation:** Extend the four classes with the branches above (this overlaps the production-side
> gaps in F-309). Analysis only for this review — do not add the tests here.

#### F-704 — `Deterministic` disabled in both build configurations of the test project

> **File:** `tests/PlexToJellyfinSync.Tests/PlexToJellyfinSync.Tests.csproj:10,15` · **Criterion:** E · **Severity:** 🔵 Low
>
> Both the Debug and Release `PropertyGroup`s set `<Deterministic>False</Deterministic>`, the same
> template-inherited override flagged for Core (F-105) and Data (F-202). It is undesirable for
> reproducible/CI builds and is now confirmed across a third project, reinforcing the suggestion to
> centralise the setting. Otherwise the `.csproj` is clean under criterion E: the single
> `PackageReference Include="MSTest"` carries **no inline version** (CPM-compliant), and the three
> `ProjectReference`s (Core, Data, Service) are correct for what the tests consume.
>
> **Recommendation:** Remove the override (or set `True`), ideally by centralising the decision in
> `Directory.Build.props` together with F-105/F-202.

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
| `src/PlexToJellyfinSync.Service/PlexClient.cs` | 3 | deep | ✅ | F-301, F-302, F-303, F-304, F-309 |
| `src/PlexToJellyfinSync.Service/PlexJsonOptions.cs` | 3 | deep | ✅ | none |
| `src/PlexToJellyfinSync.Service/WatchAggregator.cs` | 3 | deep | ✅ | F-308, F-309 |
| `src/PlexToJellyfinSync.Service/PathMapper.cs` | 3 | deep | ✅ | F-305, F-306, F-307, F-309, F-310 |
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
| `tests/PlexToJellyfinSync.Tests/MSTestSettings.cs` | 7 | deep | ✅ | none |
| `tests/PlexToJellyfinSync.Tests/NfoWriterTests.cs` | 7 | deep | ✅ | F-703 |
| `tests/PlexToJellyfinSync.Tests/PathMapperTests.cs` | 7 | deep | ✅ | F-703 |
| `tests/PlexToJellyfinSync.Tests/PlexMetadataDeserializationTests.cs` | 7 | deep | ✅ | F-701, F-703 |
| `tests/PlexToJellyfinSync.Tests/WatchAggregatorTests.cs` | 7 | deep | ✅ | F-703 |
| `tests/PlexToJellyfinSync.Tests/PlexToJellyfinSync.Tests.csproj` | 7 | quick | ✅ | F-702, F-704 |
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
