# Plan: Null elements in Plex list responses throw NullReferenceException in PlexClient

Source: Issue #246
Status: Draft
Tier: standard — a behavior change on untrusted Plex JSON (a `null` list element is skipped instead of failing the run) that creates, changes or removes no security control.
Security areas touched: HTTP calls to Plex (`PlexClient` and the Plex DTOs in `PlexToJellyfinSync.Data.Plex`: untrusted JSON)

## Problem / root cause

System.Text.Json turns a JSON `null` inside an array into a `null` element of the DTO's `List<T>`; the DTOs declare
their elements non-nullable (`PlexAccountsContainer.Account` is `List<PlexAccount>?`), and `PlexClient` dereferences
every element unchecked. Sites, all in `src/PlexToJellyfinSync.Service/PlexClient.cs` at base 142d691:

| Line | Site | What a `null` element does today |
| ---- | ---- | -------------------------------- |
| 495 | `GetOwnerAccountIdAsync`: `accounts.Where(a => a.Id > 0)` | NRE; the `catch` at 516 takes only `HttpRequestException` / `JsonException` |
| 535 | `GetLibrariesAsync`: `d.Key` | NRE |
| 239 | `AppendHistoryPage`: `entry.ViewedAt` | NRE |
| 292 | `FirstHistoryPageKeyOf`: `page[0].RatingKey` (null first entry) | NRE |
| 278 | `FirstRatingKeyOf`: `page[0].RatingKey` (null first entry of a library/episode page) | NRE |
| 328 (via 439) | `MapMediaItem`: `metadata.RatingKey` on a null page element | NRE |
| 149 | `ParseUniqueIds`: `guid.Id` | NRE |
| 374 | `MapMediaItem` genres: `g.Tag` | NRE |

The issue names the first two; the other six are the same defect (requested check of the other projections).

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | `GetOwnerAccountIdAsync` dereferences account elements unchecked and no `catch` in it handles an NRE | `PlexClient.cs` 495, catches 507–519 | confirmed |
| 2 | Issue: "the run fails with a raw runtime exception instead of the documented fallback" | NRE leaves `ResolveOwnerAsync` (`SyncOrchestrator.cs` 73, called at 283 inside the `try`), the catch-all at 310 calls `HandleError` (86): run fails, `errors`+1, `plexConnected` false, Last error = the NRE message; the worker does not stop. Documented fallback: `docs/areas/sync-pipeline.md` 76–79 | confirmed |
| 3 | `GetLibrariesAsync` has the same pattern; it fails the reconcile | `PlexClient.cs` 535; `SyncOrchestrator.ReconcileAsync` 325, catch → `HandleError(ex, true)` at 349 | confirmed |
| 4 | A null element in a library page fails the whole reconcile, in an episode page the show | NRE raised inside the async enumerator, outside the per-item `try` of `LibraryReconciler.cs` 81/110; episodes listed at 144 inside the show's `try` (113–130) | confirmed |
| 5 | History pages, `ParseUniqueIds` and genres throw on a null element | `PlexClient.cs` 239, 292, 149, 374 | confirmed |
| 6 | `Media` / `Part` null elements do not throw: `Media?.FirstOrDefault()?.Part?.FirstOrDefault()?.File` gives no file path, as for `{}` | `PlexClient.cs` 336–339 | confirmed |
| 7 | `GetMediaItemAsync` with `Metadata: [null]` returns `null` without exception | `PlexClient.cs` 593–598 | confirmed |
| 8 | `PlexJsonOptions` sets no converter or `RespectNullableAnnotations`; STJ yields `null` elements for JSON `null` in `List<T>` of a class | to verify: read `PlexJsonOptions.cs` (confirmed: no converter) and the System.Text.Json documentation (annotations not enforced on collection elements); measured when the Tester's step-5 tests for AC1–AC5 fail on the current code with `NullReferenceException` (not `JsonException`), Tester, step 5 | unverified |
| 9 | The Plex DTO list types are used only in `PlexClient`; no test constructs a DTO | `grep` for `PlexAccount`, `PlexDirectory`, `PlexMetadata`, `PlexGuid`, `PlexTag`, `PlexMedia`, `PlexPart` in `src` and `tests` | confirmed |
| 10 | No real Plex sample with a `null` element exists | `TestData/Plex/*.json` contain no `null` | confirmed |

## Acceptance criteria

A `null` element of a list in a Plex answer is handled exactly like an element of that list without any field (`{}`).

- [ ] AC1: `GetOwnerAccountIdAsync` with the answer `{"MediaContainer":{"Account":[null]}}` returns `1`, logs exactly
  one warning with the text `Could not auto-detect the Plex owner account id: the account list contains no account
  with a positive id, defaulting to 1 (set Plex:OwnerAccountId to choose the owner)` and no exception (code path:
  `owner is null` after the filter at `PlexClient.cs` 495–505); with `[null, {"id": 7}]` it returns `7` without a
  warning.
- [ ] AC2: `GetLibrariesAsync` with `Directory: [null, {"key":"1","title":"Movies","type":"movie"}, null]` returns
  exactly the one library with key `1`.
- [ ] AC3: `GetHistorySinceAsync` with a page `[null, <valid owner entry>, null]` returns the valid entry only; a full
  page of 500 whose first element is `null`, served for every request, stops after the second request (non-advancing
  page, the null first element counts as an entry without rating key and viewed-at) with the 499 valid entries.
- [ ] AC4: `GetLibraryItemsAsync` with a page `[null, <valid movie>, null]` yields the valid movie only, and logs the
  warning `Skipping a Plex item with a missing or invalid rating key` once per `null` element (code path:
  `MapMediaItem`'s rating-key guard, `PlexClient.cs` 328–333).
- [ ] AC5: `GetMediaItemAsync` with a movie whose `Genre` is `[null, {"tag":"Drama"}]` and whose `Guid` is
  `[null, {"id":"imdb://tt0113277"}]` maps `Genres` to `["Drama"]` and exactly one unique id (`imdb`,
  `tt0113277`, `IsDefault` true).

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | new `DataRow("""{"MediaContainer":{"Account":[null]}}""", DisplayName = "null Account element")` on the existing `PlexClientGetOwnerAccountIdWithoutUsableAccountLogsWarningAndFallsBackToDefaultOwner`; new `PlexClientGetOwnerAccountIdWithNullAccountElementDetectsRemainingOwner` | `tests/PlexToJellyfinSync.Tests/PlexClientTests.cs` |
| AC2 | `PlexClientGetLibrariesSkipsNullDirectoryElements` | same |
| AC3 | `PlexClientGetHistorySinceSkipsNullEntries`; `PlexClientGetHistorySinceWithNullFirstEntryStopsOnNonAdvancingPage` (built like `PlexClientGetHistorySinceWithUnkeyedFirstEntryStopsOnNonAdvancingPage`) | same |
| AC4 | `PlexClientGetLibraryItemsSkipsNullElements` (with `RecordingPlexClientLogger`) | same |
| AC5 | `PlexClientGetMediaItemSkipsNullGenreAndGuidElements` | same |

## Approach

1. DTOs (`src/PlexToJellyfinSync.Data/Plex/`): make every list element type nullable (*Signatures*). XML docs
   unchanged in meaning.
2. `PlexClient` — each site of the table in *Problem*:
   - accounts (495): `accounts.OfType<PlexAccount>().Where(a => a.Id > 0)…`; libraries (535):
     `directories.OfType<PlexDirectory>().Where(…)`; genres (373): `metadata.Genre.OfType<PlexTag>().Where(…)`;
     unique ids (148): `metadata.Guid.OfType<PlexGuid>().Select(guid => guid.Id)…` — the `IsDefault` index stays
     counted over the filtered ids;
   - `AppendHistoryPage`: `if (entry is null || entry.ViewedAt is not > 0 || …) continue;`;
   - `FirstRatingKeyOf`: `page[0]?.RatingKey`; `FirstHistoryPageKeyOf`: `(page[0]?.RatingKey, page[0]?.ViewedAt)` —
     a null first element is an entry without rating key and viewed-at, as today for `{}`;
   - `MapMediaItem(PlexMetadata? metadata)`: `if (metadata is null || IsValidRatingKey(metadata.RatingKey) == false)`
     → the existing warning and `null`.
   - `Media` / `Part` (336–339) and `GetMediaItemAsync` (593) stay as they are (facts 6, 7).
3. No new log line or user-visible text: the warnings are the existing ones.

No real sample: the change does not alter how the format is read (STJ, `PlexJsonOptions` unchanged) but how a
deserialized `null` element is projected, and no real Plex answer carries one (fact 10). The tests therefore use
hand-written answers for the `null` variant, in the style of the existing `PlexClientTests` constants.

## Options considered

Four options, recorded in [0024](../../docs/decisions/0024-null-elements-in-plex-lists.md): nullable DTO element
types plus checks at each projection (chosen); checks only, DTOs unchanged; removing nulls centrally in
`PlexJsonOptions` or the DTOs; catching `NullReferenceException` in `GetOwnerAccountIdAsync`.

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| PlexToJellyfinSync.Data | `PlexAccountsContainer`, `PlexLibrariesContainer`, `PlexMetadataContainer`, `PlexMetadata`, `PlexMedia` | nullable list element types |
| PlexToJellyfinSync.Service | `PlexClient.cs` | null-element handling at the eight sites |
| PlexToJellyfinSync.Tests | `PlexClientTests.cs` | tests of *Tests per criterion* |

## Signatures (for the Dev's skeleton)

No skeleton: the tests call only `PlexClient`'s public members, whose signatures do not change. Done in step 6:

- `PlexAccountsContainer`: `public List<PlexAccount?>? Account { get; set; }`
- `PlexLibrariesContainer`: `public List<PlexDirectory?>? Directory { get; set; }`
- `PlexMetadataContainer`: `public List<PlexMetadata?>? Metadata { get; set; }`
- `PlexMetadata`: `public List<PlexTag?>? Genre { get; set; }`, `public List<PlexGuid?>? Guid { get; set; }`,
  `public List<PlexMedia?>? Media { get; set; }`
- `PlexMedia`: `public List<PlexPart?>? Part { get; set; }`
- `PlexClient` (private): `private static bool AppendHistoryPage(IReadOnlyList<PlexMetadata?> page, DateTimeOffset since, int accountId, List<PlexHistoryEntry> result)`,
  `private static string? FirstRatingKeyOf(List<PlexMetadata?> page)`,
  `private static (string? RatingKey, long? ViewedAt)? FirstHistoryPageKeyOf(List<PlexMetadata?> page)`,
  `private MediaItem? MapMediaItem(PlexMetadata? metadata)`

## Test files

`tests/PlexToJellyfinSync.Tests/PlexClientTests.cs` (existing).

Existing test code that calls a changed signature: none (fact 9).

Files of the change no unit test can cover: none.

## Areas

Sync pipeline (`docs/areas/sync-pipeline.md`): what a `null` element in a Plex list means.

## Documentation updates

Owner: Dev, all in `docs/areas/sync-pipeline.md`:

- *What is read from Plex*, new bullet after the rating-key bullet (line 99): "A `null` element in a list of a Plex
  answer — accounts, libraries, history entries, library or episode items, an item's genres, external ids, media
  or parts — is handled like an element of that list without any field: an account without a positive id, a
  library without a key, a history entry without viewed-at time and rating key, an item without a rating key
  (skipped with the warning above). For the paging rules it still counts as an entry of its page."
- *Owner only* (line 77): after "an answer that holds no account with a positive id" add "(a `null` element counts
  as an account without one)".
- *Related decisions*: add the link to `decisions/0024-null-elements-in-plex-lists.md` with "a `null` element of a
  Plex list is handled like an empty element".

Sweep: `docs/areas/*.md` and `docs/ARCHITECTURE.md` searched for enumerations this change extends (the Plex
endpoint list at `ARCHITECTURE.md` 126–129, the DTO list at 127–128, the *Limits and errors* failure list of
`sync-pipeline.md` 174–196): none changes — no endpoint or DTO is added, and the failure list names requests, not
element shapes. `README.md`: none.

## Architecture check

The guarantees of `.squad/project.md` are untouched: the owner rule (0002) gains a skipped element, not a new
resolution path; item isolation (0006) is unchanged, and fewer answers now fail a whole run. `Core ← Data ← Service`
references are unchanged; the Plex JSON shape still stops in `PlexClient`.

## Security considerations

- [ ] No limits: no byte or length limit changes.
- [ ] Exceptions reaching a user-visible message: the NRE text `Object reference not set to an instance of an
  object.` no longer reaches *Last error* for a `null` element; no new exception or text is introduced.

## Decision records

- `docs/decisions/0024-null-elements-in-plex-lists.md` (Proposed)

## Out of scope / follow-ups

- `GetMediaItemAsync` with `Metadata: [null, {…}]` keeps taking the first element (returns `null`): a metadata
  request answers one item, so the second element is not looked for.
- `Media: [null, {…}]` keeps the first media (no file path), as for `{}`.

## Challenge notes

- minor: Fact 6 says a `null` element in `Media` or `Part` does not throw, but no test checks it, even though the plan changes both DTO element types and the area document will promise that behaviour ("…media or parts — is handled like an element of that list without any field"). Instruction for the Tester: in `PlexClientGetMediaItemSkipsNullGenreAndGuidElements` (or a sibling test), also serve a movie with `"Media":[null]` and one with `"Media":[{"Part":[null]}]`. Assert that `GetMediaItemAsync` returns the item with `FilePath` null and does not throw, so the documented `Media`/`Part` rule is covered.

## Security notes

- Non-blocking: library paging has no stop for a repeated full page that starts with a null element. Where: `GetPagedMediaItemsAsync`, `PlexClient.cs` 429–436; the non-advancing check is `if (firstRatingKey is not null && firstRatingKey == previousFirstRatingKey)`. After the change a server that keeps returning the same full 200-entry page whose first element is `null` pages forever (one HTTP request and one warning per null element per round, until cancellation). Today the null case ends with the exception; after the change it behaves exactly like a first element `{}` (no `ratingKey`), which already loops the same way on `origin/main`. It gives a hostile or broken server no new capability. Recommendation: open a follow-up issue for the unkeyed-first-entry gap in library paging (covers `{}` and `null` alike); optionally name it in *Out of scope / follow-ups*.
