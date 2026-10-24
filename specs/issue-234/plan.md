# Plan: Validate Plex rating keys and library keys before they go into a request path

Source: Issue #234
Status: Draft
Tier: security — the change creates an allow-list guard on untrusted input from Plex that decides which request paths the application builds, i.e. a guard whose purpose is to stop a hostile server or an attacker on the path from steering requests.
Security areas touched: HTTP calls to Plex (`PlexClient`: request paths built from Plex-supplied keys, untrusted JSON); Logging of external data (hostile key values no longer reach log lines; two new warnings that carry no key value)

## Problem / root cause

`PlexClient` interpolates Plex-supplied keys into request paths without a guard that covers URL syntax:

- `AppendHistoryPage` (`src/PlexToJellyfinSync.Service/PlexClient.cs:240`) only checks a history entry's rating
  key with `string.IsNullOrWhiteSpace`; the key is returned as `PlexHistoryEntry.RatingKey` and
  `SyncOrchestrator` passes it to `GetMediaItemAsync`, which builds `/library/metadata/{ratingKey}` (`:598`).
- `GetLibrariesAsync` (`:541`) only checks a library key for being non-blank; `LibraryReconciler` (`:195`, `:199`)
  passes it to `GetLibraryItemsAsync`, which builds `/library/sections/{libraryKey}/all` (`:618`).
- The rule the issue calls the "64-character / no-whitespace check", `IsValidRatingKey` (`:180-185`), is applied
  to item rating keys (`MapMediaItem`, `:329`) and show rating keys (`ResolveShowRatingKey`, `:312`), but it
  accepts every URL-structural character (`/`, U+005C backslash, `.`/`..` segments, `?`, `#`, `%`, `&`, `=`,
  `;`, `:`) and non-ASCII characters. Those keys are put into `/library/metadata/{showRatingKey}/allLeaves`
  (`:612`, called from `LibraryReconciler:144` and `SeriesAggregateWriter:125`) and `/library/metadata/{key}`
  (`SeriesAggregateWriter:104`). So the issue understates the gap: even the "validated" keys can change the path.

Related defect found on the way: the unvalidated history rating key reaches log lines — the debug line
`_logLibrarySkipped` (`SyncOrchestrator.cs:177`) and the error `Processing item {RatingKey} failed`
(`SyncOrchestrator.cs:110`) — so control characters and line breaks from Plex reach the dashboard log buffer.
The fix below closes it, because such an entry no longer leaves `PlexClient`.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | Issue: a history entry's rating key is only checked for non-blank "around line 253" | The check is at `PlexClient.cs:240` (`string.IsNullOrWhiteSpace(entry.RatingKey)`) at `fe2f836`; the line number is off (refuted), the substance holds | confirmed |
| 2 | Issue: the 64-char / no-whitespace check applies only to metadata returned afterwards | `IsValidRatingKey` is called only from `MapMediaItem` (`:329`) and `ResolveShowRatingKey` (`:312`) | confirmed |
| 3 | Issue: library keys are only checked for non-blank | `GetLibrariesAsync` `:541` | confirmed |
| 4 | Issue: risk is low because input comes from the configured Plex server | Every key-built path starts with the literal `/library/` (`:598`, `:612`, `:618`); a relative reference that starts with `/` followed by a segment cannot carry a scheme or authority (RFC 3986 section 4.2), so the host stays `Plex:BaseUrl` and the `X-Plex-Token` default header (`ServiceCollectionExtensions.cs:79`) goes nowhere else | confirmed |
| 5 | The existing rule lets URL-structural characters through | `IsValidRatingKey` `:182-184` rejects only blank, > 64, `char.IsControl`, `char.IsWhiteSpace` | confirmed |
| 6 | Show and item rating keys from metadata are put into request paths | `LibraryReconciler.cs:116`, `:144`; `SeriesAggregateWriter.cs:104`, `:125`; `SyncOrchestrator.cs:172` collects `ShowRatingKey` | confirmed |
| 7 | Unvalidated history rating keys reach log lines | `SyncOrchestrator.cs:177` (debug), `:110` (error) | confirmed |
| 8 | Real Plex keys are decimal numbers | Samples `TestData/Plex/history.json` (`1001`, `2003`), `metadata-movie.json`, `metadata-episode.json`; library ids are numbers in history (`librarySectionID`, decision 0023) and the `Plex:Libraries` filter compares them with library keys | confirmed |
| 9 | Every existing test key passes the new rule | All `ratingKey`, `grandparentRatingKey` and library `key` values in `PlexClientTests.cs` and `TestData/Plex/` are digits, except the deliberately invalid `12` U+000A `345`, `5` U+000A `00` and the 65-character key | confirmed |
| 10 | `char.IsAsciiLetterOrDigit` rejects non-ASCII digits and letters (U+0661, U+FF11, U+00E9) | .NET API contract (`char.IsAsciiLetterOrDigit`, .NET 7+): true only for `0-9`, `A-Z`, `a-z` — the step-5 test rows with these characters fail on today's code and pass only with an ASCII check | confirmed |
| 11 | The history mark is set only from entries `GetHistorySinceAsync` returns | `SyncOrchestrator.ProcessHistoryEntriesAsync` `:157-185` takes `maxViewed` from the returned entries | confirmed |
| 12 | No released decision record covers key validation | Index `docs/decisions/README.md` (0001-0024); `git ls-remote --tags origin` lists v1.0.0-v1.5.0, so 0025 is new | confirmed |

## Acceptance criteria

The **key rule**: a key is valid when it has 1 to 64 characters and each character is an ASCII letter
(`A-Z`, `a-z`), an ASCII digit (`0-9`), `-` or `_`. The **hostile forms** every test row set below draws from
(written as text; the Tester writes U+ characters as C# escapes): `1/2`, `1/../../accounts`, `.`, `..`,
`1` U+005C `2`, `1?X-Plex-Container-Size=100000`, `1#`, `1%2F2`, `1&a=b`, `1;a`, `1:2`, `1.2`, `1 2`,
`1` U+000A `2`, `1` U+0661, `1` U+FF11, U+00E9, and `1` repeated 65 times.

- [ ] AC1: `GetHistorySinceAsync` drops an entry whose rating key is present (not blank) but fails the key rule,
  once it has passed the since and account filters, and logs the warning `Skipping a Plex history entry with an
  invalid rating key` once per dropped entry; the entries around it on the same page are still returned. An
  invalid-key entry at or before "since" still stops paging (it counts for `reachedSince`). An entry without a
  rating key is still ignored without a warning.
- [ ] AC2: `GetLibrariesAsync` drops a library whose key is present but fails the key rule and logs the warning
  `Skipping a Plex library with an invalid key` once per dropped library; a library without a key is still dropped
  without a warning; valid libraries of the same answer are returned.
- [ ] AC3: Item rating keys (`GetMediaItemAsync`, `GetLibraryItemsAsync`, `GetEpisodesAsync`) and show rating keys
  are checked with the key rule instead of today's rule: an item with a hostile-form key is skipped with the
  existing warning `Skipping a Plex item with a missing or invalid rating key`; a show rating key with a
  hostile-form key is dropped with the existing warning; a key such as `aZ09-_` and a 64-character key are accepted.
- [ ] AC4: `GetMediaItemAsync(ratingKey)`, `GetEpisodesAsync(showRatingKey)` and `GetLibraryItemsAsync(libraryKey)`
  called with a key that fails the key rule (blank included) throw `ArgumentException` whose `ParamName` is the
  method's parameter name and send no request (`StubHttpMessageHandler.Requests` stays empty).
- [ ] AC5: No warning and no exception message of AC1, AC2 or AC4 contains the key, nor any other value from
  Plex: the AC1 and AC2 warnings are the exact texts above, and the AC4 message is exactly
  `The key is not a valid Plex key` plus the runtime's parameter suffix (asserted with `StartsWith` and a
  check that the key does not occur in `Message`).

A hostile-form row whose escape got lost (the U+005C, U+000A, U+0661, U+FF11, U+00E9 rows) turns into a string of
ASCII letters and digits, which the key rule accepts: the drop/throw assertions of that row then fail, so a lost
escape cannot pass silently. Each such row additionally asserts that its input contains the intended character.

## Tests per criterion

All in `PlexClientTests`. JSON bodies carrying a hostile key are built with `JsonSerializer.Serialize(key)` for the
key value, not hand-escaped. Warnings are read with `RecordingPlexClientLogger`.

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | `PlexClientGetHistorySinceSkipsEntryWithInvalidRatingKey` (DataRow per hostile form: one hostile entry between two valid ones of the owner, newer than since; asserts the two valid keys returned, one warning with the exact text) | `tests/PlexToJellyfinSync.Tests/PlexClientTests.cs` |
| AC1 | `PlexClientGetHistorySinceInvalidRatingKeyAtSinceStopsPaging` (full page of 500 whose last entry is at "since" with key `1/2`; asserts one request, no warning; passes on today's code, so deferred to step 6 — it guards the order of *Approach* step 2) | `tests/PlexToJellyfinSync.Tests/PlexClientTests.cs` |
| AC2 | `PlexClientGetLibrariesSkipsLibraryWithInvalidKey` (DataRow per hostile form; asserts the valid library returned and exactly one warning with the exact text; the existing `PlexClientGetLibrariesMapsKinds` keeps the keyless case silent — add an assertion there that no warning was logged) | `tests/PlexToJellyfinSync.Tests/PlexClientTests.cs` |
| AC3 | `PlexClientGetLibraryItemsSkipsItemsWithRatingKeyOutsideAllowList` (DataRow per hostile form), `PlexClientGetEpisodesWithShowRatingKeyOutsideAllowListClearsShowRatingKey` (DataRow per hostile form), `PlexClientGetMediaItemWithAllowListedRatingKeyMapsItem` (DataRows `aZ09-_` and 64 times `1`, response registered under `/library/metadata/<key>`) | `tests/PlexToJellyfinSync.Tests/PlexClientTests.cs` |
| AC4 | `PlexClientGetMediaItemWithInvalidKeyThrowsWithoutRequest`, `PlexClientGetEpisodesWithInvalidKeyThrowsWithoutRequest`, `PlexClientGetLibraryItemsWithInvalidKeyThrowsWithoutRequest` (DataRow per hostile form plus empty string; `Assert.ThrowsExactlyAsync<ArgumentException>` around the call and the full enumeration; `ParamName` `ratingKey` / `showRatingKey` / `libraryKey`; `handler.Requests` empty) | `tests/PlexToJellyfinSync.Tests/PlexClientTests.cs` |
| AC5 | Asserted inside the AC1, AC2 and AC4 tests above: exact warning text, `Message.StartsWith("The key is not a valid Plex key")`, `Message.Contains(key) == false` for every non-empty key | `tests/PlexToJellyfinSync.Tests/PlexClientTests.cs` |

Regression (existing, unchanged, must stay green): `PlexClientGetHistorySinceSampleMapsLibrarySectionKey` (real
sample keys pass the new rule), `PlexClientGetMediaItemMapsMovieMetadata`, `PlexClientGetEpisodesSendsContainerPaging`,
`PlexClientGetLibraryItemsSendsContainerPaging` (request paths for valid keys unchanged),
`PlexClientGetMediaItemWithInvalidRatingKeyReturnsNull`, `PlexClientGetMediaItemWithOverlongRatingKeyReturnsNull`,
`PlexClientGetEpisodesWithInvalidShowRatingKeyClearsShowRatingKey`, `PlexClientGetLibraryItemsSkipsItemsWithInvalidRatingKey`.

## Approach

All in `src/PlexToJellyfinSync.Service/PlexClient.cs`; no interface, DTO, option or caller changes.

1. Replace `IsValidRatingKey` by `IsValidKey` implementing the key rule (`char.IsAsciiLetterOrDigit(c) || c == '-'
   || c == '_'`, length 1-64, `MaxRatingKeyLength` renamed `MaxKeyLength`). `MapMediaItem` and
   `ResolveShowRatingKey` call it unchanged otherwise (AC3).
2. `AppendHistoryPage` becomes an instance method (it logs). Order per entry: skip `null`, missing viewed-at or
   blank rating key silently (as today); `viewedAt <= since` → `reachedSince` and skip (as today, any key); account
   mismatch → skip; then `IsValidKey(entry.RatingKey) == false` → warning `Skipping a Plex history entry with an
   invalid rating key` and skip; else add (AC1).
3. `GetLibrariesAsync`: a blank key is dropped silently; a present key failing `IsValidKey` is dropped with the
   warning `Skipping a Plex library with an invalid key` (AC2). Neither warning has a placeholder.
4. New `RequireValidKey(string key, string paramName)` throws `new ArgumentException("The key is not a valid Plex
   key", paramName)` when `IsValidKey` fails, else returns the key. `GetMediaItemAsync`, `GetEpisodesAsync` and
   `GetLibraryItemsAsync` build their path from its return value; in the two `IAsyncEnumerable` methods the call
   comes before `GetPagedMediaItemsAsync`, so it throws at the call, before any request (AC4).

Log wording is fixed above; no other user-visible output.

## Options considered

The full comparison is in decision 0025: (1) allow-list at entry plus a fail-closed check at the request path
(chosen); (2) percent-encoding the key — loses because `.`/`..` stay unencoded and remove path segments, the result
depends on the URL parser's handling of encoded characters, and hostile values still reach log lines; (3) the
existing rule extended to history and libraries — what the issue literally asks, but `/`, `?`, `#`, `..` still pass;
(4) digits only — no safer than (1) and skips any server that uses letters.

Forms the consumer accepts (`System.Uri` resolving the relative path against `Plex:BaseUrl`) and the guard's
behavior on each: path separator `/` and U+005C (normalized to `/` for http/https) — rejected; dot segments `.`/`..`
(removed with the preceding segment) — rejected, `.` is not allowed; query `?` with `&`/`=` (overrides paging) —
rejected; fragment `#` (drops `/allLeaves`, `/all` and the paging query) — rejected; percent-encoding `%XX` (the
parser may or may not decode `%2E`/`%2F`) — rejected, `%` is not allowed, so the decoding question does not arise;
`;`, `:`, `@` — rejected; non-ASCII letters/digits (IRI conversion) — rejected by the ASCII check; whitespace and
control characters (trimmed or escaped by the parser) — rejected; empty — rejected. The accepted characters are all
RFC 3986 *unreserved* and never form a dot segment, so no accepted key can change the path structure.

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| PlexToJellyfinSync.Service | `PlexClient` (`src/PlexToJellyfinSync.Service/PlexClient.cs`) | key rule, history and library filtering with warnings, request-path guard |
| PlexToJellyfinSync.Tests | `PlexClientTests` | new tests per criterion |

## Signatures (for the Dev's skeleton)

No public or interface signature changes, so step 4 (*Skeleton*) does not apply; the tests compile against the
existing API. Private members (Dev, step 6):

- `private const int MaxKeyLength = 64;` (replaces `MaxRatingKeyLength`)
- `private static bool IsValidKey(string? key)` (replaces `IsValidRatingKey`)
- `private static string RequireValidKey(string key, string paramName)`
- `private bool AppendHistoryPage(IReadOnlyList<PlexMetadata?> page, DateTimeOffset since, int accountId, List<PlexHistoryEntry> result)` (no longer `static`; moves to the *Methods* region)

## Test files

- `tests/PlexToJellyfinSync.Tests/PlexClientTests.cs` (existing class, new methods).

Existing test code that calls a changed signature: none (only private members change).

Files of the change no unit test can cover: none.

## Areas

- `docs/areas/sync-pipeline.md` — *Incremental history run* (an entry with an invalid rating key is ignored with a
  warning and does not advance the mark), *Full reconcile* or *What is read from Plex* (a library with an invalid
  key is ignored with a warning), *What is read from Plex* (the key rule replaces "longer than 64 characters or
  contains whitespace or a control character" for items and show references, and the statement that no request is
  made with a key that fails it).

## Documentation updates

Sweep of `docs/areas/*.md`, `docs/ARCHITECTURE.md`, `README.md`, `SECURITY.md` for rating key, library key,
`/library/` and validation statements:

- `docs/areas/sync-pipeline.md:38` (history entry without rating key ignored), `:98-100` (item rating key rule),
  `:57-60` (*Full reconcile*, libraries) — update as under *Areas*. Owner: **Dev**.
- `docs/areas/sync-pipeline.md:103` (a `null` library counts as one without a key) — still true, no change.
- `docs/areas/configuration.md:35` (`Plex:Libraries` "compared exactly; not validated") — still true: the setting is
  not validated, a configured key outside the rule simply matches no library. No change.
- `docs/ARCHITECTURE.md:125-127` (endpoint list `/library/metadata/{key}` …) — endpoints unchanged, and it already
  points to *What is read from Plex*. No change.
- `README.md:95` (`Plex:Libraries`) — no change.
- `docs/decisions/0025-plex-keys-allow-listed-before-request-paths.md` and its index row — written by the **Lead**
  (done).

## Architecture check

No guarantee of `.squad/project.md` / `docs/ARCHITECTURE.md` changes. Per-item isolation holds: dropped entries,
items and libraries are skipped inside `PlexClient` without an exception; the `ArgumentException` of AC4 is
unreachable from today's callers (every key they pass comes out of `PlexClient` filtered by the same rule) and
would be a failed item under the existing isolation if a future caller passed one. Plex's JSON shape still stays
inside `PlexClient`.

## Security considerations

- [ ] The 64-character limit counts UTF-16 characters of the JSON-decoded string; since only ASCII characters pass,
  characters and bytes coincide. It applies after JSON decoding, as before.
- [ ] Exceptions that can reach a user-visible failure reason: the AC4 `ArgumentException`, text `The key is not a
  valid Plex key (Parameter '<name>')` — no Plex value — recorded as last error only if a future caller passed
  an invalid key. No other new exception.
- [ ] New log lines carry no Plex value (AC5), so no secret or hostile text enters the dashboard log buffer through
  them; the removed path for hostile history keys into `SyncOrchestrator`'s log lines closes the related defect.

## Decision records

- `docs/decisions/0025-plex-keys-allow-listed-before-request-paths.md` (Proposed)

## Out of scope / follow-ups

- Advancing the high-water mark past a dropped history entry (it repeats its warning while it is the newest entry;
  accepted in decision 0025).
- Validating `Plex:Libraries` entries at startup.
