# Project

What the squad needs to know about this project that is not stack-specific. Read by the Lead, the Devil's
Advocate, Security, the Tester and the Reviewer. Not template-managed: `adopt-template` creates it once
and never overwrites it. A product PR updates it when the change makes an entry untrue
(`.squad/routing.md`, *Scope of a product PR*).

## Security areas

A change that touches one of these gets a Security review: of the plan, or of the diff on tier `patch/security`;
it is tier `security` only when it creates, changes or removes a security control (`.squad/routing.md`, *Tiers*).

- **Tokens and secrets:** the Plex token and the dashboard token — read from configuration, never logged,
  never exposed in the UI, API responses or exceptions; `SecretLogRedactor` masks them before a log entry
  reaches the dashboard's in-memory log buffer.
- **Dashboard authentication:** `TokenAuthMiddleware`, `LoginEndpoints`, `DashboardLoginService`,
  `LoginThrottle`, `TokenComparer`, session cookies, security headers and CSP, `Program.cs` endpoint mapping,
  the forwarded-header trust (`DashboardStartup`, `Dashboard:TrustedProxies`).
- **Path mapping and file writes:** `PathMapper`, `MediaItemWriter`, `SeriesAggregateWriter`, `NfoWriter`
  and every file write (path traversal, writing outside a configured `PathMappings:N:Local` root, symlinks).
- **`.nfo` / XML handling:** `NfoWriter` parsing and saving (XXE, entity expansion, preserving foreign
  content and encoding).
- **HTTP calls to Plex:** `PlexClient`, `PlexJsonOptions`, the named `HttpClient` (TLS, timeouts, untrusted
  JSON).
- **Logging of external data:** the in-memory log store and provider (secrets or PII in log lines).
- **State on disk:** `StateStore` (the high-water-mark file).

## Guarantees

Deliberate behavior that must not change without the Product Manager. Each one is described in its area
document under `docs/areas/` and rests on the decision record linked with it.

- Existing `.nfo` files are only ever touched in their `watched` / `playcount` / `lastplayed` elements, and
  an unchanged value skips the write entirely (`NfoWriteOutcome.Skipped`); an update saves without
  re-indenting, a new file is UTF-8 without BOM, and an update keeps the encoding its byte-order mark
  identifies — [Path mapping and NFO](../docs/areas/path-mapping-and-nfo.md),
  [decision 0003](../docs/decisions/0003-nfo-only-watch-fields-touched.md)
- Path mapping is mandatory: `PathMapper` rejects `/../` sequences and requires a matching mapping; an
  unmapped path is skipped (in `MediaItemWriter`), never passed through unchanged; `NfoWriter`
  independently verifies that the target stays under a configured local root —
  [Path mapping and NFO](../docs/areas/path-mapping-and-nfo.md),
  [decision 0004](../docs/decisions/0004-unmapped-paths-are-skipped.md)
- Polling only, single user (the Plex owner) — [Sync pipeline](../docs/areas/sync-pipeline.md),
  [0001](../docs/decisions/0001-polling-instead-of-webhooks.md),
  [0002](../docs/decisions/0002-single-user-plex-owner.md)
- `ProcessHistoryAsync` seeds the high-water mark on first run instead of replaying the whole history; a
  periodic full reconcile complements it — [Sync pipeline](../docs/areas/sync-pipeline.md),
  [decision 0005](../docs/decisions/0005-incremental-history-plus-full-reconcile.md)
- Failures are isolated per item (and per show); the `Worker` loop survives them, cannot overlap runs, and
  re-throws `OperationCanceledException` — [Sync pipeline](../docs/areas/sync-pipeline.md),
  [0006](../docs/decisions/0006-item-level-error-isolation.md),
  [0013](../docs/decisions/0013-worker-guards-both-orchestrator-calls.md)
- The dashboard is open by default and optionally protected by a token: `Dashboard:Token` unset stays a
  pass-through, `Dashboard:Enabled` false maps `/health` and nothing else —
  [Dashboard and auth](../docs/areas/dashboard-and-auth.md),
  [decision 0007](../docs/decisions/0007-dashboard-auth-model.md)
- Options are validated at startup; the Plex token stays optional —
  [Configuration](../docs/areas/configuration.md),
  [0016](../docs/decisions/0016-options-validated-at-startup.md),
  [0017](../docs/decisions/0017-plex-token-stays-optional.md)
- Container images are published to Docker Hub only —
  [Configuration](../docs/areas/configuration.md),
  [decision 0015](../docs/decisions/0015-container-images-on-docker-hub-only.md)

## Integration surface

What the Reviewer checks when the diff introduces or changes a thing of this kind: every place that must
change with it.

**A new or changed configuration option** touches:
- the options class in `src/PlexToJellyfinSync.Core/Options/`, its `SectionName` and its validation
- the binding and registration in `ServiceCollectionExtensions.AddPlexToJellyfinSync`
- `src/PlexToJellyfinSync/appsettings.json`
- the configuration table in `README.md` and the settings table of `docs/areas/configuration.md` — key,
  `PLEXSYNC__`-prefixed environment variable, default value and, in the area document, the allowed range and
  validation all have to match the code; both are updated with every option change
- `ServiceCollectionExtensionsTests` (the `ServiceCollectionExtensionsBindsConfigurationSections` group)
- the area document of the behavior the option controls (`docs/areas/sync-pipeline.md`,
  `docs/areas/path-mapping-and-nfo.md` or `docs/areas/dashboard-and-auth.md`) when the option changes
  documented behavior

**A new or changed service** touches:
- its interface in `src/PlexToJellyfinSync.Core/Abstractions/` — every production class in this codebase
  is consumed through one
- registration *and lifetime* in `ServiceCollectionExtensions` (the pipeline is singleton throughout; a
  new scoped or transient registration needs a reason)
- `ServiceCollectionExtensionsTests` for resolution and lifetime
- the hand-written fake/stub in `tests/PlexToJellyfinSync.Tests` if other tests consume that interface
- the component list and diagram in `docs/ARCHITECTURE.md`

**A change in the sync pipeline** (`Worker`, `SyncOrchestrator`, `LibraryReconciler`,
`SeriesAggregateWriter`, `MediaItemWriter`, `WatchAggregator`, `PathMapper`, `NfoWriter`, `StateStore`)
touches the *Guarantees* above, the stability policy in `docs/CONTRIBUTING.md` and decision 0018 (which
collaborator owns what). A diff that changes a guarantee without saying so in the PR description is a
finding, and so is a diff that leaves the corresponding sentence in `README.md`,
`docs/areas/sync-pipeline.md`, `docs/areas/path-mapping-and-nfo.md`, `docs/ARCHITECTURE.md` or
`docs/CONTRIBUTING.md` standing while making it untrue.

**A change to the dashboard or its auth model** (`Program.cs`, `TokenAuthMiddleware`, `LoginEndpoints`,
`DashboardLoginService`, `LoginThrottle`, `TokenComparer`, the Razor components) touches:
- `SECURITY.md`, `docs/areas/dashboard-and-auth.md`, the "Web host & dashboard" section of
  `docs/ARCHITECTURE.md`, and `README.md`
- the unauthenticated-by-default behavior (*Guarantees*)
- the middleware allowlist — a new public prefix or static-asset extension widens what is reachable
  without a session cookie
- the security headers and CSP, cookie flags (`HttpOnly`, `SameSite=Strict`, `Secure` only on HTTPS),
  constant-time token comparison, and the throttle's backoff, pruning and bound, and the trusted-proxy list
- `LoginEndpointsTests`, `LoginThrottleTests` and the middleware's tests

**A new Plex API call or DTO** touches:
- `src/PlexToJellyfinSync.Data/Plex/` and the mapping in `PlexClient` — the Plex JSON shape must not leak
  past that class into `Core` or the host
- `PlexJsonOptions` if deserialization behavior changes
- `FakePlexClient` / `StubHttpMessageHandler` in the test project
- the endpoint list in `docs/ARCHITECTURE.md`

**A new logged value** touches `SecretLogRedactor` and the dashboard log buffer: the log store feeds a
dashboard that is reachable without authentication whenever `Dashboard:Token` is empty, so a log
statement that writes a token, a credential or a full Plex URL with query string is a finding.

## Test doubles

No mocking library; hand-written fakes and stubs in `tests/PlexToJellyfinSync.Tests`, each implementing a
`Core.Abstractions` interface directly (pattern and details in `docs/UNIT_TESTS.md`, *Test doubles*):

- `FakePlexClient` — preconfigured libraries, history, items and episodes; records calls
- `FakeStateStore` — in-memory high-water mark
- `StubPathMapper` — configurable Plex-path → local-path function
- `RecordingNfoWriter` (with `NfoWriteRecord`) — records writes instead of touching the file system
- `StubHttpMessageHandler` — canned responses for the real `HttpClient` in `PlexClientTests`
- `FakeHttpClientFactory` — hands out a preconfigured `HttpClient` and counts requests
- `StubDashboardLoginService` — preconfigured `LoginResult` for `LoginEndpointsTests`
- `TestTimeProvider` — controllable clock for `LoginThrottleTests`
- `FakeSyncOrchestrator` — configurable failures; signals when a history sync or reconcile was requested
- `FakeSyncStatusProvider` — settable status snapshot that raises `Changed` on demand
- `RecordingWorkerLogger`, `RecordingSyncStatusLogger` — record log entries of `Worker` / `SyncStatusService`
- `StubAntiforgery` — antiforgery validation that always passes or always fails
