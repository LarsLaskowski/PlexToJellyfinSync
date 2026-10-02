# 0016: Options are validated at startup with DataAnnotations

- **Status:** Accepted
- **Date:** 2026-10-02
- **Source:** Issue #31 (with #69)
- **Supersedes:** —

## Context

All options types were bound with a plain `services.Configure<T>(section)`; nothing marked a setting as required or
limited its range. A missing or malformed `Plex:BaseUrl` (the shipped `appsettings.json` has `""`) let the host start
normally and then fail on every poll — visible only as a recurring sync error on the dashboard. Numeric settings were
not validated but clamped where they are used (`Worker`: poll interval at least 5 s, reconcile interval at least 1 h;
`SyncOrchestrator`: parallelism at least 1; `InMemoryLogStore`: buffer at least 1), so a configured value could be
silently replaced by another one. There was no upper bound: a `Sync:PollIntervalSeconds` above about 4.29 million
makes `Task.Delay` throw outside the Worker's guards and stops the host. An empty `State:Directory` put
`state.json` relative to the working directory (`/app` in the image, writable but not a volume) instead of the
`/config` volume, so the sync state was lost on every container restart. The repository review (F-101 / F-504)
asked for options that are validated at startup.

`docs/ARCHITECTURE.md` promises that a flaky Plex *server* never blocks startup; it makes no promise about a missing
configuration.

## Options considered

1. **Keep starting and report misconfiguration at runtime** (status quo, perhaps with a dashboard hint) — the
   dashboard and `/health` stay reachable, but the error surfaces late and repeatedly, and out-of-range values stay
   silently clamped. Rejected: the issue asks for fail-fast, and a configuration without a Plex URL cannot do any
   useful work.
2. **DataAnnotations on the options classes, registered with `ValidateDataAnnotations().ValidateOnStart()`**
   (needs the first-party `Microsoft.Extensions.Options.DataAnnotations` package) — declarative, the constraints live
   on the options types themselves, standard ASP.NET Core pattern, minimal code. Costs one package and reflection-based
   validation (irrelevant here: no trimming/AOT).
3. **`[OptionsValidator]` source-generated validators** (no extra package) — compile-time, reflection-free, but adds
   a partial validator type per options class and generated code in the analyzer scope, for a benefit (AOT/trimming)
   this application does not use.
4. **Hand-written `IValidateOptions<T>`** — no package, full control, but duplicates what attributes express and
   moves the rules away from the options types.

For `Plex:BaseUrl`, `[Url]` was considered and not used: `UrlAttribute` only checks for an `http://`, `https://` or
`ftp://` prefix, so it accepts `http://` and `ftp://…`, which still fail later. `PlexOptions` implements
`IValidatableObject` instead and requires an absolute `http`/`https` URI with a host.

For the numeric ranges, rejecting only non-positive values while keeping the higher runtime floors (5 s, 1 h) was
considered and rejected as inconsistent: a value between 1 and 4 seconds would still be silently changed.

## Decision

Option 2. `PlexOptions`, `SyncOptions`, `StateOptions` and `DashboardOptions` are registered in
`ServiceCollectionExtensions.AddPlexToJellyfinSync` with
`AddOptions<T>().Bind(section).ValidateDataAnnotations().ValidateOnStart()`, so an invalid configuration throws
`OptionsValidationException` and stops the application at startup before `Worker` or Kestrel start (which call
raises it first depends on which service reads the options first and is not part of this decision). The rules:

- `Plex:BaseUrl` required, absolute `http`/`https` URI; `Plex:OwnerAccountId` null or at least 1.
- `Sync:PollIntervalSeconds` 5–86400; `Sync:FullReconcileIntervalHours` 1–8760; both parallelism settings at least 1.
- `Dashboard:LogBufferSize` at least 1; `State:Directory` required.
- Lower bounds equal the existing runtime floors; upper bounds exist only where a larger value breaks the runtime
  (`Task.Delay`, `TimeSpan`) and are set generously (one day, one year).
- The runtime clamps in `Worker`, `SyncOrchestrator` and `InMemoryLogStore` stay as defense-in-depth for options
  built without DI validation.
- Validation messages name the setting and its environment variable, never the configured value (the token or a URL
  with credentials must not reach the container log).
- `NfoOptions` and `PathMappings` are not validated (not in scope; `PathMappings` is redesigned in #70).

## Consequences

- A container started without `PLEXSYNC__Plex__BaseUrl`, or with an out-of-range value, now exits at startup (and
  crash-loops under a restart policy) with the offending key in `docker logs`, instead of running with a failing
  sync. While it does, neither the dashboard nor `/health` is reachable.
- Configurations that used to work through the clamps (for example `Sync:PollIntervalSeconds: 2` or a parallelism of
  `0`) now fail startup; the README states the ranges.
- Running the app locally needs `Plex:BaseUrl` set; `CONTRIBUTING.md` says so.
- Validation is purely syntactic and does no network I/O, so an unreachable Plex server still never blocks startup.
- Adds the `Microsoft.Extensions.Options.DataAnnotations` package (same version train as the other
  `Microsoft.Extensions.*` packages). Revisiting would mean a source-generated or hand-written validator.