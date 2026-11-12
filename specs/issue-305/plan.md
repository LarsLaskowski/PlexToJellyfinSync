# Plan: Meet CA1716, CA1720, CA1822 and CA1848 of the shared .NET rule baseline

Source: Issues #305, #306, #307, #308 (one shared run, one pull request)
Tier: standard — renames of public API, a DI registration removed and every remaining `LoggerExtensions` call moved to source-generated log methods change production code and control flow beyond `trivial`, without creating, changing or removing a security control.
Security areas touched: Dashboard authentication (`ForwardedHeadersWarning`, log call only); Path mapping and file writes (`MediaItemWriter` log calls, `SeriesAggregateWriter` constructor); HTTP calls to Plex (`PlexMetadata` DTO property renamed, `PlexClient.ParseUniqueIds`); Logging of external data (the log statements that carry rating keys, titles, file paths, the sender address and exceptions, and the `InMemoryLogger` tests); build configuration (`.editorconfig` analyzer severities written by `dotnet-rules.py`, `.squad/tools/squad_settings.py`)

## Problem / root cause

The squad refresh (#310) listed four rules in `DOTNET_RULES_PENDING` (`.squad/tools/squad_settings.py:26`), which
`dotnet-rules.py` writes as `suggestion` (`.editorconfig:558-566`). Without the entries the rules run at the severity
of `AnalysisLevel` `latest-recommended` (`warning`, so `error` under `TreatWarningsAsErrors`). Today's diagnostics:

- CA1716: the page component class `Error` (`src/PlexToJellyfinSync/Components/Pages/Error.razor.cs:10`).
- CA1720: `PlexMetadata.Guid` (`src/PlexToJellyfinSync.Data/Plex/PlexMetadata.cs:142`).
- CA1822: `WatchAggregator.Aggregate` (`src/PlexToJellyfinSync.Service/WatchAggregator.cs:17`).
- CA1848, 19 calls: `LibraryReconciler.cs:65`, `MediaItemWriter.cs:72,81`, `SyncOrchestrator.cs:88,110`,
  `SyncStatusService.cs:94` (all in `src/PlexToJellyfinSync.Service/`), `src/PlexToJellyfinSync/Worker.cs:65,88,104,135`,
  `src/PlexToJellyfinSync/Security/ForwardedHeadersWarning.cs:49`, `tests/PlexToJellyfinSync.Tests/InMemoryLogProviderTests.cs:43,44`,
  `tests/PlexToJellyfinSync.Tests/InMemoryLoggerTests.cs:42,63,64,80,102,123`.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | #308: 39 CA1848 diagnostics, in NfoWriter, PlexClient, StateStore and Program.cs among others | Scratch worktree with `DOTNET_RULES_PENDING = {}`, both scripts run, `dotnet build -c Release --no-incremental -p:TreatWarningsAsErrors=false`: 19 CA1848 (list above). #310 (82a4ecf) already moved NfoWriter, PlexClient, StateStore and Program.cs to `NfoWriterLog`, `PlexClientLog`, `StateStoreLog`, `ProgramLog`; the issue's lines there are stale; 19 remain | refuted |
| 2 | #305, #306, #307: one diagnostic each, at the lines the issues name | Same scratch build | confirmed |
| 3 | With the entries gone the rules are `warning`, so the build fails on any leftover | `analysislevel_11_recommended.globalconfig` of SDK 11.0.100 sets CA1716, CA1720, CA1822, CA1848 to `warning`; `TreatWarningsAsErrors` in `Directory.Build.props` | confirmed |
| 4 | #308: "the project's code style already asks for `[LoggerMessage]`" | Neither `AGENTS.md` nor `.squad/stack.md` says so; only the practice of the four `*Log.cs` classes of #310 does; a stack.md line is added (*Documentation updates*) | refuted |
| 5 | A product PR may edit `squad_settings.py` and `.editorconfig` | `.squad/tools/scope-check.py:90-93` (`SETTINGS_FILE` is no squad file outside a fork); `.editorconfig` is no squad file; `.squad/routing.md`, *Scope of a product PR* | confirmed |
| 6 | Emptying the dict and running both scripts removes only the ten pending lines | Scratch run: `.editorconfig` 10 deletions, `sonar-rules.py`: block current | confirmed |
| 7 | The route does not depend on the class name | `@page "/Error"` in `Error.razor:1`; `UseExceptionHandler("/Error", …)` in `Program.cs:62`; route table `docs/areas/dashboard-and-auth.md:22` | confirmed |
| 8 | The DTO binds the Plex field by attribute, not by property name | `[JsonPropertyName("Guid")]` at `PlexMetadata.cs:141` | confirmed |
| 9 | `WatchAggregator` holds no state; its users | `WatchAggregator.cs` (no field); `SeriesAggregateWriter.cs:17,33,38,90,111`; `ServiceCollectionExtensions.cs:43`; tests: `WatchAggregatorTests.cs` (7 tests), `SeriesAggregateWriterTests.cs:499`, `LibraryReconcilerTests.cs:762`, `SyncOrchestratorTests.cs:1896`, `ServiceCollectionExtensionsTests.cs:65` | confirmed |
| 10 | Released record 0018 stays true | 0018 (in v1.6.0) says the aggregate writer aggregates "through `WatchAggregator`", which a static call still does | confirmed |
| 11 | CA1848 does not flag the interface method `ILogger.Log<TState>` | to verify: Tester, step 5, *Analyzer gate* on the adapted test files (it reports the pending rule at `suggestion` in a changed file) | unverified |
| 12 | Source-generated log methods check `IsEnabled` before calling `ILogger.Log` | to verify: Tester, step 5, reading the generated source (`-p:EmitCompilerGeneratedFiles=true`) in a scratch worktree; only the reason for the test approach | unverified |
| 13 | The converted calls log event id 0 today; no test, document or the in-memory store reads event ids | `InMemoryLogger.cs` `Log` drops `eventId`; grep of `tests/` and `docs/` | confirmed |
| 14 | Which converted messages a test pins | `WorkerTests.cs:74,151,248,283`, `ForwardedHeadersWarningTests.cs:40`, `SyncStatusServiceTests.cs:110-111`; MediaItemWriter, LibraryReconciler, SyncOrchestrator and the two Worker information lines log into `NullLogger` in every test | confirmed |
| 15 | Renaming a `.razor` file needs the *Image build check* | `.squad/stack.md`, *Image build check* | confirmed |

## Acceptance criteria

- AC1: (#305) The error page component is `ErrorPage` (`Components/Pages/ErrorPage.razor` and `ErrorPage.razor.cs`;
  `Error.razor` and `Error.razor.cs` are gone), keeps the route `/Error` and renders as before: the heading "An error
  occurred while processing your request." always, the request id (`Activity.Current?.Id`, else
  `HttpContext.TraceIdentifier`) only when there is one.
- AC2: (#305) No CA1716 diagnostic with the rule at its `AnalysisLevel` severity.
- AC3: (#306) `PlexMetadata.Guid` is renamed to `ExternalIds`, still bound to the Plex JSON field `Guid`; the external
  identifiers still deserialize and still map to `MediaItem` unique ids.
- AC4: (#306) No CA1720 diagnostic with the rule at its `AnalysisLevel` severity.
- AC5: (#307) `WatchAggregator` is a static class whose static `Aggregate` returns the same results; it is no longer
  registered in DI, and `SeriesAggregateWriter` calls it directly without a constructor parameter for it.
- AC6: (#307) No CA1822 diagnostic with the rule at its `AnalysisLevel` severity.
- AC7: (#308) Every log statement of `src/` that called a `LoggerExtensions` method, and the two
  `LoggerMessage.Define` delegates of `SyncOrchestrator`, log through a `[LoggerMessage]` method with the same level,
  the same message template character for character, the same arguments and the same exception.
- AC8: (#308) No CA1848 diagnostic with the rule at its `AnalysisLevel` severity, in production and test code.
- AC9: (#305–#308) `DOTNET_RULES_PENDING = {}` in `.squad/tools/squad_settings.py`; `.editorconfig` as
  `python3 .squad/tools/dotnet-rules.py` and `python3 .squad/tools/sonar-rules.py` write it, with no `pending:` line.
- AC10: (#308) The `InMemoryLogger` and `InMemoryLogProvider` tests call `ILogger.Log` instead of the extension
  methods and keep every assertion.

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | `ErrorPageRouteIsErrorPath`, `ErrorPageWithoutRequestIdShowsNoRequestId`, `ErrorPageWithCurrentActivityShowsItsId` (deferred to step 6: the skeleton's rename is the whole change) | `tests/PlexToJellyfinSync.Tests/ErrorPageTests.cs` |
| AC2 | check: *Build* and the *Analyzer gate* after AC9 (step 6 Dev, step 7 stamp) | — |
| AC3 | `PlexMetadataResponseWithLowercaseGuidDeserializesGuidArray`, `PlexClientGetMediaItemMapsUniqueIds`, `PlexClientGetMediaItemSkipsNullGenreAndGuidElements` (existing) | `tests/PlexToJellyfinSync.Tests/PlexMetadataDeserializationTests.cs`, `tests/PlexToJellyfinSync.Tests/PlexClientTests.cs` |
| AC4 | check: *Build* and the *Analyzer gate* after AC9 | — |
| AC5 | the seven `WatchAggregatorTests`, `ServiceCollectionExtensionsRegistersSyncPipeline` (existing, adapted) | `tests/PlexToJellyfinSync.Tests/WatchAggregatorTests.cs`, `tests/PlexToJellyfinSync.Tests/ServiceCollectionExtensionsTests.cs` |
| AC6 | check: *Build* and the *Analyzer gate* after AC9 | — |
| AC7 | `WorkerStartupReconcileFailureLogsFullReconcileFailedError`, `WorkerProcessHistoryFailureLogsIncrementalHistorySyncFailedError`, `ForwardedHeadersWarningUntrustedForwardedForLogsOnce`, `SyncStatusServiceUpdateThrowingSubscriberIsIsolated` (existing) | `tests/PlexToJellyfinSync.Tests/WorkerTests.cs`, `tests/PlexToJellyfinSync.Tests/ForwardedHeadersWarningTests.cs`, `tests/PlexToJellyfinSync.Tests/SyncStatusServiceTests.cs` |
| AC8 | check: *Build* and the *Analyzer gate* after AC9 | — |
| AC9 | check: `python3 .squad/tools/dotnet-rules.py --check` and `python3 .squad/tools/sonar-rules.py --check` pass, `grep -c "pending:" .editorconfig` is 0 (orchestrator, before step 6) | — |
| AC10 | `InMemoryLoggerInformationIsCaptured`, `InMemoryLoggerDebugIsDiscarded`, `InMemoryLoggerExceptionIsCaptured`, `InMemoryLoggerMessageWithTokenIsRedacted`, `InMemoryLoggerExceptionWithTokenIsRedacted`, `InMemoryLogProviderLoggersWriteIntoStore` (existing, adapted) | `tests/PlexToJellyfinSync.Tests/InMemoryLoggerTests.cs`, `tests/PlexToJellyfinSync.Tests/InMemoryLogProviderTests.cs` |

The templates no test pins (MediaItemWriter, LibraryReconciler, SyncOrchestrator, the two Worker information
lines) are compared line by line in the diff by the Reviewer (AC7).

## Approach

Order of the run:

1. Step 4 (Dev): the renames and the static class below, with the call sites, so *Build* passes. The Razor files
   move with `mv` and `git add` of both paths. No `[LoggerMessage]` work yet.
2. Step 5 (Tester): adapt the eight CA1848 calls of `InMemoryLoggerTests` and `InMemoryLogProviderTests` (AC10):
   each calls `ILogger.Log(level, default, message, exception, formatter)` through one private static helper per
   class (a string state with a formatter that returns it; the test's text already formatted, e.g. `"Processed the
   movie library"`); assertions, `IsEnabled` guards the tests already have, and test names unchanged. Direct calls keep
   `InMemoryLoggerDebugIsDiscarded` reaching the logger's own level filter. The Tester runs the *Analyzer gate*
   on the two files (fact 11) and reports fact 12.
3. Before step 6 (orchestrator): AC9 — set `DOTNET_RULES_PENDING = {}` (the Dev reports nothing; the text is fixed
   here), run both scripts, commit. From here *Build* fails on every leftover of the four rules.
4. Step 6 (Dev): the log classes below; then the *Image build check* (restore-order reproduction) for the renamed
   `.razor` file; the Tester writes `ErrorPageTests` in its coverage pass, rendering through `HtmlRenderer` as
   `DashboardTests` does and starting an `Activity` for the id case.

**CA1716.** Rename the component to `ErrorPage`; `@page "/Error"`, the markup and `Program.cs:62` stay unchanged.

**CA1720.** Rename the property to `ExternalIds` (XML doc unchanged, attribute `[JsonPropertyName("Guid")]` kept);
update `PlexClient.ParseUniqueIds` (`PlexClient.cs:143,148`).

**CA1822.** `public static class WatchAggregator` with a static `Aggregate`, body unchanged. Remove
`services.AddSingleton<WatchAggregator>()` (`ServiceCollectionExtensions.cs:43`), the `_aggregator` field and the
constructor parameter of `SeriesAggregateWriter`, which calls `WatchAggregator.Aggregate(…)` at lines 90 and 111.

**CA1848.** One `internal static partial class <Type>Log` per type, in the type's folder and namespace, shaped like
`PlexClientLog` (`#region Methods`, XML docs, `ILogger logger` first, an `Exception exception` parameter where the
call passes one). Each method's template is copied verbatim from the call it replaces, and its parameter names match
the placeholders. Event ids count from 1 per class; the call's level is kept:

| Class (new file) | Methods: event id, name, level — replaces |
| ---------------- | ----------------------------------------- |
| `WorkerLog` (`src/PlexToJellyfinSync/`) | 1 `ReconcileFailed` Error — `Worker.cs:65`; 2 `HistorySyncFailed` Error — `:88`; 3 `Started` Information — `:104`; 4 `Stopping` Information — `:135` |
| `ForwardedHeadersWarningLog` (`src/PlexToJellyfinSync/Security/`) | 1 `UntrustedForwardedHeaders` Warning, sender `IPAddress?` — `ForwardedHeadersWarning.cs:49` |
| `SyncOrchestratorLog` (`src/PlexToJellyfinSync.Service/`) | 1 `ItemKindSkipped` Debug and 2 `LibrarySkipped` Debug — the two `Define` delegates of `SyncOrchestrator.cs:18-24`, ids and names unchanged, fields deleted; 3 `RunFailed` Error — `:88`; 4 `ItemFailed` Error — `:110` |
| `LibraryReconcilerLog` (same) | 1 `ItemFailed` Error — `LibraryReconciler.cs:65` |
| `MediaItemWriterLog` (same) | 1 `ItemWithoutFilePath` Warning — `MediaItemWriter.cs:72`; 2 `NoPathMapping` Warning — `:81` |
| `SyncStatusServiceLog` (same) | 1 `SubscriberFailed` Warning — `SyncStatusService.cs:94` |

The existing `IsEnabled` guards (`Worker.cs:102`, `ForwardedHeadersWarning.cs:47`) and the order of every log call
relative to the status updates stay as they are. The only output that changes is the event id, 0 today (fact 13), in
providers that print it (the console logger); the dashboard's log buffer does not show it. No new user-visible output.

## Options considered

- **CA1716/CA1720 by a member-scoped suppression** — lost: the issues' "Done when" asks the code to meet the rule,
  and a suppression keeps the name every other .NET language trips over.
- **`Guids` instead of `ExternalIds`** — lost: `ExternalIds` names what the list holds (its XML doc, `ParseUniqueIds`)
  and carries no type name at all; the binding to `Guid` stays in the attribute.
- **`IWatchAggregator` with the class behind it** (`.squad/project.md` consumes services through an interface;
  CA1822 skips interface implementations) — lost: an interface for a pure function whose callers never need a
  double (every test uses the real aggregator) exists only to silence the rule.
- **Static `Aggregate` on a class still registered and injected** — lost: an injected instance used for nothing.
- **`LoggerMessage.Define` delegates** (SyncOrchestrator's form) **instead of `[LoggerMessage]`** — lost: #310 set
  the `<Type>Log` pattern for four types; one form keeps one place per type for templates and event ids, which is
  also why the two `Define` delegates move into `SyncOrchestratorLog` instead of new ids 3 and 4 beside them.
- **Tests through a test-local `[LoggerMessage]` class** — lost: generated methods check `IsEnabled` first
  (fact 12), so `InMemoryLoggerDebugIsDiscarded` would no longer reach the logger under test.
- **Four pull requests** — the user chose one.

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| PlexToJellyfinSync | `Components/Pages/Error.razor(.cs)` → `ErrorPage.razor(.cs)` | rename |
| PlexToJellyfinSync | `Worker.cs`, `Security/ForwardedHeadersWarning.cs`; new `WorkerLog.cs`, `Security/ForwardedHeadersWarningLog.cs` | log calls |
| PlexToJellyfinSync.Data | `Plex/PlexMetadata.cs` | property rename |
| PlexToJellyfinSync.Service | `WatchAggregator.cs`, `SeriesAggregateWriter.cs`, `ServiceCollectionExtensions.cs` | static class, DI |
| PlexToJellyfinSync.Service | `PlexClient.cs` | renamed property |
| PlexToJellyfinSync.Service | `SyncOrchestrator.cs`, `LibraryReconciler.cs`, `MediaItemWriter.cs`, `SyncStatusService.cs`; new `SyncOrchestratorLog.cs`, `LibraryReconcilerLog.cs`, `MediaItemWriterLog.cs`, `SyncStatusServiceLog.cs` | log calls |
| repository | `.squad/tools/squad_settings.py`, `.editorconfig` | AC9 (orchestrator) |

## Signatures (for the Dev's skeleton)

- `public partial class ErrorPage` in `src/PlexToJellyfinSync/Components/Pages/ErrorPage.razor.cs` (with `ErrorPage.razor`).
- Deleted: the class `Error` with `Error.razor` and `Error.razor.cs`.
- `public List<PlexGuid?>? ExternalIds { get; set; }` in `PlexMetadata`, attribute `[JsonPropertyName("Guid")]`.
- Deleted: the property `PlexMetadata.Guid`.
- `public static class WatchAggregator`
- `public static WatchInfo Aggregate(IReadOnlyCollection<WatchInfo> children)`
- `public SeriesAggregateWriter(IPlexClient plexClient, IPathMapper pathMapper, IMediaItemWriter itemWriter)`
- Deleted: the constructor parameter `WatchAggregator aggregator` of `SeriesAggregateWriter`.

The `<Type>Log` classes are internal and called only by their type (*Approach*); the skeleton adds none of them.
Names checked: no `Flags`/`Enum` suffix, no nested type, no keyword (CA1716), no type name (CA1720).

## Test files

- `tests/PlexToJellyfinSync.Tests/ErrorPageTests.cs` (new, Tester); the files of *Tests per criterion*.

Existing test call sites of changed signatures:

- Dev, step 4, mechanically: `PlexMetadataDeserializationTests.cs:77-79` (`Guid` → `ExternalIds`); the seven
  `WatchAggregatorTests` (`new WatchAggregator()` and `aggregator.Aggregate(…)` → `WatchAggregator.Aggregate(…)`);
  `SeriesAggregateWriterTests.cs:499`, `LibraryReconcilerTests.cs:762`, `SyncOrchestratorTests.cs:1896` (argument
  `new WatchAggregator()` dropped).
- `ServiceCollectionExtensionsTests.cs:65` asserts the registration that goes away and cannot compile against a
  static class (a static type is no type argument). Lead decision: the Dev deletes that one assertion line in step 4
  and lists it; the test still resolves `ISyncOrchestrator`, which fails if `SeriesAggregateWriter` still needed an
  aggregator. The Tester checks it in its coverage pass.
- Tester, step 5: `InMemoryLoggerTests.cs`, `InMemoryLogProviderTests.cs` (AC10).

Files no unit test can cover: none.

## Areas

none (no change in behavior; the `/Error` route in `docs/areas/dashboard-and-auth.md` stays).

## Documentation updates

- `docs/ARCHITECTURE.md` (Dev), point 3 of the sync pipeline: `WatchAggregator` is a static class, so nothing
  registers or injects it.
- `docs/UNIT_TESTS.md` (Tester), the example under *Arranging your tests*: `WatchAggregator.Aggregate(children)`
  without the instance.
- `.squad/stack.md` (Dev reports the text, the orchestrator writes it), *Writing code*: log through `[LoggerMessage]`
  methods of an internal static partial `<Type>Log` class next to the type, event ids from 1 per class, never
  through the `LoggerExtensions` methods; tests call `ILogger.Log` directly (CA1848).
- PR template checklist: no README, configuration or changelog change.

## Architecture check

No guarantee of `.squad/project.md` changes: per-item error isolation keeps its log-then-count order, the forwarded
headers warning still fires once, path mapping, NFO writes and DTO binding are untouched. The dependency arrow
`SeriesAggregateWriter --> WatchAggregator` of the diagram stays true.

## Security considerations

- [x] no limits.
- [x] none: the error page text is unchanged and shows no exception details.

Log content: every template and argument stays as it is, so no new value reaches a log (*Integration surface*,
a new logged value); `SecretLogRedactor` is untouched.

## Decision records

- none: conformance to analyzer rules with no choice of lasting weight; the logging convention goes into
  `.squad/stack.md`, and 0018 stays true (fact 10).

## Out of scope / follow-ups

- #300 (in the same pull request) is planned and approved separately.

## Challenge notes

Devil's Advocate, minor: fact 14 is wrong about the two Worker information lines. `WorkerTests` create a
`RecordingWorkerLogger` in every test (`WorkerTests.cs:64, 90, 118, 141, 166`), which records "Worker started; poll every
{Poll}, reconcile every {Reconcile}" (`Worker.cs:104`) and "Worker stopping" (`Worker.cs:135`); nothing asserts them. The
Tester's coverage pass uses `RecordingWorkerLogger` to assert one Information entry with the started text and both
intervals and one with "Worker stopping".

## Security notes

Security, non-blocking: the method name `UntrustedForwardedHeaders` of `ForwardedHeadersWarningLog` contains "trusted",
which `.squad/stack.md` (*Logger arguments*) lists for CodeQL's `cs/cleartext-storage-of-sensitive-information`. Name the
method and event `ForwardedHeadersNotApplied` instead; the message template stays, and
`ForwardedHeadersWarningUntrustedForwardedForLogsOnce` still pins it. Facts 11 and 12 stay with the Tester (step 5); Security
checks the measurement of fact 12 in the diff review.
