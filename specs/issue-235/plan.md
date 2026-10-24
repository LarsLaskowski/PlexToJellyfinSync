# Plan: Warn when the /accounts answer holds no usable owner account

Source: Issue #235
Status: Draft
Tier: standard — a new warning on a code path that today falls back silently is an observable behavior change of the owner resolution, but no security control is created, changed or removed.
Security areas touched: HTTP calls to Plex (`PlexClient.GetOwnerAccountIdAsync`; the change adds a constant log line there and changes no request, timeout or JSON handling)

## Problem / root cause

`PlexClient.GetOwnerAccountIdAsync` (`src/PlexToJellyfinSync.Service/PlexClient.cs:481-520`) logs the warning
`Could not auto-detect the Plex owner account id, defaulting to 1` only in its `catch` for `HttpRequestException`
or `JsonException` (line 514-517). When the request succeeds and the answer is readable but holds no account with
a positive id — `response` null (line 491), `MediaContainer` or `Account` missing (`accounts is null`, line 493),
or no element with `Id > 0` (`owner is null`, line 499) — control leaves the `try` block without a log line and
reaches `return 1` (line 519). The history request is then made with `accountID=1`
(`PlexClient.cs:552`), and `AppendHistoryPage` keeps only entries of that account (`PlexClient.cs:253-255`),
without the operator having been told that the owner was guessed.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | Issue: an answer without a usable account falls back to id `1` without a warning | `PlexClient.cs:491-503` fall through to `return 1` at line 519; the only `LogWarning` is in the `catch` at line 516 | confirmed |
| 2 | Issue: the code sits "around lines 506-532" | the method spans `PlexClient.cs:481-520` on base 50014bf; 506-532 is the `catch` clauses and the start of `GetLibrariesAsync`; the behavior claim stands (row 1) | refuted |
| 3 | Issue: the failed-request case logs a warning | `PlexClient.cs:514-517` (`HttpRequestException` other than 401/403, `JsonException`) | confirmed |
| 4 | Issue: the history run then filters by an account that may not be the owner | `SyncOrchestrator.cs:283` passes the resolved id to the history request; `PlexClient.cs:552` (`accountID={accountId}`) and `PlexClient.cs:253-255` drop entries of other accounts | confirmed |
| 5 | Issue: `Plex:OwnerAccountId` already sets the id explicitly | `PlexClient.cs:483-486`; `PlexOptions.cs:35-36` (`Range(1, int.MaxValue)`); `docs/areas/configuration.md:34` | confirmed |
| 6 | A fallback id is cached until a run fails, so the silent guess holds for every following successful run | `SyncOrchestrator.cs:71-76` (`_ownerAccountId ??=`) and its `HandleError` summary; `docs/areas/sync-pipeline.md:80-81` | confirmed |
| 7 | The area document states the silent case today ("an answer without a usable account also yields `1`") | `docs/areas/sync-pipeline.md:75-77` | confirmed |
| 8 | Record 0002 names the fallback to id `1` and is released, so it is not edited; the change keeps that fallback and so stays consistent with it | `docs/decisions/0002-single-user-plex-owner.md:23-24`; `git ls-remote --tags origin` lists `v1.5.0` and earlier tags, 0002 dated 2026-10-01 | confirmed |
| 9 | `PlexClientTests` builds every client with `NullLogger<PlexClient>.Instance`, so no existing test observes log output; no recording logger for `PlexClient` exists | `tests/PlexToJellyfinSync.Tests/PlexClientTests.cs:180`, `:1312-1315`; the test project holds only `RecordingSyncStatusLogger`, `RecordingWorkerLogger`, `RecordingForwardedHeadersWarningLogger` | confirmed |
| 10 | Related defect: an `Account` array holding a JSON `null` element (`[null]`) makes `a.Id` in the `Where` (line 495) throw `NullReferenceException`, which no `catch` handles, so the run fails instead of falling back; the same projection pattern exists for `Directory` (line 533); out of scope, follow-up below | `PlexAccountsContainer.Account` is `List<PlexAccount>?` without a converter; `PlexJsonOptions` sets no null handling; System.Text.Json stores a JSON `null` element of a reference-type list as `null` (documented library behavior, read, not run) | confirmed |

## Acceptance criteria

- [ ] AC1: When the `/accounts` answer is read successfully but holds no account with a positive id — the body
  `null`, `{}` (no `MediaContainer`), `{"MediaContainer":{}}` (no `Account`), `{"MediaContainer":{"Account":[]}}`,
  or only accounts with an id of `0` or below — `GetOwnerAccountIdAsync` returns `1` and logs exactly one
  warning, without an exception, with the exact text
  `Could not auto-detect the Plex owner account id: the account list contains no account with a positive id, defaulting to 1 (set Plex:OwnerAccountId to choose the owner)`.
  Decided by the fall-through of the `try` block in `PlexClient.GetOwnerAccountIdAsync` (`accounts is null` or
  `owner is null`).
- [ ] AC2: An answer with an account of positive id logs no warning, and a failed or unreadable answer (malformed
  JSON) still logs exactly one warning — the existing one with the `JsonException` attached — not the AC1 text
  as well.

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | `PlexClientGetOwnerAccountIdWithoutUsableAccountLogsWarningAndFallsBackToDefaultOwner` (`[DataRow]` per body: `null`, `{}`, `{"MediaContainer":{}}`, `{"MediaContainer":{"Account":[]}}`, `{"MediaContainer":{"Account":[{"id":0,"name":""},{"id":-3,"name":"x"}]}}`; asserts the return value `1`, one warning, its exact text and a `null` exception) | `tests/PlexToJellyfinSync.Tests/PlexClientTests.cs` |
| AC2 | `PlexClientDetectsOwnerWithoutWarning` (`AccountsJson`, returns `4`, zero warnings); `PlexClientGetOwnerAccountIdWithMalformedResponseLogsOneWarning` (`{ not valid json`, one warning, exception is a `JsonException`, text is not the AC1 text). Both pass on the current code: write them in step 5 marked deferred, they guard the placement of the new call | `tests/PlexToJellyfinSync.Tests/PlexClientTests.cs` |

## Approach

In `PlexClient.GetOwnerAccountIdAsync`, inside the `try` block and directly after the closing brace of
`if (accounts is not null) { … }` (after line 503), add one statement:

```csharp
_logger.LogWarning("Could not auto-detect the Plex owner account id: the account list contains no account with a positive id, defaulting to 1 (set Plex:OwnerAccountId to choose the owner)");
```

Every successful detection returns inside the `if` before it, the exception paths jump to their `catch` before
it, so it runs exactly on the silent fall-through and nowhere else; `return 1` at the end stays as it is. No
other statement of the method changes. The log line is a constant: no value from the Plex answer goes into it.

The test double: `tests/PlexToJellyfinSync.Tests/RecordingPlexClientLogger.cs`, an `ILogger<PlexClient>` that
records the formatted message and the exception of each `Warning` entry (modelled on
`RecordingSyncStatusLogger`). `CreateClient` in `PlexClientTests` gets an optional logger parameter (signature
below); its existing call sites keep compiling unchanged.

New user-visible output: the single log line above, at level `Warning`; no layout beyond its text.

## Options considered

1. **Warn and fall back to `1`** (chosen) — the issue's first expectation; identical in effect to the
   failed-request case, keeps the fallback recorded in 0002 and the area document, and an existing installation
   whose `/accounts` answer is empty keeps syncing.
2. **Fail the owner resolution** (throw, so the run fails and the dashboard shows the error) — visible, but a
   behavior change for every installation that syncs today on the guessed id `1` (often the right one: the
   owner is usually account `1`), and inconsistent with the failed-request case, which falls back. Changing
   that guarantee would also need the Product Manager and a record superseding 0002. Lost.
3. **Restructure the method to one fallback exit with one shared warning** — the same message for both cases
   loses the information which case occurred, and moves more lines for no gain. Lost.

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| `PlexToJellyfinSync.Service` | `PlexClient.cs` (`GetOwnerAccountIdAsync`) | one `LogWarning` statement (Dev) |
| `PlexToJellyfinSync.Tests` | `PlexClientTests.cs`, new `RecordingPlexClientLogger.cs` | three tests, optional logger on `CreateClient`, new test double (Tester) |

## Signatures (for the Dev's skeleton)

No production signature changes; step 4 is not needed. Test code (Tester):

- `internal sealed class RecordingPlexClientLogger : ILogger<PlexClient>` with
  `public List<(string Message, Exception? Exception)> Warnings { get; } = [];`
- `private static PlexClient CreateClient(HttpClient httpClient, PlexOptions? options = null, ILogger<PlexClient>? logger = null)`
  (uses `logger ?? NullLogger<PlexClient>.Instance`)

## Test files

- `tests/PlexToJellyfinSync.Tests/PlexClientTests.cs` (existing)
- `tests/PlexToJellyfinSync.Tests/RecordingPlexClientLogger.cs` (new test double, flat in the test project)

Existing test code that calls a changed signature: the `CreateClient` call sites in `PlexClientTests.cs` —
unaffected, the new parameter is optional; the Tester adds it.

Files of the change no unit test can cover: none.

## Areas

- `docs/areas/sync-pipeline.md`, *Owner only*: the answer without a usable account now logs a warning (text
  quoted) before the id `1` is used.

## Documentation updates

Sweep: `docs/areas/*.md`, `docs/ARCHITECTURE.md`, `README.md` and `docs/decisions/` searched for `owner`,
`OwnerAccountId`, `/accounts`, `defaulting to 1` and `yields`.

- `docs/areas/sync-pipeline.md:76-77` — owner: Dev. Replace "an answer without a usable account also yields `1`."
  with "an answer that holds no account with a positive id also yields `1`, with the warning `Could not
  auto-detect the Plex owner account id: the account list contains no account with a positive id, defaulting to
  1 (set Plex:OwnerAccountId to choose the owner)`."
- `docs/ARCHITECTURE.md:132-137` — no change: it names "the fallback id" and points to *Owner only*.
- `README.md:94` — no change ("auto-detected").
- `docs/decisions/0002-single-user-plex-owner.md:23-24` — no change: released, and the fallback to `1` it names
  still holds.

## Architecture check

The owner-only guarantee (0002, `docs/areas/sync-pipeline.md`, *Owner only*) is untouched: the resolution order,
the fallback id, the propagated failures (401/403, timeout, cancellation) and the owner cache with its reset on a
failed run stay as they are. Only a log line is added. No guarantee of `.squad/project.md` is weakened.

## Security considerations

- [x] No limits: no input is parsed differently and no length or byte limit is added.
- [x] None: no exception reaches a user-visible failure reason; the new line is a warning without an exception,
  and its text is a constant without Plex data, token or URL.

## Decision records

- none: no decision beyond the obvious fix — the fallback recorded in 0002 is kept; the rejected alternative
  (failing the resolution) is named under *Options considered* above.

## Out of scope / follow-ups

- Follow-up issue (orchestrator creates it): **"Null elements in Plex list responses throw NullReferenceException
  in PlexClient"** — body: "`PlexClient` projects Plex list responses without skipping `null` elements: an
  `/accounts` answer `{"MediaContainer":{"Account":[null]}}` makes `accounts.Where(a => a.Id > 0)`
  (`GetOwnerAccountIdAsync`) throw `NullReferenceException`, which no `catch` handles, so the run fails with a raw
  runtime exception instead of the documented fallback; `GetLibrariesAsync` (`d.Key` on `Directory`) has the same
  pattern. Expected: a `null` element is skipped like an element without a usable id or key. Found while planning
  #235."
- Changing the fallback itself (failing the run instead) — Product Manager decision, not requested.
