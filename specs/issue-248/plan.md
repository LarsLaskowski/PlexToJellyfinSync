# Plan: Library and episode paging does not stop on a repeated page with an unkeyed first entry

Source: Issue #248
Status: Draft
Tier: standard — the stop condition of `GetPagedMediaItemsAsync` changes (control flow and behavior), and the guard it changes is a robustness check against a misbehaving server, not a security control: a hostile server that varies the first rating key per page passes it today and after this change alike.
Security areas touched: HTTP calls to Plex (`PlexClient`, untrusted JSON)

## Problem / root cause

`PlexClient.GetPagedMediaItemsAsync` (`src/PlexToJellyfinSync.Service/PlexClient.cs:432`) takes a page as a
repeat only when `firstRatingKey is not null && firstRatingKey == previousFirstRatingKey`. `FirstRatingKeyOf`
(`PlexClient.cs:277-280`) returns `null` both for a first entry without `ratingKey` (`{}`) and for a `null`
element, so the `is not null` half of the condition is false for such a page, and a server that keeps answering
every `X-Plex-Container-Start` with the same full page (200 entries, `LibraryPageSize`, `PlexClient.cs:52`) is
asked for the next page forever (`start += LibraryPageSize`, `PlexClient.cs:451`). The `is not null` half also
serves as "there is a previous page" on the first round, which is why it cannot simply be dropped: a first page
led by an unkeyed entry would then compare equal to the initial `null` and stop at once.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | The library/episode repeat check requires a non-null first rating key | `PlexClient.cs:432` | confirmed |
| 2 | `{}` and a `null` first element both yield a `null` first key | `FirstRatingKeyOf`, `PlexClient.cs:277-280` (`page[0]?.RatingKey`) | confirmed |
| 3 | Each repeated round costs one HTTP request and one warning per unkeyed element | loop `PlexClient.cs:419-452`; `MapMediaItem` logs `Skipping a Plex item with a missing or invalid rating key` for a `null` or unkeyed element, `PlexClient.cs:329-334` | confirmed |
| 4 | History paging stops on a repeated page led by an unkeyed or `null` entry | `FirstHistoryPageKeyOf` returns a non-null tuple `(null, null)`, compared at `PlexClient.cs:569`; tests `PlexClientGetHistorySinceWithUnkeyedFirstEntryStopsOnNonAdvancingPage` and `...WithNullFirstEntry...` (`PlexClientTests.cs:665`, `:717`) | confirmed |
| 5 | Library contents and a show's episodes share the one paging method | `GetEpisodesAsync` / `GetLibraryItemsAsync`, `PlexClient.cs:608-617` | confirmed |
| 6 | The area document describes today's gap as behavior | `docs/areas/sync-pipeline.md:197-198` ("is never taken as a repeat") | confirmed |
| 7 | Record 0024 (null elements, consequence on "first entry of a page") is unreleased | `git ls-remote --tags origin`: latest `v1.5.0` = 487c34b; `git merge-base --is-ancestor 9905538 487c34b` false | confirmed |
| 8 | A test that bounds the repeat with `ResponseSequences` fails instead of hanging on today's code | `StubHttpMessageHandler.SendAsync`: an exhausted sequence without a `Responses` entry answers 404, so `EnsureSuccessStatusCode` (`PlexClient.cs:471`) throws | confirmed |
| 9 | No other document enumerates the non-advancing behavior | `grep -i advanc` over `README.md`, `docs/ARCHITECTURE.md`, `docs/areas/`: only `sync-pipeline.md:195-198` | confirmed |

All claims of the issue are confirmed (rows 1–5); none refuted. No related defect found.

## Acceptance criteria

- [ ] AC1: When `/library/sections/{key}/all` answers every request with the same full page (200 entries) whose
  first element is an entry without `ratingKey` or a JSON `null`, `GetLibraryItemsAsync` sends exactly two
  requests, returns the 199 keyed items of the first page once, and logs exactly two warnings, in this order:
  `Skipping a Plex item with a missing or invalid rating key` (from `MapMediaItem`, first page) and
  `Plex server returned a non-advancing page for /library/sections/1/all, stopping pagination` (the repeat branch
  of `GetPagedMediaItemsAsync`).
- [ ] AC2: The same holds for a show's episodes: `/library/metadata/{key}/allLeaves` repeating such a page makes
  `GetEpisodesAsync` send exactly two requests and return the 199 keyed episodes once.
- [ ] AC3: A genuinely different page is not taken as a repeat because its first entry is unkeyed: a full page led
  by a keyed entry, then a different full page led by an unkeyed entry, then a short page led by a keyed entry
  are all requested (three requests) and all keyed items of the three pages are returned.

## Tests per criterion

All in `tests/PlexToJellyfinSync.Tests/PlexClientTests.cs`. The repeated page is served through
`handler.ResponseSequences[path]` as a queue of **three** identical copies with no `Responses` entry, so today's
code fails on the 404 of the fourth request instead of looping (fact 8).

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | `PlexClientGetLibraryItemsWithUnkeyedFirstEntryStopsOnNonAdvancingPage(string firstEntry)` with `[DataRow("""{ "type": "movie" }""")]` and `[DataRow("null")]`; 199 further entries `{ "ratingKey": "{{1000 + i}}", "type": "movie" }`; `RecordingPlexClientLogger`; asserts request count 2, item count 199, the two warning texts of AC1 in order | `tests/PlexToJellyfinSync.Tests/PlexClientTests.cs` |
| AC2 | `PlexClientGetEpisodesWithUnkeyedFirstEntryStopsOnNonAdvancingPage(string firstEntry)` with `[DataRow("""{ "type": "episode" }""")]` and `[DataRow("null")]`, path `/library/metadata/500/allLeaves`, `GetEpisodesAsync("500", …)`; asserts request count 2, item count 199 | `tests/PlexToJellyfinSync.Tests/PlexClientTests.cs` |
| AC3 | `PlexClientGetLibraryItemsWithUnkeyedFirstEntryOnNextPageKeepsPaging`: sequence of page 1 (200 keyed, `1000…1199`), page 2 (`{ "type": "movie" }` + 199 keyed `2000…2198`), page 3 (two keyed `3000`, `3001`); asserts 3 requests and 401 items. It passes on today's code (it guards the fix against stopping on any unkeyed first entry), so it is deferred to step 6 | `tests/PlexToJellyfinSync.Tests/PlexClientTests.cs` |

The existing `PlexClientGetLibraryItemsStopsOnNonAdvancingPage` (keyed repeat) and the history paging tests stay
unchanged and must stay green.

## Approach

In `GetPagedMediaItemsAsync` only, separate "is there a previous page" from the key comparison, as history paging
does with its nullable tuple:

```csharp
var start = 0;
var hasPreviousPage = false;
string? previousFirstRatingKey = null;
...
var firstRatingKey = FirstRatingKeyOf(entries);

if (hasPreviousPage && firstRatingKey == previousFirstRatingKey)
{
    _logger.LogWarning("Plex server returned a non-advancing page for {BaseUrl}, stopping pagination", baseUrl);

    yield break;
}

hasPreviousPage = true;
previousFirstRatingKey = firstRatingKey;
```

Two consecutive pages whose first entries are both unkeyed or `null` therefore count as a repeat, exactly as for
history. The warning text is unchanged; no new user-visible output. `FirstRatingKeyOf` and its documentation stay
as they are.

## Options considered

1. **A "has previous page" flag and plain equality of the first key, `null` included** (chosen) — the smallest
   change, the same rule history paging already applies (`PlexClient.cs:569`), and it stops the loop the issue
   names. Cost: two genuinely different consecutive pages that both start with an unkeyed entry end paging early;
   accepted as for history (record 0024).
2. **Compare the rating keys of the whole page instead of the first entry** — tells such pages apart, but builds a
   key list per page, still sees two pages without any keyed entry as equal, and would diverge from history
   paging, which then needs the same change to stay consistent. Lost on cost and scope.
3. **Compare the first *keyed* entry of the page** — a page with no keyed entry at all would still loop, so it
   needs option 1 as a fallback anyway; two rules for one guard. Lost.

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| `PlexToJellyfinSync.Service` | `PlexClient.GetPagedMediaItemsAsync` (`src/PlexToJellyfinSync.Service/PlexClient.cs`) | stop condition as in *Approach* (Dev) |
| `PlexToJellyfinSync.Tests` | `PlexClientTests` | three tests of *Tests per criterion* (Tester) |

## Signatures (for the Dev's skeleton)

none — no new or changed member; step 4 is not needed.

## Test files

`tests/PlexToJellyfinSync.Tests/PlexClientTests.cs` (existing). Existing test code that calls a changed signature:
none. Files of the change no unit test can cover: none.

## Areas

`docs/areas/sync-pipeline.md` (*Plex paging that does not advance*): the gap sentence is replaced by the new rule.

## Documentation updates

Owner: Dev. In `docs/areas/sync-pipeline.md`, bullet *Plex paging that does not advance* (lines 195-198), replace
the last sentence ("For library contents and a show's episodes, a first entry without a rating key (or a `null`
one) is never taken as a repeat, so such pages do not stop paging.") with:

> A first entry without a rating key, or a `null` one, counts as the same first entry as another such entry, so a
> repeated page that starts with one stops paging too — and so does a different page that starts with one right
> after a page that also did.

Sweep (fact 9): no further hit in `README.md`, `docs/ARCHITECTURE.md` or the other area documents.

## Architecture check

No guarantee of `docs/ARCHITECTURE.md` / `.squad/project.md` is touched: the reconcile still processes the items
of the pages read so far, the worker still never stops on an error, and nothing changes in the files written.
The change only ends a loop that previously ran until cancellation.

## Security considerations

- [ ] No limits: no byte or length limit on parsed input is added or changed.
- [ ] No exception reaches a user-visible message: none new; the stop is a `yield break` with the existing
  warning, whose only external value is the request path built from a validated library or show key.

## Decision records

- `docs/decisions/0024-null-elements-in-plex-lists.md` (Accepted, unreleased, edited in place by the Lead): its
  consequence on the paging rules now says that an unkeyed or `null` first entry counts as a repeat for library
  contents and episodes as for history, with the accepted early stop and option 2 as the rejected alternative.
  Index row unchanged.

## Out of scope / follow-ups

- Bounding the number of pages a run may request (a hostile server can vary the first key per page): not part of
  this issue, no follow-up proposed.