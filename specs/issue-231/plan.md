# Plan: Trust forwarded headers only from configured proxies and bound the login throttle

Source: Issue #231
Status: Revised (1)
Tier: security — the change narrows which senders may set the client address and scheme (a guard against spoofing that feeds the login throttle and the cookie's `Secure` flag) and changes the throttle's bound, both security controls of the dashboard login.
Security areas touched: Dashboard authentication (`LoginThrottle`, session cookie `Secure` flag, `Program.cs` middleware pipeline, the forwarded-header trust in `DashboardStartup`); a new configuration option (`Dashboard:TrustedProxies`); `.squad/project.md` (*Security areas* widened)

## Problem / root cause

- `src/PlexToJellyfinSync/Security/DashboardStartup.cs:38-39` clears `KnownIPNetworks` and `KnownProxies`.
  `ForwardedHeadersMiddleware.ApplyForwarders` (ASP.NET Core 10.0.13,
  `src/Middleware/HttpOverrides/src/ForwardedHeadersMiddleware.cs`) sets
  `checkKnownIps = KnownIPNetworks.Count > 0 || KnownProxies.Count > 0` and checks the sender only when it is
  true, so with both lists empty `X-Forwarded-For` and `X-Forwarded-Proto` are applied from every sender.
- `src/PlexToJellyfinSync/Security/LoginEndpoints.cs:61` keys the throttle on `context.Connection.RemoteIpAddress`,
  which `Program.cs:50` (`UseForwardedHeaders`) has already replaced with the forwarded address; line 89 sets the
  cookie's `Secure` from `Request.IsHttps`, which the forwarded scheme decides.
- `src/PlexToJellyfinSync.Service/Security/LoginThrottle.cs:58-72` (`PruneStaleEntries`) runs only above 1024 keys
  and removes only records idle more than 15 minutes and not locked: no upper bound.
- Related defect found on the way: `RegisterFailure` takes a record with `GetOrAdd` (line 115) and locks it
  afterwards; a concurrent `PruneStaleEntries` can remove that record in between, so the failure lands on an
  orphaned object and is lost. The single-lock design below removes the race.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | The options honour `X-Forwarded-For`/`-Proto` from any sender because both known lists are cleared | `DashboardStartup.cs:33-39`; middleware `checkKnownIps` (source above, line 221) | confirmed |
| 2 | The throttle keys on `Connection.RemoteIpAddress` after the middleware | `LoginEndpoints.cs:61`, `Program.cs:50` runs before the endpoints | confirmed |
| 3 | `ForwardLimit` is 1 and the rightmost `X-Forwarded-For` entry is used | `ForwardedHeadersOptions.cs:83` (`= 1`); middleware fills sets right to left (`forwardedFor[Length - i - 1]`) | confirmed |
| 4 | `X-Forwarded-Proto` decides `IsHttps` and with it `Secure` | middleware applies `currentValues.Scheme` to `request.Scheme`; `LoginEndpoints.cs:89` | confirmed |
| 5 | The throttle map is pruned only above 1024 keys, only idle (> 15 min) and unlocked records | `LoginThrottle.cs:58-72`, constants at 22, 30 | confirmed |
| 6 | The issue's "behind a proxy that sets or appends the header the effect does not apply" | true only when the client cannot reach the port directly; any direct sender still spoofs (fact 1) | confirmed |
| 7 | Sessions are 256-bit random, token comparison constant-time | `DashboardLoginService.cs` (`SessionIdByteLength = 32`, `TokenComparer.FixedTimeEquals`) | confirmed |
| 8 | Framework defaults are `KnownProxies = { ::1 }` and `KnownIPNetworks = { 127.0.0.0/8 }` | `ForwardedHeadersOptions.cs:88`, `DualIPNetworkList.cs:27` (10.0.13) | confirmed |
| 9 | The sender check maps an IPv4-mapped IPv6 address to IPv4 first, then checks the original | `ForwardedHeadersMiddleware.CheckKnownAddress` (line 388) | confirmed |
| 10 | A request whose `RemoteIpAddress` is null has its first forwarded entry applied even with known lists | middleware comment and condition `currentValues.RemoteIpAndPort != null && checkKnownIps && …` (line 224); Kestrel over TCP always sets the address | confirmed |
| 11 | `System.Net.IPNetwork.TryParse` splits at the last `/`, parses the address with `IPAddress.TryParse`, the prefix with `NumberStyles.None` (no sign), rejects a prefix above 32/128, and clears host bits instead of failing (`172.18.0.1/16` gives `172.18.0.0/16`) | dotnet/runtime v10.0.0 `System.Net.Primitives/src/System/Net/IPNetwork.cs` lines 48-54, 179-198 (the XML docs saying it throws are stale) | confirmed |
| 12 | `IPNetwork.Contains` with an IPv4 network accepts an IPv4-mapped IPv6 address; `::/0` contains every IPv6 address | same file lines 66-110 | confirmed |
| 13 | `ForwardedHeadersMiddleware` has a public constructor `(RequestDelegate, ILoggerFactory, IOptions<ForwardedHeadersOptions>)` and a public `ApplyForwarders(HttpContext)`, so tests run the real middleware | middleware lines 46, 120 | confirmed |
| 14 | The test project references `Microsoft.AspNetCore.App` | `PlexToJellyfinSync.Tests.csproj` `FrameworkReference` | confirmed |
| 15 | Decision 0007 is released (append-only) | `git ls-remote --tags origin` lists `v1.5.0`; 0021 states 0001-0014 are in v1.5.0 | confirmed |
| 16 | The existing test `LoginThrottlePrunesStaleEntries` stays green under the new prune rule | traced: 1024 keys admitted, 76 on the shared record; after 20 min the new key prunes all 1024 and is admitted; `10.1.0.0` is then untracked with room in the map, so not locked | confirmed |
| 17 | No behavior depends on how `IPAddress.TryParse` treats surrounding white space | the parser trims every entry before parsing (*Approach* 2) | confirmed |
| 18 | `IPAddress.TryParse` and `IPNetwork.TryParse` accept non-canonical IPv4 text: `192.168.001.010` gives `192.168.1.8`, `010.0.0.1` gives `8.0.0.1`, `167772160/8` and `0x0a000000/8` give `10.0.0.0/8`, `10/8` gives `0.0.0.0/8`; `fe80::1%eth0/64` and `[::1]/128` are accepted too | Security's plan review, measured | confirmed |
| 19 | When the middleware applies a forwarded address it sets `X-Original-For`, when it applies a scheme `X-Original-Proto`; it never sets them otherwise | `ForwardedHeadersMiddleware.cs` lines 312-341 (10.0.13), `ForwardedHeadersDefaults.cs` | confirmed |
| 20 | A list value set without an index (`PLEXSYNC__Dashboard__TrustedProxies=10.0.0.0/8`) binds to an empty array without an error | scratch binding run by the Dev (`AddEnvironmentVariables("PLEXSYNC__")` + `Get<T>()` on a `string[]`): `PLEXSYNC__Dashboard__TrustedProxies=10.0.0.0/8` gives 0 entries, `PLEXSYNC__Dashboard__TrustedProxies__0=10.0.0.0/8` gives 1; the documentation states the indexed form | confirmed |

## Acceptance criteria

The deciding code path for AC1-AC3 is `ForwardedHeadersMiddleware.ApplyForwarders` with the options returned by
`DashboardStartup.CreateForwardedHeadersOptions(DashboardOptions)`; tests set `Connection.RemoteIpAddress` to a
non-null address (fact 10).

- [ ] AC1: With `TrustedProxies` empty, a request from `203.0.113.7` carrying `X-Forwarded-For: 198.51.100.1` and
  `X-Forwarded-Proto: https` keeps `RemoteIpAddress` `203.0.113.7` and `IsHttps` false.
- [ ] AC2: With `TrustedProxies` empty, a request from `127.0.0.1` or `::1` has both headers applied
  (`RemoteIpAddress` `198.51.100.1`, `IsHttps` true): the framework's loopback defaults stay.
- [ ] AC3: With `TrustedProxies = ["172.18.0.0/16"]`, a request from `172.18.0.5` and from `::ffff:172.18.0.5`
  carrying `X-Forwarded-For: 203.0.113.9, 198.51.100.1` and `X-Forwarded-Proto: https` gets `RemoteIpAddress`
  `198.51.100.1` and `IsHttps` true; with `TrustedProxies = ["192.0.2.10"]` a request from `192.0.2.10` gets the
  same; a request from `172.19.0.5` with the network entry stays unchanged.
- [ ] AC4: `TrustedProxyParser.TryParse` accepts, after trimming white space, an IPv4 address in canonical
  dotted-decimal form (four decimal octets 0-255 without leading zeros) or an IPv6 address without `.`, `%`, `[`
  or `]`, each alone (a single-host network, prefix 32 or 128) or with `/` and a prefix length in decimal digits
  without sign or leading zero, at most 32 or 128 (host bits cleared). It rejects everything else: null, empty and
  whitespace-only text, a host name, short, integer, hex and octal IPv4 forms, a zone id, brackets, an IPv6 form
  with embedded IPv4, a second `/`, an empty, signed, zero-padded or too long prefix.
  `DashboardOptions` validation reports each rejected entry at index `i` for member `TrustedProxies` with exactly
  `Dashboard:TrustedProxies:{i} must be an IP address or a CIDR network such as 172.18.0.0/16 (environment variable PLEXSYNC__Dashboard__TrustedProxies__{i}).`,
  a text that never contains the entry; an empty list and valid entries give no result.
- [ ] AC5: `AddPlexToJellyfinSync` binds `Dashboard:TrustedProxies:N` into `DashboardOptions.TrustedProxies`, and
  an invalid entry fails options validation (`ValidateOnStart`).
- [ ] AC6: `LoginThrottle` never holds more than 1024 per-client records (`TrackedClientCount`). A failure of an
  untracked client while 1024 are held first removes the records idle more than 15 minutes and not locked; the
  client gets its own record when that frees room.
- [ ] AC7: While 1024 records are held and none can be removed, every untracked client is evaluated and counted
  on one shared record: after failures of six distinct new clients, a seventh new client is locked out with
  `retryAfter` 1 second, while a tracked client is still judged by its own record.
- [ ] AC8: `RegisterSuccess` of a client counted on the shared record does not clear the shared record.
- [ ] AC9: An entry the parser rejects never widens trust: `CreateForwardedHeadersOptions` with
  `TrustedProxies = ["10.0.0.0/33"]` leaves a request from `203.0.113.7` with both headers unchanged
  (`IsHttps` false), and `KnownIPNetworks` holds only the framework default.
- [ ] AC10: `ForwardedHeadersWarning.Inspect`, called after the middleware, logs one warning per instance, the
  first time a request carries `X-Forwarded-For` without `X-Original-For` or `X-Forwarded-Proto` without
  `X-Original-Proto` (decided by those four header names, fact 19), at level Warning with exactly
  `Forwarded headers from {Sender} were not applied: the sender is not a trusted proxy or the header is malformed. List the reverse proxy in Dashboard:TrustedProxies (environment variable PLEXSYNC__Dashboard__TrustedProxies__0). This warning is logged once.`
  where `{Sender}` is `Connection.RemoteIpAddress` (never a header value); a request whose headers were applied,
  or one without forwarded headers, logs nothing; a second untrusted request logs nothing.

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | `DashboardStartupForwardedHeadersFromUntrustedSenderAreIgnored` | `tests/PlexToJellyfinSync.Tests/DashboardStartupTests.cs` |
| AC2 | `DashboardStartupForwardedHeadersFromLoopbackAreApplied` (DataRow `127.0.0.1`, `::1`) | `tests/PlexToJellyfinSync.Tests/DashboardStartupTests.cs` |
| AC3 | `DashboardStartupForwardedHeadersFromTrustedNetworkAreApplied` (DataRow `172.18.0.5`, `::ffff:172.18.0.5`), `DashboardStartupForwardedHeadersFromTrustedAddressAreApplied`, `DashboardStartupForwardedHeadersFromOutsideTrustedNetworkAreIgnored`, `DashboardStartupForwardedHeadersFromTrustedIPv6NetworkAreApplied` (entry `2001:db8::/32`: sender `2001:db8::5` applied, `2001:db9::5` unchanged) | `tests/PlexToJellyfinSync.Tests/DashboardStartupTests.cs` |
| AC4 | `TrustedProxyParserAddressReturnsSingleHostNetwork` (DataRow `192.0.2.10`/32, `2001:db8::1`/128, `" 192.0.2.10 "`/32), `TrustedProxyParserNetworkReturnsNetwork` (DataRow `172.18.0.0/16`, `172.18.0.1/16` → base `172.18.0.0`, `2001:db8::/32`), `TrustedProxyParserInvalidEntryReturnsFalse` (DataRow null, `""`, `"   "`, `proxy.local`, `10.0.0.0/33`, `2001:db8::/129`, `10.0.0.0/`, `10.0.0.0/-1`, `10.0.0.0/+8`, `10.0.0.0/08`, `10.0.0.0/8/8`, `192.168.001.010`, `010.0.0.0/8`, `167772160/8`, `0x0a000000/8`, `10.1`, `10/8`, `10.0.0.1:80`, `fe80::1%eth0/64`, `[::1]/128`, `::ffff:10.0.0.0/104`) | `tests/PlexToJellyfinSync.Tests/TrustedProxyParserTests.cs` |
| AC4 | `DashboardOptionsValidTrustedProxiesAreValid`, `DashboardOptionsInvalidTrustedProxyReportsIndexWithoutValue` (entries `["10.0.0.0/8", "proxy.local"]`: one result, exact text for index 1, member `TrustedProxies`, text does not contain `proxy.local`) | `tests/PlexToJellyfinSync.Tests/DashboardOptionsTests.cs` |
| AC5 | `ServiceCollectionExtensionsBindsConfigurationSections` (extended with `Dashboard:TrustedProxies:0`), `ServiceCollectionExtensionsInvalidTrustedProxyFailsValidation` | `tests/PlexToJellyfinSync.Tests/ServiceCollectionExtensionsTests.cs` |
| AC6 | `LoginThrottleTrackedClientsStayWithinBound` (2000 distinct keys at one instant → 1024), `LoginThrottleFullMapPrunesIdleRecordsBeforeSharing` (1024 keys with one failure each, advance 20 min, one failure of a new key: `TrackedClientCount` is 1) | `tests/PlexToJellyfinSync.Tests/LoginThrottleTests.cs` |
| AC7 | `LoginThrottleFullMapCountsNewClientsOnSharedRecord` | `tests/PlexToJellyfinSync.Tests/LoginThrottleTests.cs` |
| AC8 | `LoginThrottleSuccessDoesNotClearSharedRecord` | `tests/PlexToJellyfinSync.Tests/LoginThrottleTests.cs` |
| AC9 | `DashboardStartupInvalidTrustedProxyIsNotTrusted` | `tests/PlexToJellyfinSync.Tests/DashboardStartupTests.cs` |
| AC10 | `ForwardedHeadersWarningUntrustedForwardedForLogsOnce` (two untrusted requests, one entry, exact text with the sender), `ForwardedHeadersWarningUntrustedForwardedProtoLogs`, `ForwardedHeadersWarningAppliedHeadersLogNothing` (trusted sender through the real middleware), `ForwardedHeadersWarningWithoutForwardedHeadersLogsNothing` | `tests/PlexToJellyfinSync.Tests/ForwardedHeadersWarningTests.cs` |

The AC1-AC3, AC9 and AC10 tests build `new ForwardedHeadersMiddleware(_ => Task.CompletedTask, NullLoggerFactory.Instance,
Options.Create(DashboardStartup.CreateForwardedHeadersOptions(options)))` and call `ApplyForwarders` on a
`DefaultHttpContext` (scheme `http`).

## Approach

1. **Setting.** `DashboardOptions.TrustedProxies` (`string[]`, default empty), bound from
   `Dashboard:TrustedProxies:N`. `DashboardOptions` implements `IValidatableObject` like `PlexOptions`: one
   `ValidationResult` per entry `TrustedProxyParser.TryParse` rejects, with the AC4 text and member
   `TrustedProxies`. (`Validator` runs `Validate` only after the `Range` attribute passed; that order stays.)
2. **Parser** (`Core/Options/TrustedProxyParser.cs`), without `IPNetwork.TryParse` (whose failure leaves
   `0.0.0.0/0` in the out value): null, empty or whitespace → false; trim; reject any `%`, `[` or `]`; split at
   `/`, more than one `/` → false; the address part through `IPAddress.TryParse`, then for IPv4 require
   `address.ToString()` to equal the address part (this rejects every short, integer, hex, octal and zero-padded
   form, fact 18, and a `:port` suffix), for IPv6 require the part to contain no `.`; the prefix part, when present,
   only ASCII digits, no leading zero except `0` itself, at most 32 or 128, absent → 32 or 128; the result is
   `new System.Net.IPNetwork(address, prefix)`, which clears host bits (fact 11). `IPAddress.TryParse`'s lenient
   forms would otherwise trust a network the operator never wrote, which is not fail-closed.
3. **Forwarded headers.** `CreateForwardedHeadersOptions(DashboardOptions options)` keeps `XForwardedFor |
   XForwardedProto`, `ForwardLimit` 1 and the framework defaults (no `Clear`), and adds every parsed entry to
   `KnownIPNetworks` (never the obsolete `KnownNetworks`, ASPDEPR005). With `using Microsoft.AspNetCore.HttpOverrides`
   in the file, write `System.Net.IPNetwork` qualified or use `var`: the obsolete `HttpOverrides.IPNetwork` makes the
   bare name ambiguous. Entries are already validated; an entry the parser rejects anyway is skipped (never added
   as "trust all"). `Program.cs:50` passes `dashboardOptions` (read at line 43). The sender check, loopback and
   IPv4-mapped handling are the framework's (facts 8, 9, 12).
4. **Throttle.** Replace the `ConcurrentDictionary` and the per-record `lock` with a `Dictionary<string,
   LoginAttemptState>` (ordinal) plus one `_sharedRecord` field, all guarded by one `System.Threading.Lock`. A
   private `ResolveRecord(string clientKey, DateTimeOffset now, bool create)`:
   tracked key → its record; else, when 1024 are held, remove the records idle more than 15 minutes and not
   locked; then with room → a new record when `create` (null otherwise, i.e. not locked); without room → the
   shared record. `IsLockedOut` uses `create: false`, `RegisterFailure` `create: true` and applies the unchanged
   counting rules to the resolved record. `RegisterSuccess` removes only a tracked key's own record. The shared
   record follows the same counting, backoff and 15-minute reset as any record. `TrackedClientCount` returns the
   dictionary's count under the lock. `PruneStaleEntries` is replaced by the prune step inside `ResolveRecord`.
5. **Upgrade warning** (`src/PlexToJellyfinSync/Security/ForwardedHeadersWarning.cs`, new): constructor takes
   `ILogger<ForwardedHeadersWarning>`; `Inspect(HttpContext)` applies the AC10 condition and claims the single
   warning with `Interlocked.Exchange` on an `int` field, so concurrent requests log at most once. `Program.cs`
   creates one instance after `builder.Build()` (`new ForwardedHeadersWarning(app.Services.GetRequiredService<ILogger<ForwardedHeadersWarning>>())`,
   no DI registration and no interface: host-only, like `LoginEndpoints`) and calls it in an `app.Use` directly
   after `UseForwardedHeaders`. Only the connection's address is logged, never a header value, so an attacker
   controls neither the text nor the volume (one line per process); a client can suppress the warning by sending
   `X-Original-For` itself, which costs only the diagnostic. The guard for CA1873 (`IsEnabled` or `LoggerMessage`)
   is the Dev's. The logged argument gets a neutral name (`sender`): CodeQL treats any name containing `trusted`
   as sensitive (`.squad/stack.md`, *Logger arguments*).
6. No other new user-visible text besides the validation message of AC4 and the warning of AC10.

## Options considered

The options and why they lost are in [decision 0022](../../docs/decisions/0022-forwarded-headers-from-configured-proxies-only.md):
trusted proxies plus loopback (chosen) against trusting private networks by default, keying on the socket
address, a global throttle record, two framework-shaped settings; for the bound, a shared overflow record
(chosen) against evicting the oldest record and per-/64 keys.

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| Core | `Options/DashboardOptions.cs` | `TrustedProxies`, `IValidatableObject` |
| Core | `Options/TrustedProxyParser.cs` (new) | entry parser |
| Host | `Security/DashboardStartup.cs` | options built from the setting |
| Host | `Program.cs` | passes `dashboardOptions` (line 50); creates and calls `ForwardedHeadersWarning` |
| Host | `Security/ForwardedHeadersWarning.cs` (new) | once-per-process upgrade warning |
| Host | `appsettings.json` | `"TrustedProxies": []` in `Dashboard` |
| Service | `Security/LoginThrottle.cs` | bounded records, shared record, one lock, `TrackedClientCount` |

## Signatures (for the Dev's skeleton)

- `public string[] TrustedProxies { get; set; } = [];` on `DashboardOptions`
- `public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)` on `DashboardOptions` (class
  now `public sealed class DashboardOptions : IValidatableObject`, region `#region IValidatableObject`)
- `public static class TrustedProxyParser` with `public static bool TryParse(string? entry, out System.Net.IPNetwork network)`
- `public static ForwardedHeadersOptions CreateForwardedHeadersOptions(DashboardOptions options)` replaces the
  parameterless overload in `DashboardStartup`
- `public int TrackedClientCount { get; }` on `LoginThrottle` (not on `ILoginThrottle`)
- `public sealed class ForwardedHeadersWarning` with `public ForwardedHeadersWarning(ILogger<ForwardedHeadersWarning> logger)`
  and `public void Inspect(HttpContext context)`

Skeleton must rewrite: `DashboardStartup.cs`, `DashboardOptions.cs`, `LoginThrottle.cs` (all read).

## Test files

`tests/PlexToJellyfinSync.Tests/DashboardStartupTests.cs`, `tests/PlexToJellyfinSync.Tests/TrustedProxyParserTests.cs`
(new), `tests/PlexToJellyfinSync.Tests/DashboardOptionsTests.cs`,
`tests/PlexToJellyfinSync.Tests/ServiceCollectionExtensionsTests.cs`, `tests/PlexToJellyfinSync.Tests/LoginThrottleTests.cs`,
`tests/PlexToJellyfinSync.Tests/ForwardedHeadersWarningTests.cs` (new) with the new test double
`tests/PlexToJellyfinSync.Tests/RecordingForwardedHeadersWarningLogger.cs` (records level and message, like
`RecordingWorkerLogger`).

Existing call sites of a changed signature: `DashboardStartupTests.DashboardStartupCreateForwardedHeadersOptionsHonorsForwardedForAndProto`
calls the parameterless overload, which goes away: the Dev changes the call to
`CreateForwardedHeadersOptions(new DashboardOptions())` in step 4 so *Build* passes; its two `IsEmpty` assertions
pin the defect, so the Tester replaces that test in step 5 by the AC1-AC3 tests (keeping the header-flag
assertions in one of them). `Program.cs:50` is production code (Dev).

Files no unit test can cover: none. The changed lines in `Program.cs` (about five) are startup glue without a
test; the changed-lines coverage stays above the gate with the rest of the change.

## Areas

- `docs/areas/dashboard-and-auth.md`: *Forwarded headers* rewritten (trusted senders: loopback and
  `Dashboard:TrustedProxies`; from any other sender both headers are ignored; rightmost entry, one hop; the trusted
  proxy must set or append `X-Forwarded-For` — a proxy that passes the client's header through unchanged lets the
  client choose the rightmost entry; how to trust every sender, `0.0.0.0/0` and `::/0`; the once-per-process
  warning of AC10 with its text); *Login throttle* last bullet (bound 1024, prune on admission, the shared record,
  success does not clear it); *Limits and errors* first bullet rewritten (the bypass is gone; what an unconfigured
  proxy deployment gets; the shared-record lockout during a flood; the residual rate: a client with many real
  source addresses, such as an IPv6 /64, can hold up to 1024 records, in steady state about 200 guesses per minute
  plus those of the shared record, an accepted trade-off); *Related decisions* adds 0022.
- `docs/areas/configuration.md`: settings table row `Dashboard:TrustedProxies` | `PLEXSYNC__Dashboard__TrustedProxies__0`,
  `__1`, … | empty list | IPv4 addresses in dotted-decimal form, IPv6 addresses, either with `/` and a prefix length;
  each entry set with its index (a value without index is not read as a list entry); *Limits and errors* first
  bullet adds an invalid entry.

## Documentation updates

Sweep done over `docs/areas/*.md`, `docs/ARCHITECTURE.md`, `README.md`, `SECURITY.md`, `.squad/project.md` for the
settings tables, the forwarded-header statements and the throttle's pruning. All edits: **Dev**.

- `README.md`: configuration table row after `Dashboard:LogBufferSize` (key, env var `PLEXSYNC__Dashboard__TrustedProxies__0`,
  `__1`, …, default "–", "IP addresses or CIDR networks of reverse proxies whose `X-Forwarded-For`/`X-Forwarded-Proto`
  are trusted, each with its index; loopback is always trusted").
- `SECURITY.md`, *Deployment Security Considerations*: the reverse-proxy bullet adds "and list its address in
  `Dashboard:TrustedProxies`".
- `docs/ARCHITECTURE.md` lines 194-198: `DashboardStartup` builds the options from `Dashboard:TrustedProxies` on top of
  the loopback defaults, linking decision 0022; lines 219-222: the throttle prunes when admitting a new client to a
  full record set and shares one record beyond it.
- `.squad/project.md`: *Security areas*, Dashboard authentication bullet adds "the forwarded-header trust
  (`DashboardStartup`, `Dashboard:TrustedProxies`)"; *Integration surface*, dashboard bullet "the throttle's backoff and
  pruning behavior" becomes "the throttle's backoff, pruning and bound, and the trusted-proxy list".
- The two area documents under *Areas*.

## Architecture check

No guarantee of `.squad/project.md` changes: the dashboard stays open without a token, `/health` stays public,
the cookie keeps `Secure` only on HTTPS. The forwarded-header flow in `docs/ARCHITECTURE.md` changes (update above,
linking 0022). Behavior change for upgraders behind a non-loopback proxy (decision 0022, *Consequences*); the PR
description names the setting to add.

## Security considerations

- [ ] No limits on parsed input beyond the framework's; the configuration entries are trimmed text, not decoded.
- [ ] Throttle cost: the full-map prune scans at most 1024 records under the one lock, only when a new client
  meets a full map; that is cheap at 1024, and `MaxTrackedClients` must not be raised without revisiting it (code
  comment on the constant, Dev).
- [ ] Exceptions reaching a user: none new. The validation message names index and variable, never the value.

## Decision records

- `docs/decisions/0022-forwarded-headers-from-configured-proxies-only.md` (Proposed), amends released 0007.

## Out of scope / follow-ups

- A log line per untrusted request (attacker-controlled volume) or at startup listing the trusted networks: not
  added; the single warning of AC10 is.
- `X-Forwarded-Host` and `X-Forwarded-Prefix` stay disabled.

## Challenge

Devil's Advocate, three minor objections; all accepted.

1. *A failed `IPNetwork.TryParse` leaves `0.0.0.0/0`, so a skipped entry could become "trust all".* Accepted:
   AC9 with `DashboardStartupInvalidTrustedProxyIsNotTrusted`; the parser no longer calls `IPNetwork.TryParse`
   (*Approach* 2).
2. *No IPv6 trust case.* Accepted: `DashboardStartupForwardedHeadersFromTrustedIPv6NetworkAreApplied` under AC3.
3. *Upgraders lose `Secure` and the per-client throttle silently.* Accepted: a once-per-process warning (AC10,
   *Approach* 5). A startup warning for an empty list was rejected because every home deployment without a proxy,
   the default case, would see it. Recorded in decision 0022 (*Options considered* 9, *Consequences*).

## Security review

- **B1 (blocking), non-canonical IPv4 forms trusted networks the operator never wrote.** Resolved: the parser
  requires canonical dotted-decimal IPv4 (`address.ToString()` equals the text), a digits-only prefix without
  leading zero (*Approach* 2); AC4 lists the rejected forms; the measured inputs are DataRows of
  `TrustedProxyParserInvalidEntryReturnsFalse` (fact 18). The *Approach* 2 sentence claiming they fail closed is
  gone. The whole list of accepted forms was re-checked: canonical IPv4, IPv6 without `.`/`%`/`[`/`]`, optional
  canonical prefix; everything else is rejected.
- **N1, zone ids and brackets.** Taken: `%`, `[`, `]` rejected; DataRows `fe80::1%eth0/64`, `[::1]/128`.
- **N2, list entries need their index.** Taken: README and `configuration.md` show `__0`, `__1`, …; fact 20 is
  measured by the Dev before the row is written.
- **N3, a pass-through proxy.** Taken: `dashboard-and-auth.md` *Forwarded headers* states the proxy must set or
  append `X-Forwarded-For`.
- **N4, residual brute-force rate.** Taken: stated in *Limits and errors* (about 200 guesses per minute from 1024
  real addresses plus the shared record) and in 0022 *Consequences* as the accepted trade-off.
- **N5, scan cost under the lock.** Taken: *Security considerations* and 0022 *Consequences*; a comment on
  `MaxTrackedClients` says so.

## Security notes

Non-blocking findings of Security's `APPROVED` plan review (round 2), verbatim:

- **N6, warning wording.** The text names `{Sender}` and then says "List the reverse proxy in Dashboard:TrustedProxies". The first untrusted sender to claim the one warning may be an attacker reaching the port directly, and an operator may copy that address into the list. Suggestion: make the text say that `{Sender}` should be listed only if it is the operator's reverse proxy (e.g. "If {Sender} is your reverse proxy, list it in …"). Because only the first sender claims the line, an attacker's request can also take the slot before the real proxy's; worth one sentence in the *Forwarded headers* section of `docs/areas/dashboard-and-auth.md`.
- **N7, IPv6 with a port.** An unbracketed `2001:db8::1:80` is a valid IPv6 address and is accepted as that address, not as host `2001:db8::1` port 80. This is inherent to IPv6 text, and brackets are rightly rejected. One sentence in the `configuration.md` row ("no port") would head off the mistake.
