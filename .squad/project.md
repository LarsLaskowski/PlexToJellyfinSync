# Project

What the squad needs to know about this project that is not stack-specific. Read by the Lead, the Devil's
Advocate, Security, the Tester and the Reviewer. Not template-managed: `adopt-template` creates it once
and never overwrites it. A product PR updates it when the change makes an entry untrue
(`.squad/routing.md`, *Scope of a product PR*).

Every entry points: it names files, types or endpoints and links the document that says more. Behavior, values,
limits and sequences are written once, in the area documents (`docs/areas/README.md`), and the reasons in the
decision records; a copy here goes stale with the next change. `config-check.py` warns when this file grows past
`PROJECT_MAX_WORDS` words (`.squad/tools/squad_settings.py`).

## Security areas

A change that touches one of these gets a Security review: of the plan, or of the diff on tier `patch/security`;
it is tier `security` only when it creates, changes or removes a security control (`.squad/routing.md`, *Tiers*).
One line per area: what is protected, the concrete types, files or endpoints, and a link to the area document that
states its rules; no limits, patterns or sequences.

- **Tokens and secrets** — the Plex token and the dashboard token against disclosure in logs, pages and
  responses: `PlexOptions`, `DashboardOptions`, `SecretLogRedactor` —
  [Dashboard and auth](../docs/areas/dashboard-and-auth.md#live-log-buffer-and-secret-masking),
  [Configuration](../docs/areas/configuration.md)
- **Dashboard authentication** — the dashboard routes against access without a session and against token guessing:
  `TokenAuthMiddleware`, `LoginEndpoints`, `LoginPage`, `DashboardLoginService`, `LoginThrottle`, `TokenComparer`,
  `DashboardStartup` and `ForwardedHeadersWarning` (forwarded headers, `Dashboard:TrustedProxies`), the security
  headers and the endpoint mapping in `Program.cs`; `/login`, `/logout`, `/health` —
  [Dashboard and auth](../docs/areas/dashboard-and-auth.md)
- **Path mapping and file writes** — the media volume against writes outside the configured local roots:
  `PathMapper`, `MediaItemWriter`, `SeriesAggregateWriter`, `NfoWriter` —
  [Path mapping and NFO](../docs/areas/path-mapping-and-nfo.md)
- **`.nfo` / XML handling** — the parser against hostile XML (external entities, entity expansion) and the user's
  files against lost content: `NfoWriter` — [Path mapping and NFO](../docs/areas/path-mapping-and-nfo.md)
- **HTTP calls to Plex** — the request paths and the process against untrusted keys and JSON from Plex:
  `PlexClient`, `PlexJsonOptions`, the named `HttpClient`, the DTOs in `src/PlexToJellyfinSync.Data/` —
  [Sync pipeline](../docs/areas/sync-pipeline.md#what-is-read-from-plex)
- **Logging of external data** — log lines against secrets and personal data: `InMemoryLogStore`,
  `InMemoryLogProvider`, `InMemoryLogger` in `src/PlexToJellyfinSync.Service/Logging/` —
  [Dashboard and auth](../docs/areas/dashboard-and-auth.md#live-log-buffer-and-secret-masking)
- **State on disk** — the high-water-mark file against corruption and misreading: `StateStore`, `SyncStateFile`,
  `SchemaVersionProbe` — [Sync pipeline](../docs/areas/sync-pipeline.md#state-file)

## Guarantees

Deliberate behavior that must not change without the Product Manager. One line each: the guarantee in a few words,
a link to its decision record where it was a real choice, and a link to the area document under `docs/areas/` that
states it.

- Existing `.nfo` files change only in their watch fields —
  [0003](../docs/decisions/0003-nfo-only-watch-fields-touched.md),
  [Path mapping and NFO](../docs/areas/path-mapping-and-nfo.md#updating-an-existing-file)
- A path is written only through a matching mapping and inside a configured local root —
  [0004](../docs/decisions/0004-unmapped-paths-are-skipped.md),
  [Path mapping and NFO](../docs/areas/path-mapping-and-nfo.md)
- Plex is polled, never called back — [0001](../docs/decisions/0001-polling-instead-of-webhooks.md),
  [Sync pipeline](../docs/areas/sync-pipeline.md#polling-and-schedule)
- Only the Plex owner's watch state is synchronized — [0002](../docs/decisions/0002-single-user-plex-owner.md),
  [Sync pipeline](../docs/areas/sync-pipeline.md#owner-only)
- Incremental history run seeded on first start, plus a periodic full reconcile —
  [0005](../docs/decisions/0005-incremental-history-plus-full-reconcile.md),
  [0027](../docs/decisions/0027-state-file-schema-version.md),
  [Sync pipeline](../docs/areas/sync-pipeline.md#incremental-history-run)
- Failures are isolated per item and never stop the worker —
  [0006](../docs/decisions/0006-item-level-error-isolation.md),
  [0013](../docs/decisions/0013-worker-guards-both-orchestrator-calls.md),
  [Sync pipeline](../docs/areas/sync-pipeline.md#limits-and-errors)
- The dashboard is open by default and optionally protected by a token —
  [0007](../docs/decisions/0007-dashboard-auth-model.md),
  [Dashboard and auth](../docs/areas/dashboard-and-auth.md)
- Forwarded headers are trusted only from configured proxies —
  [0022](../docs/decisions/0022-forwarded-headers-from-configured-proxies-only.md),
  [Dashboard and auth](../docs/areas/dashboard-and-auth.md#forwarded-headers)
- An invalid configuration stops the start — [0016](../docs/decisions/0016-options-validated-at-startup.md),
  [Configuration](../docs/areas/configuration.md#startup-check)
- The Plex token stays optional — [0017](../docs/decisions/0017-plex-token-stays-optional.md),
  [Configuration](../docs/areas/configuration.md#startup-check)
- Container images are published to Docker Hub only —
  [0015](../docs/decisions/0015-container-images-on-docker-hub-only.md),
  [Configuration](../docs/areas/configuration.md#container)

## Integration surface

What the Reviewer checks when the diff introduces or changes a thing of this kind: every place that must
change with it.

**A new or changed configuration option** touches:
- the options class in `src/PlexToJellyfinSync.Core/Options/`, its `SectionName` and its validation
- the binding and registration in `ServiceCollectionExtensions.AddPlexToJellyfinSync`
- `src/PlexToJellyfinSync/appsettings.json`
- the settings table of `docs/areas/configuration.md`, the one home of key, environment variable, default and
  allowed values; `README.md` only where its quick start uses the key
- `ServiceCollectionExtensionsTests` (the `ServiceCollectionExtensionsBindsConfigurationSections` group)
- the area document of the behavior the option controls

**A new or changed service** touches:
- its interface in `src/PlexToJellyfinSync.Core/Abstractions/` — every production class is consumed through one
- registration and lifetime in `ServiceCollectionExtensions` (the pipeline is singleton throughout; a scoped or
  transient registration needs a reason)
- `ServiceCollectionExtensionsTests` for resolution and lifetime
- the hand-written double in `tests/PlexToJellyfinSync.Tests` if other tests consume that interface
- the component list and diagram in `docs/ARCHITECTURE.md`

**A change in the sync pipeline** (`Worker`, `SyncOrchestrator`, `LibraryReconciler`, `SeriesAggregateWriter`,
`MediaItemWriter`, `WatchAggregator`, `PathMapper`, `NfoWriter`, `StateStore`) touches:
- the *Guarantees* above and the stability policy in `docs/CONTRIBUTING.md`; a diff that changes a guarantee
  without saying so in the PR description is a finding
- `docs/areas/sync-pipeline.md` or `docs/areas/path-mapping-and-nfo.md`, and `docs/ARCHITECTURE.md` with
  decision 0018 when the split of responsibilities changes

**A change to the dashboard or its auth model** (`Program.cs`, `src/PlexToJellyfinSync/Security/`,
`src/PlexToJellyfinSync.Service/Security/`, the Razor components) touches:
- `docs/areas/dashboard-and-auth.md`, `SECURITY.md` and the "Web host & dashboard" section of
  `docs/ARCHITECTURE.md`
- the public paths of `TokenAuthMiddleware`: a new prefix or extension widens what is reachable without a session
- `LoginEndpointsTests`, `LoginThrottleTests`, `TokenAuthMiddlewareTests`, `DashboardStartupTests`

**A new Plex API call or DTO** touches:
- `src/PlexToJellyfinSync.Data/Plex/` and the mapping in `PlexClient`; the Plex JSON shape does not leave that class
- `PlexJsonOptions` if deserialization changes
- `FakePlexClient` / `StubHttpMessageHandler`
- the endpoint list in `docs/ARCHITECTURE.md` and `docs/areas/sync-pipeline.md` (*What is read from Plex*)

**A new logged value** touches `SecretLogRedactor` and the masking rules in `docs/areas/dashboard-and-auth.md`:
a token, a credential or a full Plex URL with query string in a log statement is a finding.

## Test doubles

No mocking library; hand-written fakes and stubs in `tests/PlexToJellyfinSync.Tests`, each implementing the
interface or base type it stands in for directly (pattern and details in `docs/UNIT_TESTS.md`, *Test doubles*), one
line each: the double, where it lives and what it stands in for; how it behaves is read in its code:

- `FakePlexClient` (`FakePlexClient.cs`) — `IPlexClient`
- `FakeStateStore` (`FakeStateStore.cs`) — `IStateStore`
- `StubPathMapper` (`StubPathMapper.cs`) — `IPathMapper`
- `RecordingNfoWriter` (`RecordingNfoWriter.cs`, records `NfoWriteRecord`) — `INfoWriter`
- `FakeSyncOrchestrator` (`FakeSyncOrchestrator.cs`) — `ISyncOrchestrator` for the `Worker`
- `FakeSyncStatusProvider` (`FakeSyncStatusProvider.cs`) — `ISyncStatusProvider` for the dashboard
- `StubDashboardLoginService` (`StubDashboardLoginService.cs`) — `IDashboardLoginService` for the login endpoints
- `StubHttpMessageHandler` (`StubHttpMessageHandler.cs`) — Plex's HTTP answers behind the real `HttpClient`
- `FakeHttpClientFactory` (`FakeHttpClientFactory.cs`) — `IHttpClientFactory`
- `StubAntiforgery` (`StubAntiforgery.cs`) — ASP.NET Core's `IAntiforgery`
- `TestTimeProvider` (`TestTimeProvider.cs`) — the clock (`TimeProvider`)
- `RecordingWorkerLogger` (`RecordingWorkerLogger.cs`) — `ILogger<Worker>`
- `RecordingSyncStatusLogger` (`RecordingSyncStatusLogger.cs`) — `ILogger<SyncStatusService>`
- `RecordingStateStoreLogger` (`RecordingStateStoreLogger.cs`) — `ILogger<StateStore>`
- `RecordingPlexClientLogger` (`RecordingPlexClientLogger.cs`) — `ILogger<PlexClient>`
- `RecordingForwardedHeadersWarningLogger` (`RecordingForwardedHeadersWarningLogger.cs`) —
  `ILogger<ForwardedHeadersWarning>`
