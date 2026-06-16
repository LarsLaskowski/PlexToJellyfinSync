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

The sync and persistence core is functionally coherent and its async hygiene is excellent: every
`await` in `SyncOrchestrator`, `StateStore` and `NfoWriter` carries `.ConfigureAwait(false)`, the
`CancellationToken` is threaded through every method and checked with `ThrowIfCancellationRequested()`
inside each loop, there is no `async void` and no blocking `.Result`/`.Wait()`. `SyncOrchestrator`
re-throws `OperationCanceledException` before its broad `catch` so shutdown is not masked, and
**idempotency holds at the disk level**: a second incremental run with no new history performs no
writes (the watermark is only advanced when `maxViewed > since`), and the full reconcile re-visits
every item but relies on `NfoWriter` returning `Skipped` when the watch fields already match — so an
unchanged library produces zero file writes. `NfoWriter` correctly preserves unrelated NFO content
(it parses the existing file with `LoadOptions.PreserveWhitespace` and only touches `watched`/
`playcount`/`lastplayed`), and `MovieNfoFilenameStrategy` is implemented correctly (the `default`
switch arm maps `PreferExistingMovieNfo = 0` to "movie.nfo if present, else video-named"). Thread
safety of the dashboard-facing status is sound: `SyncStatusService` guards a single mutable instance
with a `Lock` and returns deep-copied snapshots, and `StateStore` serialises all file access through a
`SemaphoreSlim`.

The findings below cluster around one recurring theme — **non-atomic in-place writes** (F-401 for the
NFO files, F-411 for the state file), which can corrupt or empty a user's existing data on a crash or
cancellation mid-write — plus the **per-item error handling in `SyncOrchestrator`** (F-406), where a
single bad item aborts the whole cycle and, because the watermark never advances past it, can
permanently stall the incremental sync. The remainder are robustness (malformed-NFO handling,
folder-layout assumptions, owner-id caching), a god-class observation (F-407), two `#region`
grouping slips (F-404, F-410), and the test-coverage gap that four of these five classes have no
tests at all (F-416).

#### F-401 — NFO files are written in place and non-atomically; a crash mid-write can empty an existing NFO

> **File:** `src/PlexToJellyfinSync.Service/NfoWriter.cs:150` (write), `:359`, `:347`, `:379` (callers) · **Criterion:** A · **Severity:** 🟠 High
>
> `SaveAsync` opens the **target** path directly with `new FileStream(path, FileMode.Create, …)`, which
> truncates the existing file to zero length *before* the new content is serialised. If the process is
> killed, the host shuts down, or the passed `CancellationToken` fires while `document.SaveAsync` is
> still running, the user's existing `.nfo` is left truncated or empty — the very metadata the project
> promises to preserve is lost. This is the data-integrity concern called out in criterion A
> ("Is existing NFO content preserved on write?") and it applies to the update path (`:359`), the
> rebuild path (`:347`) and the create path (`:379`).
>
> **Recommendation:** Write to a sibling temp file (e.g. `path + ".tmp"`) and atomically replace the
> target via `File.Move(temp, path, overwrite: true)` (or `File.Replace`) only after the stream has
> flushed and disposed successfully. That way a partial write never overwrites a good file.

#### F-402 — Malformed or foreign NFO content throws an unhandled `XmlException`; the defensive `root is null` branch is unreachable

> **File:** `src/PlexToJellyfinSync.Service/NfoWriter.cs:341,344` · **Criterion:** A · **Severity:** 🟡 Medium
>
> When the target file exists, `XDocument.Parse(xml, …)` is called with no surrounding `try`/`catch`.
> A broken or non-XML `.nfo` (truncated by an earlier crash, hand-edited, or written by another tool in
> a different shape) makes `Parse` throw `XmlException`, which propagates out of `WriteAsync`. Because
> `SyncOrchestrator` has no per-item guard (see F-406), that single malformed file aborts the entire
> sync cycle. Separately, the defensive `if (root is null)` rebuild branch at line 344 is effectively
> **dead code**: `XDocument.Parse` either returns a document with a root element or throws "Root element
> is missing", so `document.Root` is never null after a successful parse. The real failure mode
> (a parse exception) is therefore unhandled while the handled case cannot occur.
>
> **Recommendation:** Wrap the read/parse in a `try`/`catch (XmlException)` and, on failure, either skip
> the item with a warning or rebuild the document from scratch (the behaviour the unreachable branch was
> presumably meant to provide). This both makes the file robust against foreign/broken NFOs and removes
> the dead branch.

#### F-403 — `NfoWriter` does not verify the resolved target stays under the library root

> **File:** `src/PlexToJellyfinSync.Service/NfoWriter.cs:163-192,334-336` · **Criterion:** C · **Severity:** 🔵 Low
>
> `WriteAsync` writes to whatever `ResolveTargetPath` produces from the `localPath` handed in by
> `SyncOrchestrator`, which is the raw output of `PathMapper.MapToLocal`. `NfoWriter` performs no
> containment check that the resolved path is rooted under the configured Local prefix. Session 3 found
> that `PathMapper`'s traversal guard is purely textual and misses a leading `..` segment (F-306), so a
> manipulated Plex file path could in principle steer a write outside the intended library root. The
> practical risk is low (single trusted Plex owner, Linux/Docker target), but the write side currently
> relies entirely on the mapper's incomplete guard with no defence-in-depth at the point of writing.
>
> **Recommendation:** As recommended in F-306, have `NfoWriter` canonicalise the resolved target
> (`Path.GetFullPath`) and assert it is rooted under the mapped Local prefix before opening the stream;
> reject and log anything that escapes.

#### F-404 — The static `SaveAsync` lives in `#region Methods` instead of `#region Static methods`

> **File:** `src/PlexToJellyfinSync.Service/NfoWriter.cs:130,140` · **Criterion:** B · **Severity:** 🔵 Low
>
> `GetRootName`, `SetChild` and `AddIfNotEmpty` are correctly grouped under `#region Static methods`,
> but `SaveAsync` — which is also `private static` — sits at the top of `#region Methods` alongside the
> instance methods. `.claude/CLAUDE.md` requires `#region` blocks grouped by member kind, so a static
> method belongs in the static-methods region.
>
> **Recommendation:** Move `SaveAsync` into the `#region Static methods` block (or merge the two method
> regions if the split is not wanted), keeping member-kind grouping consistent.

#### F-405 — On update, the original encoding/BOM is not preserved and appended elements are unindented

> **File:** `src/PlexToJellyfinSync.Service/NfoWriter.cs:23,145,340,359` · **Criterion:** A · **Severity:** ⚪ Note
>
> The existing file is read with `File.ReadAllTextAsync` (which auto-detects a BOM and decodes
> accordingly) but always rewritten as UTF-8 **without** a BOM via the shared `_utf8NoBom` encoder. An
> NFO that originally carried a UTF-8/UTF-16 BOM (common for Kodi/Windows-authored files) silently loses
> it, and a file declaring a non-UTF-8 encoding in its XML declaration is re-emitted as UTF-8. Also, the
> update path saves with `indent: false` while preserving existing whitespace, so the newly added
> `watched`/`playcount`/`lastplayed` elements are appended without indentation and can look misaligned
> against the surrounding pretty-printed content. Neither affects correctness — Jellyfin parses both —
> so this is recorded as a note on output fidelity.
>
> **Recommendation:** If byte-for-byte fidelity matters, detect and re-emit the original BOM/encoding;
> otherwise document UTF-8-no-BOM as the canonical output. The indentation cosmetics are acceptable
> given the deliberate choice to preserve existing whitespace.

#### F-406 — An error on a single item aborts the whole sync cycle and can permanently stall the watermark

> **File:** `src/PlexToJellyfinSync.Service/SyncOrchestrator.cs:270-285,297-312` (also `:377-384`, `:397-416`) · **Criterion:** A · **Severity:** 🟠 High
>
> The per-entry loop in `ProcessHistoryAsync` (and the per-item loops in `ReconcileMovieLibraryAsync` /
> `ReconcileSeriesLibraryAsync`) call `ProcessRatingKeyAsync` / `WriteItemAsync` /
> `UpdateSeriesAggregatesAsync` with **no per-item `try`/`catch`**. Any exception from one item — a
> malformed existing NFO (F-402), an I/O error, a transient Plex hiccup on one `GetMediaItemAsync` —
> propagates straight to the cycle-level `catch (Exception ex) { HandleError(ex); }`, so every remaining
> item in the batch is skipped. The process doc lists exactly this as a focus area: "an error in one
> item must not abort the cycle." Worse, in the incremental path the high-water mark is only persisted
> *after* the loop completes (`:297-301`); when the loop aborts, the watermark is never advanced, so the
> next poll re-fetches the same window and fails on the same poison entry again — the incremental sync
> can be stuck indefinitely behind one bad item.
>
> **Recommendation:** Wrap each item's processing in its own `try`/`catch` (re-throwing
> `OperationCanceledException`), log-and-continue on failure, and increment an error counter so the
> cycle completes and the watermark can advance past entries that cannot be processed. Consider tracking
> the watermark as "min unprocessed" so a mid-batch failure does not silently skip earlier successes.

#### F-407 — `SyncOrchestrator` concentrates too many responsibilities

> **File:** `src/PlexToJellyfinSync.Service/SyncOrchestrator.cs:14-420` · **Criterion:** D · **Severity:** 🟡 Medium
>
> At ~420 lines the class owns owner-account resolution and caching, the incremental history pipeline,
> the full reconcile for both movie and series libraries, the season/series aggregate computation and
> writing, path-mapping orchestration, status mutation, and error handling. These are several distinct
> concerns; the aggregation logic (`UpdateSeriesAggregatesAsync`) and the reconcile-by-library-kind
> logic in particular could be separate collaborators. The monolith is hard to unit-test in isolation
> (see F-416) and makes the per-item error-handling gap (F-406) easy to miss.
>
> **Recommendation:** Extract at least the series/season aggregation and the reconcile strategy into
> their own types behind small interfaces, leaving `SyncOrchestrator` as a thin coordinator. This also
> opens each piece to focused unit tests.

#### F-408 — The Plex owner account id is cached for the process lifetime and never refreshed

> **File:** `src/PlexToJellyfinSync.Service/SyncOrchestrator.cs:28,76-81` · **Criterion:** A/D · **Severity:** 🔵 Low
>
> `ResolveOwnerAsync` memoises the owner id in `_ownerAccountId` with `??=` and never re-resolves it.
> Combined with F-303 (where `GetOwnerAccountIdAsync` swallows any failure and returns the fallback id
> `1`), a transient error or a not-yet-ready Plex server on the very first call permanently pins the
> orchestrator to account `1`. If that guess is wrong, the history filter uses the wrong account for the
> entire process lifetime and the sync silently processes the wrong user's (or no) history until restart.
>
> **Recommendation:** Only cache a *successfully* resolved id (let failures stay un-cached so the next
> cycle retries), or invalidate the cache when a cycle reports `PlexConnected = false`. At minimum,
> document that the owner id is resolved exactly once per process.

#### F-409 — Season/series NFO directories are derived from a fixed folder-layout assumption

> **File:** `src/PlexToJellyfinSync.Service/SyncOrchestrator.cs:192,212-213` · **Criterion:** A · **Severity:** 🔵 Low
>
> `UpdateSeriesAggregatesAsync` infers the season directory as `Path.GetDirectoryName(firstEpisodeLocal)`
> and the show directory as `GetDirectoryName(GetDirectoryName(episodeLocal))` — i.e. it assumes episode
> files sit one level under a per-season folder, which in turn sits one level under the show folder. For
> libraries that do not use per-season subfolders (all episodes directly under the show folder), flat
> layouts, or specials in non-standard locations, `season.nfo`/`tvshow.nfo` are written to the wrong
> directory (or the show NFO lands in the season folder). The watch state is correct but the file may be
> placed where Jellyfin will not read it.
>
> **Recommendation:** Derive the target directories from the item/season metadata where possible, or make
> the expected layout explicit and skip (with a warning) when the inferred directory does not match the
> expected structure.

#### F-410 — Two private reconcile helpers are inside `#region ISyncOrchestrator`

> **File:** `src/PlexToJellyfinSync.Service/SyncOrchestrator.cs:236,373,393,419` · **Criterion:** B · **Severity:** 🔵 Low
>
> `#region ISyncOrchestrator` (the interface-implementation region) contains the two public interface
> methods `ProcessHistoryAsync` and `ReconcileAsync`, but also the **private** helpers
> `ReconcileMovieLibraryAsync` (`:373`) and `ReconcileSeriesLibraryAsync` (`:393`). Under the CLAUDE.md
> rule, an interface region should hold only that interface's members; private helpers belong in the
> general `#region Methods` (which ends at `:234`). The region grouping is therefore mixed.
>
> **Recommendation:** Move the two private reconcile helpers up into `#region Methods` so the interface
> region contains only the interface implementation.

#### F-411 — The state file is written non-atomically; a corrupt file silently resets the watermark and re-syncs from "now"

> **File:** `src/PlexToJellyfinSync.Service/State/StateStore.cs:111-113,65-70` · **Criterion:** A · **Severity:** 🟠 High
>
> `SetHighWaterMarkAsync` writes `state.json` directly with `new FileStream(_filePath, FileMode.Create,
> …)`, truncating the existing file before the new JSON is serialised. A crash or cancellation between
> truncation and flush leaves a zero-length or partial file. On the next start `ReadAsync` catches the
> resulting `JsonException`, logs a warning, and returns a **fresh** `SyncStateFile` with
> `HighWaterMark == null` — which sends `ProcessHistoryAsync` down its first-run branch
> (`SyncOrchestrator.cs:248-261`), resetting the watermark to `DateTimeOffset.UtcNow`. The net effect is
> that all watch history accumulated between the crash and the restart is silently skipped: the very
> data-integrity question criterion A asks ("Is the state file written atomically?") is answered "no",
> and the corrupt-file recovery quietly loses sync position rather than alerting.
>
> **Recommendation:** Write to a temp file and atomically rename over `state.json` (mirrors F-401). On a
> corrupt read, preserve the bad file (rename to `state.json.corrupt`) and surface an error/metric rather
> than silently restarting from "now", so the watermark reset is visible.

#### F-412 — No schema version or migration strategy for the state file

> **File:** `src/PlexToJellyfinSync.Service/State/SyncStateFile.cs:6-15` · **Criterion:** D · **Severity:** 🔵 Low
>
> `SyncStateFile` carries a single `HighWaterMark` property and no version/schema marker. System.Text.Json
> tolerates missing and (by default) unknown properties, so the format is leniently forward/backward
> compatible today, but there is no explicit version field to key future migrations off. If the format
> ever changes shape (renamed/removed fields, semantic changes), there is no way to detect the old
> version and migrate it deliberately — old files would be reinterpreted under the new schema's defaults.
>
> **Recommendation:** Add an explicit `int SchemaVersion` (or similar) now while the file is trivial, and
> branch on it in `ReadAsync` when/if the format evolves. Low urgency given the single field, but cheap to
> add before the file shape matters.

#### F-413 — A throwing `Changed` subscriber propagates into the worker

> **File:** `src/PlexToJellyfinSync.Service/SyncStatusService.cs:66-74` · **Criterion:** A · **Severity:** 🔵 Low
>
> `Update` invokes `Changed?.Invoke()` (correctly outside the lock) but with no exception isolation. The
> subscribers are Blazor dashboard components; if any handler throws, the exception unwinds back through
> `Update` into whatever called it — most often `SyncOrchestrator`, where it is caught by the cycle-level
> `catch` and treated as a sync failure (incrementing `Errors`, aborting the cycle per F-406). A UI-side
> bug should not be able to disrupt the background sync.
>
> **Recommendation:** Invoke the handlers defensively — iterate `Changed.GetInvocationList()` (or wrap the
> single `Invoke`) in a `try`/`catch` that logs and continues — so a faulty subscriber cannot break the
> worker.

#### F-414 — `GetSnapshot` hand-copies every property and silently drops fields added later

> **File:** `src/PlexToJellyfinSync.Service/SyncStatusService.cs:43-63` · **Criterion:** D · **Severity:** ⚪ Note
>
> The snapshot is built by manually copying all twelve `SyncStatusViewData` properties into a new
> instance. This is correct and thread-safe today, but it is a maintenance trap: adding a property to
> `SyncStatusViewData` without also adding a copy line here makes the dashboard silently miss the new
> field, with no compiler help.
>
> **Recommendation:** Generate the copy from a single source of truth — e.g. make `SyncStatusViewData` a
> `record` and use `with`/a copy constructor, or implement a `Clone()` on the model — so new properties
> are included automatically.

#### F-415 — `Changed` fires on every mutation, including per-item counters

> **File:** `src/PlexToJellyfinSync.Service/SyncStatusService.cs:66-74` · **Criterion:** D · **Severity:** 🔵 Low
>
> `Update` raises `Changed` unconditionally on each call. The orchestrator calls `Update` very
> frequently — `s.ItemsProcessed++` runs once per processed item, and `RecordOutcome` once per write — so
> a full reconcile of a large library raises thousands of change notifications, each of which re-renders
> the connected Blazor dashboard circuits. This is a scalability concern for the explicitly in-scope
> "large libraries" case (it also amplifies F-413, since every notification is a chance for a subscriber
> to throw).
>
> **Recommendation:** Debounce/throttle change notifications (e.g. coalesce to at most one render per N
> milliseconds) or only raise `Changed` for state transitions that matter to the UI, rather than on every
> counter increment.

#### F-416 — Four of the five sync/persistence classes have no tests

> **File:** `src/PlexToJellyfinSync.Service/SyncOrchestrator.cs`,
> `src/PlexToJellyfinSync.Service/State/StateStore.cs`,
> `src/PlexToJellyfinSync.Service/State/SyncStateFile.cs`,
> `src/PlexToJellyfinSync.Service/SyncStatusService.cs` · **Criterion:** D · **Severity:** 🟡 Medium
>
> Of the five files in this session, only `NfoWriter` has a test class (`NfoWriterTests`), and even that
> covers only the `MediaKind.Movie` happy paths (the idempotent "no change → `Skipped`" path, the other
> root names, the malformed-XML branch and the filename strategies are untested — see F-703). The
> orchestration logic (`SyncOrchestrator` — watermark advancement, the history→aggregation→write
> pipeline, the per-item error path of F-406), the atomic-persistence behaviour of `StateStore`
> (round-trip, corrupt-file recovery, the watermark reset of F-411), and the concurrency of
> `SyncStatusService` (parallel Blazor reads vs. worker writes) are entirely unverified — precisely the
> behaviours the process doc flags as critical ("sync writes wrong watch states", atomic state writes,
> thread safety). This overlaps F-702 (recorded in Session 7) and is restated here as the test-gap
> conclusion for this session.
>
> **Recommendation:** Prioritise tests for `SyncOrchestrator` (mocked `IPlexClient`/`INfoWriter`/
> `IStateStore`, asserting one failing item does not abort the cycle and that the watermark advances) and
> `StateStore` (round-trip plus corrupt-file and atomic-write behaviour), then `SyncStatusService`
> snapshot/notification concurrency. Per the process doc, do **not** add the tests as part of this
> review — tracked here as a gap.

### Session 5 — Service III (Logging & DI)

The logging sink and the DI registration are small, disciplined and clean against criterion B: all
four C# files use file-scoped namespaces, hold exactly one top-level `public sealed class` /
`static class`, wrap their members in `#region` blocks grouped by kind (`Fields`, `Constructors`,
`Events`, the interface regions `ILoggerProvider`/`ILogStore`/`ILogger` named after the interface and
not ending in "implementation", and `IDisposable`), carry English XML docs on every member, use
`== false` (`IsEnabled(logLevel) == false`, `string.IsNullOrWhiteSpace(...) == false`), `var`,
LINQ method syntax and `_camelCase` readonly fields, and have no primary constructors. `using`
directives sit outside the namespace, System first (`System.Collections.Concurrent` before the
`Microsoft.*` / `PlexToJellyfinSync.*` groups), files are CRLF with no trailing newline — **no B
findings**.

The core mechanics are sound. **The ring buffer is correctly bounded and thread-safe:**
`InMemoryLogStore` guards a `Queue<LogEntry>` with a `Lock`, enqueues under the lock and dequeues
down to `Math.Max(1, LogBufferSize)` so memory is hard-capped (default 1000) with no leak, and
`GetEntries` returns a `ToList` snapshot taken under the lock — so the Worker writing and the Blazor
circuits reading never tear. `InMemoryLogProvider` caches one `InMemoryLogger` per category in a
`ConcurrentDictionary` (the `GetOrAdd` factory may run twice under a race but only one instance is
stored — harmless), carries `[ProviderAlias("InMemory")]` so configuration can target it by alias,
and `Dispose` clears the cache; it holds no unmanaged/disposable resources, so the trivial `Dispose`
is correct. **Registration is complete:** every Core abstraction is bound to exactly one
implementation — `ILogStore`→`InMemoryLogStore`, `ISyncStatusProvider`→`SyncStatusService`,
`IPathMapper`→`PathMapper`, `INfoWriter`→`NfoWriter`, `IStateStore`→`StateStore`,
`ISyncOrchestrator`→`SyncOrchestrator`, `IPlexClient`→`PlexClient` (typed client) — plus the
concrete `WatchAggregator`. **Lifetimes are right for the Worker/Blazor interplay:** all shared
state is `Singleton`, and the `X-Plex-Token` is attached as an HTTP *header* on the typed client
(not a query string), consistent with the Session 3 token-handling note.

The findings below concern: the logger's hardcoded level floor that ignores logging configuration
(A); the unguarded, un-coalesced `EntryAdded` event (A/D); the store being an unredacted mirror of
all log text on a dashboard that is public by default (C); the absence of options validation /
`ValidateOnStart` on the registration (D); a magic-string options binding (B/D); the split
registration site for the logger provider (D); and the template build-determinism setting (E). The
typed-client **captive dependency** is the focus-area item raised as **F-304** in Session 3 — this
session **confirms** it from the registration side: `AddHttpClient<IPlexClient, PlexClient>`
(transient handler, default 2-minute rotation) is consumed only by singletons
(`SyncOrchestrator`), so `IHttpClientFactory`'s handler rotation never takes effect. For the single
fixed internal Plex host this is acceptable if adopted deliberately; otherwise inject
`IHttpClientFactory` and create a per-cycle client (see F-304). No new ID is assigned; F-304 is
listed against `ServiceCollectionExtensions.cs` in the checklist.

#### F-501 — `InMemoryLogger.IsEnabled` hardcodes an Information floor and ignores logging configuration

> **File:** `src/PlexToJellyfinSync.Service/Logging/InMemoryLogger.cs:45-48` · **Criterion:** A · **Severity:** 🔵 Low
>
> `IsEnabled` returns `logLevel >= LogLevel.Information` unconditionally. The framework's composite
> logger applies the configured `Logging:LogLevel` filters (including any rule for the `InMemory`
> alias) *before* calling this sink, so configuration can only ever raise the threshold — this
> hardcoded floor means `Debug`/`Trace` can **never** reach the in-memory store even when explicitly
> enabled in configuration. As a direct consequence the live log view
> (`Components/Pages/Logs.razor:13-14`) offers `Trace` and `Debug` options in its level dropdown that
> can never display anything, because the store never captures below `Information`. The sink also
> ignores per-category configuration of its own (it relies entirely on the upstream filter), which is
> acceptable, but the fixed floor is a silent limitation rather than a configurable one.
>
> **Recommendation:** Drop the hardcoded comparison and let the configured filter decide
> (`return true;`, or honor a `DashboardOptions`-supplied minimum level), so the dashboard can surface
> `Debug`/`Trace` when configured and the UI's level options are meaningful. If an Information floor is
> intended, make it explicit/configurable and align the `Logs.razor` dropdown to it.

#### F-502 — `EntryAdded` is raised per entry with no exception isolation and no coalescing

> **File:** `src/PlexToJellyfinSync.Service/Logging/InMemoryLogStore.cs:66` · **Criterion:** A/D · **Severity:** 🔵 Low
>
> `Add` raises `EntryAdded?.Invoke(entry)` (correctly outside the lock, and the delegate is captured to
> a local so concurrent subscribe/unsubscribe is safe), but with two weaknesses. First, **no exception
> isolation:** if a subscriber throws synchronously, the exception unwinds back through `Add` into
> `InMemoryLogger.Log` and thus into *whatever code emitted the log line* — most often the Worker /
> `SyncOrchestrator`, where it is caught by the cycle-level handler and counted as a sync failure
> (the same class of problem as F-413 for `SyncStatusService.Changed`). The current Blazor subscriber
> (`Logs.razor:60-72`) marshals via `InvokeAsync`, so it does not throw synchronously today, but the
> contract is unguarded. Second, **the event fires on every single entry:** a full reconcile of a large
> library emits thousands of log lines, each raising `EntryAdded`, and the dashboard handler re-reads
> the full snapshot and re-renders on each — the logging analogue of the status-notification flooding
> in F-415, a scalability concern for the in-scope "large libraries" case.
>
> **Recommendation:** Invoke subscribers defensively (iterate `GetInvocationList()` / wrap in
> `try`/`catch` that logs-and-continues) so a faulty UI handler cannot disrupt the Worker, and consider
> coalescing/throttling notifications (e.g. at most one per N ms) — or let the consumer poll — so log
> bursts do not flood the Blazor circuits.

#### F-503 — The in-memory store mirrors all log text unredacted into a dashboard that is public by default

> **File:** `src/PlexToJellyfinSync.Service/Logging/InMemoryLogger.cs:58-67`,
> `src/PlexToJellyfinSync.Service/Logging/InMemoryLogStore.cs:54-67` · **Criterion:** C · **Severity:** 🔵 Low
>
> The logger copies `Message = formatter(state, exception)` and `Exception = exception?.ToString()`
> verbatim into the store, which feeds the live log view. The sink is **category-agnostic** — it
> captures every category at `Information`+, including framework logs (e.g. `Microsoft.Extensions.Http`)
> — and performs **no scrubbing/redaction**. Today the Plex token does not reach the logs (it is sent as
> a header and never logged — Session 3), so there is no active leak. But the design is leak-prone by
> construction: anything a future log statement or a more verbose logging configuration (e.g. enabling
> HttpClient request/header logging) puts into a message or exception is surfaced unfiltered in a web UI
> that, per F-103, is **publicly reachable when `Dashboard.Token` is empty** (the default). The two
> defaults compound: an unauthenticated dashboard plus an unredacted full-fidelity log mirror.
>
> **Recommendation:** Treat the log view as a potential exfiltration surface: keep the dashboard
> authenticated by default (F-103), and consider a redaction pass (mask known secret patterns / the
> configured token) at the sink before storing, or restrict captured categories. At minimum, document
> that operators must not enable secret-bearing log categories while the dashboard is exposed.

#### F-504 — Options are bound without DataAnnotations validation or `ValidateOnStart`

> **File:** `src/PlexToJellyfinSync.Service/ServiceCollectionExtensions.cs:29-34` · **Criterion:** D · **Severity:** 🟡 Medium
>
> Every options type is registered with a plain `services.Configure<T>(section)` and **no validation**:
> there is no `AddOptions<T>().Bind(...).ValidateDataAnnotations().ValidateOnStart()`. This is the
> registration-side counterpart to F-101 (the options classes carry no constraints): a missing
> `Plex:BaseUrl`/`Plex:Token`, a zero/negative `Sync:PollIntervalSeconds` /
> `Sync:FullReconcileIntervalHours`, or a bad `State:Directory` is not caught at startup and only
> surfaces later as a runtime failure (first HTTP call, a broken timer, or a write error). The process
> doc's criterion D explicitly asks for options "validatable at startup", and since this method is the
> single place the options are registered, the validation wiring belongs here.
>
> **Recommendation:** Switch to `AddOptions<T>().Bind(configuration.GetSection(T.SectionName))
> .ValidateDataAnnotations().ValidateOnStart()` for the options that have required/ranged members
> (pairs with the DataAnnotations recommended in F-101), so misconfiguration fails fast at boot rather
> than mid-run.

#### F-505 — `PathMappings` is bound via a magic-string section name and as a bare `List<PathMapping>`

> **File:** `src/PlexToJellyfinSync.Service/ServiceCollectionExtensions.cs:34` · **Criterion:** B/D · **Severity:** 🔵 Low
>
> Every other options binding uses the type's `SectionName` constant
> (`PlexOptions.SectionName`, `SyncOptions.SectionName`, …), but the path mappings are bound with a raw
> string literal `configuration.GetSection("PathMappings")` and as `IOptions<List<PathMapping>>` rather
> than a dedicated options class. The magic string is the kind of un-checked constant the
> `SectionName` convention exists to avoid (a typo here or in `appsettings.json` silently yields an
> empty list and every item becomes "unmapped"), and binding a bare collection skips the place where a
> `SectionName` constant and any validation would naturally live.
>
> **Recommendation:** Introduce a small options type (e.g. `PathMappingOptions` with a
> `List<PathMapping> Mappings` and a `SectionName` constant) or at least hoist the `"PathMappings"`
> literal into a shared constant, so the section name is defined once and is consistent with the other
> options.

#### F-506 — The in-memory logger provider is wired in the Host, separate from its `ILogStore`

> **File:** `src/PlexToJellyfinSync.Service/ServiceCollectionExtensions.cs:36`,
> `src/PlexToJellyfinSync/Program.cs:41-42` · **Criterion:** D · **Severity:** ⚪ Note
>
> `AddPlexToJellyfinSync` registers `ILogStore` (the buffer), but the `InMemoryLogProvider` that feeds
> it is registered separately in `Program.cs` as `AddSingleton<ILoggerProvider>(...)`. The two halves
> of one logging pipeline therefore live in two registration sites, so a consumer who calls
> `AddPlexToJellyfinSync` gets the store but **no** provider populating it unless they also remember the
> Host wiring. This is a cohesion observation, not a bug (an `ILoggerProvider` can be added to a plain
> `IServiceCollection`, exactly as the Host does).
>
> **Recommendation:** Optionally move the `ILoggerProvider`/`InMemoryLogProvider` registration into
> `AddPlexToJellyfinSync` (or a dedicated `AddInMemoryLogging` extension) so the store and its provider
> are wired together and the logging pipeline is self-contained.

#### F-507 — `Deterministic` disabled in both build configurations

> **File:** `src/PlexToJellyfinSync.Service/PlexToJellyfinSync.Service.csproj:8,13` · **Criterion:** E · **Severity:** 🔵 Low
>
> Both the Debug and Release `PropertyGroup`s set `<Deterministic>False</Deterministic>` — the same
> template-inherited override flagged for Core (F-105), Data (F-202) and the test project (F-704), now
> confirmed in a fourth project. Deterministic compilation is the default and is desirable for
> reproducible/CI builds. Otherwise the `.csproj` is clean under criterion E: the two
> `PackageReference`s (`Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Http`) carry **no inline
> versions** (CPM-compliant via `Directory.Packages.props`), the Reihitsu.Analyzer is wired in centrally
> via `Directory.Build.props` (no per-project analyzer reference needed), and the `ProjectReference`s
> point only to **Core and Data** as required — both of which the Service actually consumes (unlike the
> dead Data→Core reference in F-201).
>
> **Recommendation:** Remove the override (or set `True`), ideally by centralising the decision in
> `Directory.Build.props` together with F-105/F-202/F-704.

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
| `src/PlexToJellyfinSync.Service/NfoWriter.cs` | 4 | deep | ✅ | F-401, F-402, F-403, F-404, F-405 |
| `src/PlexToJellyfinSync.Service/SyncOrchestrator.cs` | 4 | deep | ✅ | F-406, F-407, F-408, F-409, F-410, F-416 |
| `src/PlexToJellyfinSync.Service/State/StateStore.cs` | 4 | deep | ✅ | F-411, F-412, F-416 |
| `src/PlexToJellyfinSync.Service/State/SyncStateFile.cs` | 4 | deep | ✅ | F-412, F-416 |
| `src/PlexToJellyfinSync.Service/SyncStatusService.cs` | 4 | deep | ✅ | F-413, F-414, F-415, F-416 |
| `src/PlexToJellyfinSync.Service/Logging/InMemoryLogProvider.cs` | 5 | deep | ✅ | none |
| `src/PlexToJellyfinSync.Service/Logging/InMemoryLogStore.cs` | 5 | deep | ✅ | F-502, F-503 |
| `src/PlexToJellyfinSync.Service/Logging/InMemoryLogger.cs` | 5 | deep | ✅ | F-501, F-503 |
| `src/PlexToJellyfinSync.Service/ServiceCollectionExtensions.cs` | 5 | deep | ✅ | F-304, F-504, F-505, F-506 |
| `src/PlexToJellyfinSync.Service/PlexToJellyfinSync.Service.csproj` | 5 | quick | ✅ | F-507 |
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
