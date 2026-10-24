# Plan: History sync filters by library and media kind

Source: Issue #233
Status: Draft
Tier: standard — the history run changes behavior (it now skips entries and items), and no security control is created, changed or removed.
Security areas touched: HTTP calls to Plex (`PlexClient`, `PlexMetadata`: one new field read from untrusted JSON); Logging of external data (two new debug lines carrying a Plex rating key and section key); build configuration (`tests/PlexToJellyfinSync.Tests/PlexToJellyfinSync.Tests.csproj` gains the test-data items)

## Problem / root cause

`SyncOrchestrator.ProcessHistoryEntriesAsync` (`src/PlexToJellyfinSync.Service/SyncOrchestrator.cs:149-174`)
fetches and writes every history entry, and `ProcessRatingKeyAsync` (lines 125-139) counts and hands every item
Plex returns to `IMediaItemWriter.WriteItemAsync` (line 136). Neither reads `Plex:Libraries`, which only
`ReconcileAsync` applies (lines 301-303), and neither checks the item kind. `PlexClient.MapKind`
(`src/PlexToJellyfinSync.Service/PlexClient.cs:114-117`) maps every type other than movie, episode, season and
show to `MediaKind.Unknown`, and `NfoWriter` writes that kind as `<file>.nfo` (`ResolveTargetPath` default,
`src/PlexToJellyfinSync.Service/NfoWriter.cs:297-300`) with a `movie` root (`GetRootName` default, lines 84-87).
The history DTO does not read `librarySectionID` at all (`src/PlexToJellyfinSync.Data/Plex/PlexMetadata.cs`),
so the run has nothing to filter on.

Related defects with the same cause, fixed by this change: an episode of an excluded library adds its show to
the affected shows (`SyncOrchestrator.cs:162-165`), so that show's aggregates are written; and the history run
counts non-video items in "Items processed", which `docs/areas/sync-pipeline.md:157` says counts movies and
episodes only.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | `Plex:Libraries` filters only the reconcile | `SyncOrchestrator.cs:301-303`; no other read of `_plexOptions` | confirmed |
| 2 | The history request sends no section filter | `PlexClient.cs:565` (URL with `sort`, `accountID`, paging only) | confirmed |
| 3 | The history run does not filter by kind | `SyncOrchestrator.cs:125-139`, `160` | confirmed |
| 4 | An unknown kind gets `<file>.nfo` with a `movie` root | `NfoWriter.cs:84-87`, `297-300`; `docs/areas/path-mapping-and-nfo.md:75` | confirmed |
| 5 | A history entry carries `librarySectionID` as a JSON string | `specs/issue-233/samples/history.json` (`"1"`, `"7"`), real sample supplied by the Product Manager | confirmed |
| 6 | `/library/metadata/{key}` carries `librarySectionID` as a JSON number on each `Metadata` element | `specs/issue-233/samples/metadata-movie.json` (`1`), `metadata-episode.json` (`7`) | confirmed |
| 7 | `PlexJsonOptions.Default` reads numbers from JSON strings, so an `int?` property accepts both forms | to verify: Tester, steps 5-6, the tests of AC1 and AC5 read the two real forms (option `JsonNumberHandling.AllowReadingFromString` in `src/PlexToJellyfinSync.Service/PlexJsonOptions.cs`) | unverified |
| 8 | A `string?` property would make every metadata response (number form) fail to deserialize | to verify: Tester, step 6, the AC5 tests fail if the field is typed `string` (System.Text.Json reads no JSON number into a string) | unverified |
| 9 | A non-numeric `librarySectionID` string fails the page with `JsonException`, as a non-numeric `accountID` does | to verify: Tester, step 6, the AC6 test (same option set; `PlexMetadata.AccountId` is `int?` today) | unverified |
| 10 | Section keys from `/library/sections` are the decimal section ids (`"key": "1"`) that `Plex:Libraries` lists | `PlexClient.cs:546-553`; `docs/areas/configuration.md:35` ("compared exactly") | confirmed |
| 11 | No existing history test sets `Plex:Libraries`, and `AddHistory` leaves the new field `null` | `tests/PlexToJellyfinSync.Tests/SyncOrchestratorTests.cs:682` (reconcile only), `1700-1708` | confirmed |
| 12 | The samples hold no real private value | to verify: orchestrator, before step 4, compares every field of the three samples against the real values, prefixes included (it anonymized them and holds the real values; titles, paths, guids and UUIDs read as synthetic) | unverified |

## Acceptance criteria

- [ ] AC1: `GetHistorySinceAsync` sets `PlexHistoryEntry.LibrarySectionKey` to the entry's `librarySectionID` as
  an invariant decimal string (`"1"`, `"7"` for the real sample), and to `null` when the entry has none.
- [ ] AC2: With a non-empty `Plex:Libraries`, a history entry whose `LibrarySectionKey` is not listed (compared
  exactly, ordinal) or is `null` is not requested from Plex, not written, not counted in "Items processed", and
  adds no show to the aggregates; the high-water mark still advances past it.
- [ ] AC3: With an empty `Plex:Libraries`, every entry is processed as today, an entry without a library
  included.
- [ ] AC4: An item read for a history entry whose kind is neither `Movie` nor `Episode` (`Unknown`, `Season`,
  `Series`) is not written and not counted; the high-water mark still advances past it.
- [ ] AC5: The real metadata responses (number form of `librarySectionID`) still map to a movie and an episode
  through `GetMediaItemAsync`, and the DTO reads `LibrarySectionId` 1 and 7 from them.
- [ ] AC6: A history page whose `librarySectionID` is not a number fails `GetHistorySinceAsync` with
  `JsonException` (the run fails as for any failed history request; the mark stays).

## Tests per criterion

All new test methods read the fixtures under `tests/PlexToJellyfinSync.Tests/TestData/Plex/` (see *Test
files*) where they need Plex JSON; AC2-AC4 drive the real orchestrator over `FakePlexClient` as the class does.

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | `PlexClientGetHistorySinceSampleMapsLibrarySectionKey` (reads `history.json`, since `UnixEpoch`, account 1: entries `1001`→`"1"`, `2003`→`"7"`); `PlexClientGetHistorySinceEntryWithoutLibrarySectionHasNullKey` (inline JSON, the existing `HistoryJson` shape) | `tests/PlexToJellyfinSync.Tests/PlexClientTests.cs` |
| AC2 | `SyncOrchestratorProcessHistoryEntryOutsideConfiguredLibrariesIsSkipped` (`Libraries = ["1"]`, movie `m1` in `"1"` at +10 min, movie `m2` in `"2"` at +30 min: one write of `m1`, `MediaItemRequests` = `["m1"]`, items processed 1, mark = +30 min); `SyncOrchestratorProcessHistoryEntryWithoutLibraryIsSkippedWhenLibrariesConfigured` (`Libraries = ["1"]`, entry with `null` key: no request, no write, mark advanced); `SyncOrchestratorProcessHistoryEpisodeOutsideConfiguredLibrariesWritesNoAggregates` (`Libraries = ["1"]`, episode in `"7"`: no season or series write, `EpisodeRequests` empty) | `tests/PlexToJellyfinSync.Tests/SyncOrchestratorTests.cs` |
| AC3 | existing `SyncOrchestratorProcessHistoryWritesMovieAndAdvancesWatermark` and `SyncOrchestratorProcessHistoryEpisodeWritesSeasonAndSeriesAggregates` (empty filter, `null` key) stay green unchanged | `tests/PlexToJellyfinSync.Tests/SyncOrchestratorTests.cs` |
| AC4 | `SyncOrchestratorProcessHistoryItemOfOtherKindIsSkipped` with `[DataRow(MediaKind.Unknown)]`, `[DataRow(MediaKind.Season)]`, `[DataRow(MediaKind.Series)]` (mapped file path: no write, items processed 0, mark advanced) | `tests/PlexToJellyfinSync.Tests/SyncOrchestratorTests.cs` |
| AC5 | `PlexClientGetMediaItemSampleMapsKindAndFilePath` with `[DataRow("metadata-movie.json", "1001", MediaKind.Movie)]`, `[DataRow("metadata-episode.json", "2003", MediaKind.Episode)]`; `PlexMetadataResponseSampleReadsNumericLibrarySectionId` with `[DataRow("metadata-movie.json", 1)]`, `[DataRow("metadata-episode.json", 7)]` — both deferred to step 6: they pass on the skeleton, whose DTO property is the whole change | `tests/PlexToJellyfinSync.Tests/PlexClientTests.cs`, `tests/PlexToJellyfinSync.Tests/PlexMetadataDeserializationTests.cs` |
| AC6 | `PlexClientGetHistorySinceNonNumericLibrarySectionThrowsJsonException` (inline JSON, `"librarySectionID": "x"`) — deferred to step 6: it passes on the skeleton | `tests/PlexToJellyfinSync.Tests/PlexClientTests.cs` |

## Approach

1. **DTO** — `PlexMetadata` gets `LibrarySectionId` (`int?`, JSON name `librarySectionID`). Not `string?`: the
   metadata endpoint sends a number (fact 6, 8).
2. **Mapping** — `PlexClient.AppendHistoryPage` sets `LibrarySectionKey =
   entry.LibrarySectionId?.ToString(CultureInfo.InvariantCulture)` on each `PlexHistoryEntry` it adds. No other
   mapping changes; `MediaItem` gets no new field.
3. **Library filter** — `SyncOrchestrator.ProcessHistoryEntriesAsync` keeps the `maxViewed` update for every
   entry and calls `TryProcessRatingKeyAsync` only when `IsInConfiguredLibraries(entry)` holds:
   `_plexOptions.Libraries.Length == 0 || (entry.LibrarySectionKey is not null &&
   _plexOptions.Libraries.Contains(entry.LibrarySectionKey, StringComparer.Ordinal))`. The reconcile's filter
   stays as it is.
4. **Kind filter** — `ProcessRatingKeyAsync`, after the `item is null` check and before `ItemsProcessed++`,
   returns `null` for an item whose `Kind` is neither `MediaKind.Movie` nor `MediaKind.Episode`, so the item is
   neither counted, written nor added to the affected shows.
5. **Logs** — both skips log at `Debug` (one line per skipped play would be noise at `Information` on every
   poll), with these exact templates:
   - `History entry {RatingKey} belongs to library {LibrarySectionKey}, which Plex:Libraries does not list, skipping`
     (a `null` key is logged as is);
   - `Item {RatingKey} is neither a movie nor an episode, skipping`.
   Tests do not assert them (`NullLogger`). No other user-visible output changes.

## Options considered

- **Section filter in the history request (`librarySectionID` query parameter)** — lost: one section per request
  means one paged history loop per library and a merge of their order; the server behavior is not verified; the
  client check would still be needed for an entry without a section.
- **Library filter on the fetched item** — lost: needs a `MediaItem` field and keeps one item request per
  excluded play, while the entry's own field (fact 5) allows skipping before the request. Reassessed with the
  samples: the entry-based filter is now the better one.
- **Kind filter on the history entry's `type`** — lost: saves one request per non-video play only, duplicates
  `MapKind`, and checks a value other than the one that decides the NFO root; the Product Manager called
  non-video plays irrelevant for this server.
- **Kind guard in `MediaItemWriter` or a rejecting `NfoWriter`** — lost: leaves "Items processed" counting the
  skipped item, and the reconcile never passes such an item.

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| `PlexToJellyfinSync.Data` | `Plex/PlexMetadata.cs` | new property `LibrarySectionId` |
| `PlexToJellyfinSync.Core` | `Models/PlexHistoryEntry.cs` | new property `LibrarySectionKey` |
| `PlexToJellyfinSync.Service` | `PlexClient.cs` | `AppendHistoryPage` maps the section key |
| `PlexToJellyfinSync.Service` | `SyncOrchestrator.cs` | library skip in `ProcessHistoryEntriesAsync`, kind skip in `ProcessRatingKeyAsync`, new `IsInConfiguredLibraries` |
| `PlexToJellyfinSync.Tests` | `PlexToJellyfinSync.Tests.csproj`, `TestData/Plex/*.json` | test data copied to the output directory |

## Signatures (for the Dev's skeleton)

```csharp
// PlexToJellyfinSync.Data.Plex.PlexMetadata, region Properties
/// <summary>
/// Library section id; a JSON string in history entries, a JSON number in metadata responses
/// </summary>
[JsonPropertyName("librarySectionID")]
public int? LibrarySectionId { get; set; }

// PlexToJellyfinSync.Core.Models.PlexHistoryEntry, region Properties
/// <summary>
/// Key of the library section the watched item belongs to, or <c>null</c> if Plex reported none
/// </summary>
public string? LibrarySectionKey { get; set; }

// PlexToJellyfinSync.Service.SyncOrchestrator, region Methods
/// <summary>
/// Check whether a history entry belongs to a library listed in <c>Plex:Libraries</c>; an empty list admits every entry
/// </summary>
/// <param name="entry">History entry</param>
/// <returns><c>true</c> if the entry is to be processed</returns>
private bool IsInConfiguredLibraries(PlexHistoryEntry entry)
```

The skeleton changes no existing signature and rewrites no existing file beyond adding these members.

## Test files

- `tests/PlexToJellyfinSync.Tests/PlexClientTests.cs`, `tests/PlexToJellyfinSync.Tests/SyncOrchestratorTests.cs`,
  `tests/PlexToJellyfinSync.Tests/PlexMetadataDeserializationTests.cs` (existing classes).
- Fixtures: the Tester copies `specs/issue-233/samples/history.json`, `metadata-movie.json` and
  `metadata-episode.json` byte for byte to `tests/PlexToJellyfinSync.Tests/TestData/Plex/` (the work folder is
  removed before the PR) and adds to the test project
  `<ItemGroup><None Include="TestData\**\*.json" CopyToOutputDirectory="PreserveNewest" /></ItemGroup>`; tests
  read them from `Path.Combine(AppContext.BaseDirectory, "TestData", "Plex", <name>)` with
  `TestContext.CancellationToken`. A small shared reader may live in the test classes; no new test double.
- Variants the samples cover: movie and episode history entries (string section id, entry with `accountID`),
  movie and episode metadata (number section id on container and item). Not covered: a non-video item (none on
  the server; AC4 uses `FakePlexClient` items), an entry without a section and a non-numeric one (inline JSON,
  AC1 and AC6).
- Existing test call sites of a changed signature: none (new properties only). `SyncOrchestratorTests.AddHistory`
  gets an optional `librarySectionKey` parameter or an overload, written by the Tester.
- Files no unit test can cover: none.

## Areas

- `docs/areas/sync-pipeline.md` — *Incremental history run*: the library skip (before the item is read, an entry
  without a library included while the filter is set) and the kind skip (only movies and episodes are written
  and counted), both advancing the mark; *Full reconcile* line 51-52: the filter now applies to both runs (move
  the sentence or point to the history bullet); *Limits and errors*: the bullet "History and `Plex:Libraries`"
  (lines 180-182) is replaced by the case of an entry without a library and a non-numeric library id (the run
  fails, AC6).
- `docs/areas/path-mapping-and-nfo.md:75` — keep the writer's rule, add that neither run hands such an item to
  the writer (link to the sync pipeline).

## Documentation updates

| File | Edit | Owner |
| ---- | ---- | ----- |
| `docs/areas/sync-pipeline.md` | as under *Areas* | Dev |
| `docs/areas/path-mapping-and-nfo.md` | as under *Areas* | Dev |
| `docs/ARCHITECTURE.md` lines 87-91 | `ProcessHistoryAsync` skips entries outside `Plex:Libraries` and items that are neither movies nor episodes; link decision 0023 | Dev |
| `.squad/stack.md` *Layout* | add the test-data layout: `tests/PlexToJellyfinSync.Tests/TestData/<source>/`, copied to the output directory | Tester |
| `docs/UNIT_TESTS.md` line 39 | "filesystem fixtures beyond a temp directory" becomes "beyond a temp directory and the JSON samples under `TestData/`" | Tester |

Sweep done: `README.md:95` and `docs/areas/configuration.md:35` ("Restrict to library section keys", "compared
exactly") stay true; `docs/decisions/0018` line 61 (the orchestrator owns the filter) stays true; the
`ARCHITECTURE.md` endpoint list does not change.

## Architecture check

No guarantee in `.squad/project.md` is weakened: the mark is still seeded on the first run and advances past
every entry read (0005), failures stay isolated per item (0006), owner-only and polling-only are untouched, and
path mapping and the NFO writer do not change. The orchestrator keeps owning the `Plex:Libraries` filter (0018);
`PlexClient` still keeps Plex's JSON shape behind `PlexHistoryEntry`.

## Security considerations

- [ ] No limits: the new field is an `int?`; no byte or length limit is added.
- [ ] Exceptions reaching a user-visible message: a `JsonException` from a malformed `librarySectionID` reaches
  `HandleError` like any failed history request and becomes the dashboard's last error (its message names the
  JSON path, no secret); no new text.
- [ ] The library skip fails closed: with a non-empty filter an entry without a library is skipped.
- [ ] The new log lines carry only a rating key and a section key, both from Plex, no token or URL.

## Decision records

- `docs/decisions/0023-history-run-filters-by-library-and-kind.md` (Proposed), indexed in
  `docs/decisions/README.md`.

## Out of scope / follow-ups

- Skipping non-video plays before their item request (option on the entry's `type`): not needed now.
- The *Image build check* is required by `.squad/stack.md` for every `.csproj` change; the test project is not
  part of the image, the Dev runs the check once in step 6 anyway.

## Challenge notes

Minor objections of the Devil's Advocate (plan challenge, verbatim):

1. minor — The new error case is documented too narrowly. `PlexMetadata` is shared by three response types: history pages (`PlexClient.cs:566`), paged library listings (`GetPagedMediaItemsAsync`, `PlexClient.cs:433`) and `/library/metadata/{key}` (`PlexClient.cs:603`). So a non-numeric `librarySectionID` now fails any of them, not just the history page. Instruction to the Dev: in the *Limits and errors* bullet that replaces lines 180-182 of `docs/areas/sync-pipeline.md`, and in the *Consequences* of decision 0023, say that a non-numeric `librarySectionID` fails whatever Plex response carries it: a history page fails the run (the mark stays); a library page fails the reconcile of that library; a metadata response is a failed item. Why minor: the code change and AC6 are right, only the description is too narrow.

2. minor — The plan does not say what happens to data the old behaviour already left on disk. Earlier history runs wrote `<file>.nfo` with a `movie` root next to non-video files under mapped paths. With the kind skip those files are no longer updated, and nothing removes them. Instruction to the Dev: add one sentence to the history bullet of `docs/areas/sync-pipeline.md` (or to `docs/areas/path-mapping-and-nfo.md:75`) saying that NFO files written for such items before this change are left untouched and can be deleted by hand. Do not add any cleanup code, because the issue does not ask for it. Why minor: it is a documentation sentence, and the plan needs no other change.

## Security notes

Non-blocking findings of Security's plan review (APPROVED, verbatim summary):

- Fail-closed holds; `"01"` and `"+1"` normalize to `"1"` (same numeric section), no change needed.
- The library-skip Debug line logs `entry.RatingKey` unvalidated, as the existing `LogError` at `SyncOrchestrator.cs:111` already does; no new exposure, general log hardening is a separate issue.
- A non-numeric `librarySectionID` fails the whole history page (the mark stays); accepted as designed, matches `accountID` behavior, stated in the decision record.
- Fact 12: the numeric fields of the samples (`lastViewedAt`, `addedAt`, `updatedAt`, `size`, `deviceID`) were replaced with synthetic values by the orchestrator before step 4.
- The test `.csproj` item affects only the test output; the image build check still runs once in step 6.
