# Plan: Validate options at startup (required fields and value ranges)

Source: Issue #31 (pairs with #69, F-504 — the registration side; this change fixes both)
Status: Draft
Tier: security — the change adds a package dependency (`Microsoft.Extensions.Options.DataAnnotations`), changes how
the Plex token / base URL that feed the Plex `HttpClient` are bound and validated, and changes what the Docker
image does at startup with its shipped configuration defaults.

## Problem / root cause

- `src/PlexToJellyfinSync.Core/Options/PlexOptions.cs:22` `BaseUrl` defaults to `string.Empty` and carries no
  constraint. `src/PlexToJellyfinSync.Service/ServiceCollectionExtensions.cs:30-34` binds every options type with a
  plain `services.Configure<T>(section)` — no validation, no `ValidateOnStart`. A missing or malformed
  `Plex:BaseUrl` therefore starts the host normally: an empty value leaves `HttpClient.BaseAddress` unset (line 58
  guard) so every Plex request fails with an `InvalidOperationException` on each poll; a non-URI value such as
  `plex:32400`-style garbage or a bare `http://` makes `new Uri(options.BaseUrl)` (line 60) throw a
  `UriFormatException` on every `CreateClient` call. The error only appears as a recurring sync failure on the
  dashboard, never at boot.
- Numeric settings are not validated but are **clamped** at the point of use, which the issue text misses:
  `Worker.cs:101-102` (`Math.Max(5, PollIntervalSeconds)`, `Math.Max(1, FullReconcileIntervalHours)`),
  `SyncOrchestrator.cs:426,543` (`Math.Max(1, …Parallelism)`), `InMemoryLogStore.cs:34`
  (`Math.Max(1, LogBufferSize)`). So zero/negative values do not break the timer or the ring buffer today; they are
  silently replaced by a different value than the one configured.
- There is **no upper bound**, and that is a real latent crash: `Worker.cs:121` `Task.Delay(pollInterval, …)` throws
  `ArgumentOutOfRangeException` for any interval above `uint.MaxValue - 1` ms (~49.7 days, i.e.
  `PollIntervalSeconds > 4 294 967`). That exception is outside both `Safe…` guards and the `catch
  (OperationCanceledException)`, so `ExecuteAsync` faults and the host stops. `TimeSpan.FromHours` at line 102
  overflows likewise for `FullReconcileIntervalHours` above ~256 million.
- `src/PlexToJellyfinSync.Core/Options/StateOptions.cs:22` `Directory` accepts an empty value, which makes
  `StateStore` (`StateStore.cs:39`) write `state.json` relative to the working directory (`/app` in the image). The
  Dockerfile chowns `/app` to `APP_UID`, so the write succeeds, but `/app` is not a volume: the state is ephemeral and
  lost on every container restart (forcing a full re-sync) instead of persisting in the `/config` volume.

## Acceptance criteria

Options-class level (validated with `System.ComponentModel.DataAnnotations.Validator.TryValidateObject(options,
new ValidationContext(options), results, validateAllProperties: true)`, i.e. the same call
`ValidateDataAnnotations()` makes):

- [ ] AC1: A `PlexOptions` with `BaseUrl` `null`, `""` or whitespace-only is invalid; the result names member
  `BaseUrl` and its message contains `Plex:BaseUrl`.
- [ ] AC2: A `PlexOptions` whose `BaseUrl` is not an absolute `http`/`https` URI is invalid with a result naming
  `BaseUrl`: at least `plex:32400`, `/relative/path`, `ftp://plex:21`, `http://` and `not a url`.
- [ ] AC3: A `PlexOptions` with `BaseUrl` `http://plex:32400`, `https://plex.example.com` or
  `http://192.168.1.10:32400/` is valid.
- [ ] AC4: A `PlexOptions` with a valid `BaseUrl` and an empty `Token` is valid (token stays optional, see
  decision 0017).
- [ ] AC5: `PlexOptions.OwnerAccountId` `null` and `1` are valid; `0` and `-1` are invalid (member
  `OwnerAccountId`).
- [ ] AC6: `SyncOptions.PollIntervalSeconds` `5` and `86400` are valid; `4`, `0`, `-1` and `86401` are invalid
  (member `PollIntervalSeconds`).
- [ ] AC7: `SyncOptions.FullReconcileIntervalHours` `1` and `8760` are valid; `0`, `-1` and `8761` are invalid.
- [ ] AC8: `SyncOptions.EpisodeReconcileParallelism` and `LibraryReconcileParallelism`: `1` is valid; `0` and `-1`
  are invalid (each reported under its own member name).
- [ ] AC9: `DashboardOptions.LogBufferSize` `1` is valid; `0` and `-1` are invalid. `DashboardOptions.Token` empty is
  valid.
- [ ] AC10: `StateOptions.Directory` `""` and whitespace-only are invalid; `/config` is valid.
- [ ] AC11: A default-constructed `SyncOptions`, `DashboardOptions` and `StateOptions` are valid, and a
  `PlexOptions` whose only non-default value is `BaseUrl = "http://plex:32400"` is valid — i.e. every shipped
  default other than `Plex:BaseUrl` passes.
- [ ] AC11a: `PlexOptions.Validate(new ValidationContext(options))` called directly with `BaseUrl` `null`, `""` or
  whitespace-only yields no result (the blank case is left to `[Required]`, so it is never reported twice).
- [ ] AC12: No validation message ever contains the configured value: with `BaseUrl =
  "http://user:hunter2@"` (invalid) and `Token = "tok-secret-123"`, no `ValidationResult.ErrorMessage` contains
  `hunter2` or `tok-secret-123`; the same holds for the `OptionsValidationException.Message` in AC14.

Registration level (`ServiceCollectionExtensions.AddPlexToJellyfinSync` with an in-memory `IConfiguration`):

- [ ] AC13: With `Plex:BaseUrl` set to a valid URL and nothing else, `IOptions<T>.Value` resolves for `PlexOptions`,
  `SyncOptions`, `StateOptions`, `DashboardOptions` and `NfoOptions`, and
  `provider.GetRequiredService<IStartupValidator>().Validate()` does not throw.
- [ ] AC14: With an empty configuration, `IStartupValidator.Validate()` throws `OptionsValidationException`
  (without any options having been resolved first — proves `ValidateOnStart` is registered for `PlexOptions`).
- [ ] AC15: With a valid `Plex:BaseUrl` and one out-of-range value each — `Sync:PollIntervalSeconds = 0`,
  `Dashboard:LogBufferSize = 0`, `State:Directory = " "` — `IStartupValidator.Validate()` throws
  `OptionsValidationException`, and `IOptions<T>.Value` of the affected type throws `OptionsValidationException`
  whose `OptionsType` is that type (proves `ValidateDataAnnotations` + `ValidateOnStart` on `SyncOptions`,
  `DashboardOptions`, `StateOptions`).
- [ ] AC16: Binding is unchanged: the existing `ServiceCollectionExtensionsBindsConfigurationSections` and
  `ServiceCollectionExtensionsConfiguresPlexHttpClient` keep passing unchanged.
- [ ] AC17: The runtime clamps stay (defense-in-depth for options built without the DI validation, e.g.
  `Options.Create` in tests): the existing clamp tests in `SyncOrchestratorTests` and `InMemoryLogStoreTests` keep
  passing unchanged, and `Worker.cs`, `SyncOrchestrator.cs` and `InMemoryLogStore.cs` are not modified.

Note for the Tester: `ServiceCollectionExtensionsTests.BuildProvider()` is currently called with an empty
configuration by three tests that resolve `IPlexClient` / `ILogRedactor` (both read `IOptions<PlexOptions>.Value`
in their constructors). After this change those resolutions throw by design, so `BuildProvider`'s default must
become a minimal valid configuration (`Plex:BaseUrl = http://plex.test:32400`). That is an intended consequence of
AC14, not a silent test change; record it in `log.md`.

## Approach

1. **Annotate the options classes (Core).** `System.ComponentModel.DataAnnotations` is part of the shared
   framework — no package for `PlexToJellyfinSync.Core`.
   - `PlexOptions.BaseUrl`: `[Required]` plus an absolute-`http`/`https` check via `IValidatableObject` on
     `PlexOptions` (`Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri)` and `uri.Scheme` is `Uri.UriSchemeHttp`
     or `Uri.UriSchemeHttps`, also requiring a non-empty `uri.Host`). `[Url]` is **not** used: `UrlAttribute` only
     checks the `http://`/`https://`/`ftp://` prefix, so it accepts `http://` and `ftp://…`, both of which still fail
     later in `new Uri(...)` / against Plex. `Validate` yields nothing for a null/whitespace `BaseUrl` (that case is
     `[Required]`'s; `Validator` only calls `IValidatableObject.Validate` once all property attributes passed).
   - `PlexOptions.OwnerAccountId`: `[Range(1, int.MaxValue)]` (`null` passes, `RangeAttribute` ignores null).
   - `PlexOptions.Token`, `PlexOptions.Libraries`: no constraint.
   - `SyncOptions.PollIntervalSeconds`: `[Range(5, 86400)]` — the lower bound is the existing Worker floor, the upper
     bound (one day) stays far below the `Task.Delay` limit.
   - `SyncOptions.FullReconcileIntervalHours`: `[Range(1, 8760)]` — existing floor; one year upper bound.
   - `SyncOptions.EpisodeReconcileParallelism`, `LibraryReconcileParallelism`: `[Range(1, int.MaxValue)]`.
   - `DashboardOptions.LogBufferSize`: `[Range(1, int.MaxValue)]` (the queue does not preallocate, so no technical
     upper limit).
   - `StateOptions.Directory`: `[Required]`.
   - Every attribute sets an `ErrorMessage` that names the configuration key and its env var (e.g.
     `"Plex:BaseUrl is required (environment variable PLEXSYNC__Plex__BaseUrl)."`, `"Sync:PollIntervalSeconds must be
     between {1} and {2} (environment variable PLEXSYNC__Sync__PollIntervalSeconds)."`) and **never** a `{0}`-style
     placeholder for the value (`RangeAttribute` formats `{0}` = display name, `{1}` = min, `{2}` = max — the value is
     never a format argument, keep it that way). Exact wording is the Dev's; the key name in the message is AC1.
2. **Register with validation (Service).** In `AddPlexToJellyfinSync`, replace the four `services.Configure<T>(…)`
   calls for `PlexOptions`, `SyncOptions`, `StateOptions`, `DashboardOptions` with
   `services.AddOptions<T>().Bind(configuration.GetSection(T.SectionName)).ValidateDataAnnotations().ValidateOnStart();`.
   `NfoOptions` and `PathMappings` keep `Configure` (no annotations; `PathMappings` is #70 / F-505). The
   `if (string.IsNullOrWhiteSpace(options.BaseUrl) == false)` guard in the `HttpClient` callback stays (cheap, and
   tests may build the client without startup validation).
3. **Package.** Add `Microsoft.Extensions.Options.DataAnnotations` `10.0.12` (same version train as the existing
   `Microsoft.Extensions.*` packages) to `Directory.Packages.props` under `<!-- Runtime -->` and a version-less
   `<PackageReference>` to `src/PlexToJellyfinSync.Service/PlexToJellyfinSync.Service.csproj`. `Bind` comes from
   `Microsoft.Extensions.Options.ConfigurationExtensions` and `ValidateOnStart`/`IStartupValidator` from
   `Microsoft.Extensions.Options`, both already transitive via `Microsoft.Extensions.Hosting`.
4. **Host.** No change to `Program.cs`. An invalid configuration throws `OptionsValidationException` and stops the
   application at startup before `Worker` or Kestrel start, with a non-zero exit code and the message on stderr
   (`docker logs`). Which call surfaces it first is an implementation detail and is not promised in docs or records:
   today an invalid `PlexOptions` already throws inside `builder.Build()` (`Program.cs:41`), because the
   `ILoggerProvider` factory (`Program.cs:38-39`) resolves `SecretLogRedactor`, whose constructor reads
   `plexOptions.Value` (`SecretLogRedactor.cs:41`); an invalid `DashboardOptions` throws at `Program.cs:43`; the
   remaining cases are caught by `IStartupValidator` in `Host.StartAsync`. All paths are the same fail-fast outcome.
   AC14 calls `IStartupValidator.Validate()` directly and does not depend on this ordering.
5. **Docs** (Dev, see below). ARCHITECTURE wording that says parallelism values below 1 are "clamped rather than
   rejected" becomes "rejected at startup; the clamp remains as defense-in-depth".

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| Core | `Options/PlexOptions.cs` | `[Required]` on `BaseUrl`, `[Range]` on `OwnerAccountId`, implements `IValidatableObject` |
| Core | `Options/SyncOptions.cs` | `[Range]` on the two intervals and the two parallelism settings |
| Core | `Options/DashboardOptions.cs` | `[Range]` on `LogBufferSize` |
| Core | `Options/StateOptions.cs` | `[Required]` on `Directory` |
| Service | `ServiceCollectionExtensions.cs` | `AddOptions<T>().Bind().ValidateDataAnnotations().ValidateOnStart()` for the four types |
| Service | `PlexToJellyfinSync.Service.csproj` | `PackageReference` `Microsoft.Extensions.Options.DataAnnotations` |
| (root) | `Directory.Packages.props` | `PackageVersion` `Microsoft.Extensions.Options.DataAnnotations` `10.0.12` |
| Tests | new `PlexOptionsTests.cs`, `SyncOptionsTests.cs`, `DashboardOptionsTests.cs`, `StateOptionsTests.cs` (one file per type, per `docs/UNIT_TESTS.md`; the earlier "or one `OptionsValidationTests.cs`" alternative is withdrawn by the Lead's review-round-1 decision), shared helper `DataAnnotationsValidation.cs`; `ServiceCollectionExtensionsTests.cs` | AC1–AC15; `BuildProvider` default config |
| Docs | `README.md`, `docs/CONTRIBUTING.md`, `docs/ARCHITECTURE.md` | see below |

## Signatures (for the Dev's skeleton)

```csharp
// src/PlexToJellyfinSync.Core/Options/PlexOptions.cs
using System.ComponentModel.DataAnnotations;

public sealed class PlexOptions : IValidatableObject
{
    [Required(ErrorMessage = "…Plex:BaseUrl…")]
    public string BaseUrl { get; set; } = string.Empty;          // unchanged default

    public string Token { get; set; } = string.Empty;            // unchanged, no attribute

    [Range(1, int.MaxValue, ErrorMessage = "…Plex:OwnerAccountId…")]
    public int? OwnerAccountId { get; set; }

    public string[] Libraries { get; set; } = [];                // unchanged

    #region IValidatableObject
    /// <inheritdoc/>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext);
    #endregion // IValidatableObject
}

// src/PlexToJellyfinSync.Core/Options/SyncOptions.cs  (defaults unchanged)
[Range(5, 86400, ErrorMessage = "…")]          public int PollIntervalSeconds { get; set; } = 60;
[Range(1, 8760, ErrorMessage = "…")]           public int FullReconcileIntervalHours { get; set; } = 24;
[Range(1, int.MaxValue, ErrorMessage = "…")]   public int EpisodeReconcileParallelism { get; set; } = 4;
[Range(1, int.MaxValue, ErrorMessage = "…")]   public int LibraryReconcileParallelism { get; set; } = 2;

// src/PlexToJellyfinSync.Core/Options/DashboardOptions.cs  (default unchanged)
[Range(1, int.MaxValue, ErrorMessage = "…")]   public int LogBufferSize { get; set; } = 1000;

// src/PlexToJellyfinSync.Core/Options/StateOptions.cs  (default unchanged)
[Required(ErrorMessage = "…")]                 public string Directory { get; set; } = "/config";

// src/PlexToJellyfinSync.Service/ServiceCollectionExtensions.cs — public signature unchanged:
public static IServiceCollection AddPlexToJellyfinSync(this IServiceCollection services, IConfiguration configuration);
```

The skeleton's `Validate` body throws `NotImplementedException`; attributes may already be present in the
skeleton (they are declarations, not behavior) — the Tester's AC tests for the attributes then fail only through
the missing registration/`Validate` behavior, which is acceptable. If the Dev prefers to add attributes only in
step 6, that is fine too.

## Documentation updates

- `README.md` configuration table (lines 92–108):
  - `Plex:BaseUrl`: "Plex base URL — **required**, an absolute `http`/`https` URL (e.g. `http://plex:32400`); the
    container does not start without it".
  - `Plex:Token`: "Plex auth token (`X-Plex-Token`); required unless Plex allows unauthenticated access from this
    container's network".
  - `Plex:OwnerAccountId`: add "(≥ 1)".
  - `Sync:PollIntervalSeconds`: "Incremental poll interval (5–86400)".
  - `Sync:FullReconcileIntervalHours`: "Full reconcile interval (1–8760)".
  - The two parallelism rows already say "minimum 1" — keep.
  - `State:Directory`: add "(must not be empty)". `Dashboard:LogBufferSize`: add "(minimum 1)".
  - One sentence after the table: invalid or missing required settings stop the application at startup, before the
    sync worker or the dashboard start, with an error naming the setting (visible in `docker logs`), instead of
    failing on every sync. Do not describe which framework call raises it.
- `docs/CONTRIBUTING.md` lines 64–69 ("Running the app locally"): replace "without those the host still starts and
  serves the dashboard" — the host now refuses to start without a valid `Plex:BaseUrl`; tell the reader to set it
  (e.g. `PLEXSYNC__Plex__BaseUrl`) before `dotnet run`. Without a token/path mapping it still starts.
- `docs/ARCHITECTURE.md`:
  - Sync pipeline point 1: "`Sync:PollIntervalSeconds` (minimum 5s)" / "(minimum 1h)" → state the validated ranges
    (5–86400 s, 1–8760 h) and that the `Math.Max` floor remains as defense-in-depth.
  - Point 2, lines 74–78: replace "a configured value below that is clamped rather than rejected" with "a configured
    value below that is rejected at startup; the clamp remains as defense-in-depth".
  - "Configuration & dependency injection": add a bullet — `PlexOptions`, `SyncOptions`, `StateOptions` and
    `DashboardOptions` are registered with `ValidateDataAnnotations().ValidateOnStart()`; required settings and
    ranges are declared on the options classes; misconfiguration stops the application at startup before `Worker`
    or Kestrel start (no promise about the exact framework call that raises it);
    validation does no network I/O, so an unreachable Plex server still never blocks startup; link
    [0016](decisions/0016-options-validated-at-startup.md) and [0017](decisions/0017-plex-token-stays-optional.md).

## Architecture check

- NFO watch-field guarantee, unmapped-path skip, dashboard auth model: untouched.
- "A flaky Plex server never blocks startup" (point 4) is preserved: validation is purely syntactic (no DNS/HTTP),
  so reachability is still checked only at runtime. What changes is that an **absent or malformed** configuration now
  blocks startup — a deliberate, documented behavior change (decision 0016), requested by #31/#69.
- The documented "clamped rather than rejected" behavior for the parallelism settings (ARCHITECTURE point 2) is
  changed to "rejected at startup"; not one of the deliberate guarantees listed in the Lead charter, recorded in
  0016, ARCHITECTURE updated.
- `/health` and the dashboard are not available while the configuration is invalid (the host never starts). This is
  the intended fail-fast trade-off; recorded in 0016.

## Security considerations

- **No secret in validation output.** `OptionsValidationException` messages go to stderr / container logs, outside
  `SecretLogRedactor`. Messages must name the key, never the value — `Plex:Token` has no attribute at all, and the
  `BaseUrl` message must not echo the URL (it may carry `user:password@`). AC12 tests this.
- **New dependency.** `Microsoft.Extensions.Options.DataAnnotations` is first-party Microsoft, same release train and
  version as the already-referenced `Microsoft.Extensions.*` packages, covered by the existing NuGet Dependabot group.
  No transitive package outside `Microsoft.Extensions.*` / the shared framework.
- **Docker defaults.** The image's shipped `appsettings.json` has `"Plex": { "BaseUrl": "" }`, so a container started
  without `PLEXSYNC__Plex__BaseUrl` now exits at startup (and crash-loops under a `restart:` policy) instead of
  running a dashboard that reports a failure on every poll. Every other shipped default (`60`, `24`, `4`, `2`,
  `1000`, `/config`, empty tokens) passes validation (AC11). The README quick start already sets `BaseUrl`. No
  Dockerfile change.
- `State:Directory` empty no longer silently puts `state.json` under `/app`. `/app` is writable (the Dockerfile chowns
  it to `APP_UID`) but not a volume, so the state there was ephemeral and lost on restart; it now has to live in the
  configured directory (`/config` by default).
- Validation is reflection-based (`Validator`); no trimming/AOT is enabled for the publish, so no `IL2026` concern —
  the Code Officer confirms the analyzer check stays clean.

## Decision records

- `docs/decisions/0016-options-validated-at-startup.md` (Proposed) — fail fast on invalid configuration via
  DataAnnotations + `ValidateOnStart`; package choice; ranges and why the clamps stay.
- `docs/decisions/0017-plex-token-stays-optional.md` (Proposed) — `Plex:Token` is deliberately not `[Required]`,
  deviating from the issue's "de-facto mandatory".

## Out of scope / follow-ups

- **#69 (F-504)** is the registration half of this change and is fully resolved by step 2 of the approach; the PR
  should close both (`Closes #31`, `Closes #69`).
- **#70 (F-505)** — `PathMappings` as a bare `List<PathMapping>` with a literal section name — stays out of scope;
  validating mappings (e.g. "at least one", non-empty prefixes) belongs with that redesign. No new issue.
- **#80 (F-608)** — Worker snapshots `SyncOptions` once — unaffected; validation does not introduce
  `IOptionsMonitor`.
- `NfoOptions` gets no validation (not named in the issue; an empty `DateTimeFormat` falls back to the general
  format and an invalid enum already fails binding).