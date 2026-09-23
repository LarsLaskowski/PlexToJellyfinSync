using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using PlexToJellyfinSync.Core.Enums;
using PlexToJellyfinSync.Core.Options;
using PlexToJellyfinSync.Service;

namespace PlexToJellyfinSync.Tests;

/// <summary>
/// Tests for <see cref="PlexClient"/>
/// </summary>
[TestClass]
public sealed class PlexClientTests
{
    #region Constants

    private const string AccountsJson = """
                                        {
                                          "MediaContainer": {
                                            "Account": [
                                              { "id": 0, "name": "" },
                                              { "id": 9, "name": "guest" },
                                              { "id": 4, "name": "owner" }
                                            ]
                                          }
                                        }
                                        """;

    private const string LibrariesJson = """
                                         {
                                           "MediaContainer": {
                                             "Directory": [
                                               { "key": "1", "title": "Movies", "type": "movie" },
                                               { "key": "2", "title": "Shows", "type": "show" },
                                               { "key": "3", "title": "Music", "type": "artist" },
                                               { "title": "Broken" }
                                             ]
                                           }
                                         }
                                         """;

    private const string HistoryJson = """
                                       {
                                         "MediaContainer": {
                                           "Metadata": [
                                             { "ratingKey": "10", "viewedAt": 1767225600, "accountID": 4 },
                                             { "ratingKey": "11", "viewedAt": 1767139200, "accountID": 4 },
                                             { "ratingKey": "12", "viewedAt": 1767225600, "accountID": 8 },
                                             { "ratingKey": "13", "viewedAt": 1767312000 },
                                             { "ratingKey": "14", "accountID": 4 },
                                             { "viewedAt": 1767312000, "accountID": 4 }
                                           ]
                                         }
                                       }
                                       """;

    private const string MovieJson = """
                                     {
                                       "MediaContainer": {
                                         "Metadata": [
                                           {
                                             "ratingKey": "12345",
                                             "type": "movie",
                                             "title": "Heat",
                                             "titleSort": "Heat",
                                             "originalTitle": "Heat",
                                             "year": 1995,
                                             "summary": "A crew of thieves",
                                             "studio": "Warner Bros.",
                                             "duration": 10260000,
                                             "originallyAvailableAt": "1995-12-15",
                                             "addedAt": 1600000000,
                                             "Genre": [ { "tag": "Crime" }, { "tag": "Drama" }, { } ],
                                             "Guid": [ { "id": "imdb://tt0113277?lang=en" }, { "id": "tmdb://949" }, { "id": "broken" } ],
                                             "Media": [ { "Part": [ { "file": "/data/Movies/Heat (1995)/Heat.mkv" } ] } ],
                                             "viewCount": 2,
                                             "lastViewedAt": 1700000000
                                           }
                                         ]
                                       }
                                     }
                                     """;

    private const string EpisodesJson = """
                                        {
                                          "MediaContainer": {
                                            "Metadata": [
                                              {
                                                "ratingKey": "501",
                                                "type": "episode",
                                                "title": "Pilot",
                                                "index": 1,
                                                "parentIndex": 2,
                                                "grandparentTitle": "Breaking Bad",
                                                "grandparentRatingKey": "500",
                                                "Media": [ { "Part": [ { "file": "/data/Shows/Breaking Bad/Season 02/S02E01.mkv" } ] } ]
                                              }
                                            ]
                                          }
                                        }
                                        """;

    private const string EmptyContainerJson = """
                                              {
                                                "MediaContainer": { }
                                              }
                                              """;

    private const string InvalidFilePathJson = """
                                               {
                                                 "MediaContainer": {
                                                   "Metadata": [
                                                     {
                                                       "ratingKey": "12345",
                                                       "type": "movie",
                                                       "title": "Heat",
                                                       "Media": [ { "Part": [ { "file": "/data/Movies/Heat\u0000.mkv" } ] } ]
                                                     }
                                                   ]
                                                 }
                                               }
                                               """;

    private const string InvalidRatingKeyJson = """
                                                {
                                                  "MediaContainer": {
                                                    "Metadata": [
                                                      {
                                                        "ratingKey": "12\n345",
                                                        "type": "movie",
                                                        "title": "Heat"
                                                      }
                                                    ]
                                                  }
                                                }
                                                """;

    #endregion // Constants

    #region Methods

    /// <summary>
    /// A configured owner account id is used without contacting Plex
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task PlexClientConfiguredOwnerAccountIdSkipsRequest()
    {
        using var handler = new StubHttpMessageHandler();
        using var httpClient = CreateHttpClient(handler);

        var client = CreateClient(httpClient,
                                  new PlexOptions
                                  {
                                      OwnerAccountId = 42
                                  });

        var ownerId = await client.GetOwnerAccountIdAsync(CancellationToken.None);

        Assert.AreEqual(42, ownerId, "The configured owner account id should be used!");
        Assert.IsEmpty(handler.Requests, "A configured owner account id should not trigger a request!");
    }

    /// <summary>
    /// Without configuration the lowest positive account id is detected as owner
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task PlexClientDetectsLowestPositiveAccountIdAsOwner()
    {
        using var handler = new StubHttpMessageHandler();

        handler.Responses["/accounts"] = AccountsJson;

        using var httpClient = CreateHttpClient(handler);

        var client = CreateClient(httpClient);

        var ownerId = await client.GetOwnerAccountIdAsync(CancellationToken.None);

        Assert.AreEqual(4, ownerId, "The lowest positive account id should be detected as owner!");
    }

    /// <summary>
    /// A failing account request falls back to the default owner account id
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task PlexClientFailingAccountRequestFallsBackToDefaultOwner()
    {
        using var handler = new StubHttpMessageHandler();
        using var httpClient = CreateHttpClient(handler);

        var client = CreateClient(httpClient);

        var ownerId = await client.GetOwnerAccountIdAsync(CancellationToken.None);

        Assert.AreEqual(1, ownerId, "A failing account request should fall back to account id 1!");
    }

    /// <summary>
    /// Library sections are mapped to their media kind and entries without key are dropped
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task PlexClientGetLibrariesMapsKinds()
    {
        using var handler = new StubHttpMessageHandler();

        handler.Responses["/library/sections"] = LibrariesJson;

        using var httpClient = CreateHttpClient(handler);

        var client = CreateClient(httpClient);

        var libraries = await client.GetLibrariesAsync(CancellationToken.None);

        Assert.HasCount(3, libraries, "Only sections carrying a key should be reported!");
        Assert.AreEqual(MediaKind.Movie, libraries[0].Kind, "A movie section should be mapped to the movie kind!");
        Assert.AreEqual("Movies", libraries[0].Title, "The section title should be mapped!");
        Assert.AreEqual(MediaKind.Series, libraries[1].Kind, "A show section should be mapped to the series kind!");
        Assert.AreEqual(MediaKind.Unknown, libraries[2].Kind, "An unsupported section type should be mapped to the unknown kind!");
    }

    /// <summary>
    /// A missing library container is reported as an empty list
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task PlexClientGetLibrariesWithoutDirectoryReturnsEmpty()
    {
        using var handler = new StubHttpMessageHandler();

        handler.Responses["/library/sections"] = EmptyContainerJson;

        using var httpClient = CreateHttpClient(handler);

        var client = CreateClient(httpClient);

        var libraries = await client.GetLibrariesAsync(CancellationToken.None);

        Assert.IsEmpty(libraries, "An empty container should be reported as an empty list!");
    }

    /// <summary>
    /// History entries are filtered by timestamp and account and ordered ascending
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task PlexClientGetHistorySinceFiltersAndOrdersEntries()
    {
        using var handler = new StubHttpMessageHandler();

        handler.Responses["/status/sessions/history/all"] = HistoryJson;

        using var httpClient = CreateHttpClient(handler);

        var client = CreateClient(httpClient);
        var since = DateTimeOffset.FromUnixTimeSeconds(1767139200);

        var entries = await client.GetHistorySinceAsync(since, 4, CancellationToken.None);

        Assert.HasCount(2, entries, "Only newer entries of the requested account should be reported!");
        Assert.AreEqual("10", entries[0].RatingKey, "Entries should be ordered ascending by viewed-at!");
        Assert.AreEqual("13", entries[1].RatingKey, "An entry without account id should be attributed to the requested account!");
        Assert.AreEqual(4, entries[1].AccountId, "A missing account id should default to the requested account!");
    }

    /// <summary>
    /// The history request carries the account id and a bounded container size
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task PlexClientGetHistorySinceSendsAccountAndPageSize()
    {
        using var handler = new StubHttpMessageHandler();

        handler.Responses["/status/sessions/history/all"] = HistoryJson;

        using var httpClient = CreateHttpClient(handler);

        var client = CreateClient(httpClient);

        await client.GetHistorySinceAsync(DateTimeOffset.UnixEpoch, 4, CancellationToken.None);

        Assert.HasCount(1, handler.Requests, "Exactly one request should have been sent!");
        Assert.IsTrue(handler.Requests[0].Contains("accountID=4", StringComparison.Ordinal), "The request should carry the account id!");
        Assert.IsTrue(handler.Requests[0].Contains("X-Plex-Container-Size=500", StringComparison.Ordinal), "The request should bound the page size!");
    }

    /// <summary>
    /// A full page that already reaches "since" stops pagination instead of fetching further pages
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task PlexClientGetHistorySinceStopsPagingOnceSinceIsReached()
    {
        using var handler = new StubHttpMessageHandler();

        var since = DateTimeOffset.FromUnixTimeSeconds(1_600_000_250);

        // A full 500-entry page, sorted descending: the first 250 entries are newer than "since",
        // the remaining 250 are at or before it, so pagination should stop after this one page.
        var pageEntries = Enumerable.Range(0, 500)
                                    .Select(i => $$"""{ "ratingKey": "{{1000 + i}}", "viewedAt": {{1_600_000_500 - i}}, "accountID": 4 }""");
        var pageJson = $$"""{ "MediaContainer": { "Metadata": [ {{string.Join(",", pageEntries)}} ] } }""";

        // Served only once: an unexpected second request should fail loudly instead of the
        // handler serving the same full page forever and hanging the test.
        handler.ResponseSequences["/status/sessions/history/all"] = new Queue<string>([pageJson]);

        using var httpClient = CreateHttpClient(handler);

        var client = CreateClient(httpClient);

        var entries = await client.GetHistorySinceAsync(since, 4, CancellationToken.None);

        Assert.HasCount(1, handler.Requests, "A full page that reaches \"since\" should not trigger a further request!");
        Assert.HasCount(250, entries, "Only entries newer than \"since\" should be reported!");
        Assert.AreEqual("1249", entries[0].RatingKey, "The oldest reported entry should be the one just newer than \"since\"!");
        Assert.AreEqual("1000", entries[^1].RatingKey, "The newest entry should be last once ordered ascending!");
    }

    /// <summary>
    /// A full first page of history is followed by a second page instead of being silently truncated
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task PlexClientGetHistorySincePagesBeyondFirstFullPage()
    {
        using var handler = new StubHttpMessageHandler();

        var since = DateTimeOffset.FromUnixTimeSeconds(1_600_000_000);

        // Page 1: 500 entries, all newer than "since", sorted descending, none reaching "since" yet.
        var firstPageEntries = Enumerable.Range(0, 500)
                                         .Select(i => $$"""{ "ratingKey": "{{1000 + i}}", "viewedAt": {{1_700_000_000 - i}}, "accountID": 4 }""");
        var firstPageJson = $$"""{ "MediaContainer": { "Metadata": [ {{string.Join(",", firstPageEntries)}} ] } }""";

        // Page 2: a single entry older than "since", ending the pagination.
        const string secondPageJson = """
                                      {
                                        "MediaContainer": {
                                          "Metadata": [
                                            { "ratingKey": "2000", "viewedAt": 1500000000, "accountID": 4 }
                                          ]
                                        }
                                      }
                                      """;

        handler.ResponseSequences["/status/sessions/history/all"] = new Queue<string>([firstPageJson, secondPageJson]);

        using var httpClient = CreateHttpClient(handler);

        var client = CreateClient(httpClient);

        var entries = await client.GetHistorySinceAsync(since, 4, CancellationToken.None);

        Assert.HasCount(2, handler.Requests, "A full first page should trigger a second, paged request!");
        Assert.Contains("X-Plex-Container-Start=0", handler.Requests[0], "The first request should start at offset zero!");
        Assert.Contains("X-Plex-Container-Start=500", handler.Requests[1], "The second request should start after the first page!");
        Assert.HasCount(500, entries, "All entries from the first page should be reported, not silently truncated!");
        Assert.AreEqual("1499", entries[0].RatingKey, "The oldest entry from the first page should be first once ordered ascending!");
        Assert.AreEqual("1000", entries[^1].RatingKey, "The newest entry from the first page should be last once ordered ascending!");
    }

    /// <summary>
    /// A server that ignores the paging parameters and keeps returning the same full history page
    /// does not loop forever
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task PlexClientGetHistorySinceStopsOnNonAdvancingPage()
    {
        using var handler = new StubHttpMessageHandler();

        // A full 500-entry page, all newer than "since", so nothing here reaches "since" and a
        // well-behaved server would be asked for a further page.
        var pageEntries = Enumerable.Range(0, 500)
                                    .Select(i => $$"""{ "ratingKey": "{{1000 + i}}", "viewedAt": {{1_700_000_000 - i}}, "accountID": 4 }""");
        var pageJson = $$"""{ "MediaContainer": { "Metadata": [ {{string.Join(",", pageEntries)}} ] } }""";

        // Registered as a plain response rather than a sequence: every request to this path, no
        // matter its "X-Plex-Container-Start", is answered with the exact same full first page.
        handler.Responses["/status/sessions/history/all"] = pageJson;

        using var httpClient = CreateHttpClient(handler);

        var client = CreateClient(httpClient);

        var entries = await client.GetHistorySinceAsync(DateTimeOffset.UnixEpoch, 4, CancellationToken.None);

        Assert.HasCount(2, handler.Requests, "A non-advancing repeat of the first page should stop pagination after one retry!");
        Assert.HasCount(500, entries, "Only the entries from the first page should be reported, not duplicated!");
    }

    /// <summary>
    /// A rewatch of the item that leads a page does not falsely trigger the non-advancing-page guard
    /// on the genuinely next page
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task PlexClientGetHistorySinceRewatchAtPageBoundaryKeepsPaging()
    {
        using var handler = new StubHttpMessageHandler();

        // Page 1: a full 500-entry page, leading with rating key "1000" watched at 1_700_000_000.
        var firstPageEntries = Enumerable.Range(0, 500)
                                         .Select(i => $$"""{ "ratingKey": "{{1000 + i}}", "viewedAt": {{1_700_000_000 - i}}, "accountID": 4 }""");
        var firstPageJson = $$"""{ "MediaContainer": { "Metadata": [ {{string.Join(",", firstPageEntries)}} ] } }""";

        // Page 2: leads with an earlier, separate viewing of the same item ("1000" rewatched), plus
        // two further entries. The rating key repeats but the viewed-at timestamp does not, so this
        // is a genuinely different page and must not be mistaken for a non-advancing repeat.
        const string secondPageJson = """
                                      {
                                        "MediaContainer": {
                                          "Metadata": [
                                            { "ratingKey": "1000", "viewedAt": 900000000, "accountID": 4 },
                                            { "ratingKey": "2000", "viewedAt": 899999999, "accountID": 4 },
                                            { "ratingKey": "2001", "viewedAt": 899999998, "accountID": 4 }
                                          ]
                                        }
                                      }
                                      """;

        handler.ResponseSequences["/status/sessions/history/all"] = new Queue<string>([firstPageJson, secondPageJson]);

        using var httpClient = CreateHttpClient(handler);

        var client = CreateClient(httpClient);

        var entries = await client.GetHistorySinceAsync(DateTimeOffset.UnixEpoch, 4, CancellationToken.None);

        Assert.HasCount(2, handler.Requests, "A rewatch leading the next page should not stop pagination early!");
        Assert.HasCount(503, entries, "Entries from both pages should be reported, including the rewatch!");
        Assert.HasCount(2, entries.Where(e => e.RatingKey == "1000").ToList(), "Both viewings of the rewatched item should be reported!");
    }

    /// <summary>
    /// A server that ignores the paging parameters and keeps returning the same full history page
    /// does not loop forever even when the leading entry of that page carries no rating key
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task PlexClientGetHistorySinceWithUnkeyedFirstEntryStopsOnNonAdvancingPage()
    {
        using var handler = new StubHttpMessageHandler();

        // A full 500-entry page whose first entry carries no rating key; the remaining 499 are
        // valid and none of them reach "since".
        const string firstEntry = """{ "viewedAt": 1700000000, "accountID": 4 }""";
        var restEntries = Enumerable.Range(0, 499)
                                    .Select(i => $$"""{ "ratingKey": "{{1000 + i}}", "viewedAt": {{1_699_999_999 - i}}, "accountID": 4 }""");
        var pageJson = $$"""{ "MediaContainer": { "Metadata": [ {{firstEntry}}, {{string.Join(",", restEntries)}} ] } }""";

        // Registered as a plain response rather than a sequence: every request to this path, no
        // matter its "X-Plex-Container-Start", is answered with the exact same full first page.
        handler.Responses["/status/sessions/history/all"] = pageJson;

        using var httpClient = CreateHttpClient(handler);

        var client = CreateClient(httpClient);

        var entries = await client.GetHistorySinceAsync(DateTimeOffset.UnixEpoch, 4, CancellationToken.None);

        Assert.HasCount(2, handler.Requests, "A non-advancing repeat should stop pagination after one retry even without a leading rating key!");
        Assert.HasCount(499, entries, "The unkeyed leading entry should be dropped, the rest of the first page kept!");
    }

    /// <summary>
    /// The metadata of a movie is mapped completely
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task PlexClientGetMediaItemMapsMovieMetadata()
    {
        using var handler = new StubHttpMessageHandler();

        handler.Responses["/library/metadata/12345"] = MovieJson;

        using var httpClient = CreateHttpClient(handler);

        var client = CreateClient(httpClient);

        var item = await client.GetMediaItemAsync("12345", CancellationToken.None);

        Assert.IsNotNull(item, "The movie should have been mapped!");
        Assert.AreEqual(MediaKind.Movie, item.Kind, "The media kind should be mapped!");
        Assert.AreEqual("Heat", item.Title, "The title should be mapped!");
        Assert.AreEqual(1995, item.Year, "The year should be mapped!");
        Assert.AreEqual("Warner Bros.", item.Studio, "The studio should be mapped!");
        Assert.AreEqual(171, item.RuntimeMinutes, "The runtime should be converted to minutes!");
        Assert.AreEqual("/data/Movies/Heat (1995)/Heat.mkv", item.FilePath, "The file path should be mapped!");
        Assert.HasCount(2, item.Genres, "Genres without tag should be dropped!");
        Assert.IsNotNull(item.Premiered, "The premiere date should be mapped!");
        Assert.IsNotNull(item.DateAdded, "The date added should be mapped!");
        Assert.IsTrue(item.Watch.Watched, "A positive view count should mark the item as watched!");
        Assert.AreEqual(2, item.Watch.PlayCount, "The play count should be mapped!");
        Assert.AreEqual(DateTimeOffset.FromUnixTimeSeconds(1700000000), item.Watch.LastPlayed, "The last playback should be mapped!");
    }

    /// <summary>
    /// External identifiers are split into type and value and the first one is the default
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task PlexClientGetMediaItemMapsUniqueIds()
    {
        using var handler = new StubHttpMessageHandler();

        handler.Responses["/library/metadata/12345"] = MovieJson;

        using var httpClient = CreateHttpClient(handler);

        var client = CreateClient(httpClient);

        var item = await client.GetMediaItemAsync("12345", CancellationToken.None);

        Assert.IsNotNull(item, "The movie should have been mapped!");
        Assert.HasCount(2, item.UniqueIds, "Identifiers without a scheme should be dropped!");
        Assert.AreEqual("imdb", item.UniqueIds[0].Type, "The identifier type should be split off!");
        Assert.AreEqual("tt0113277", item.UniqueIds[0].Value, "The query suffix should be stripped from the identifier!");
        Assert.IsTrue(item.UniqueIds[0].IsDefault, "The first identifier should be the default one!");
        Assert.AreEqual("tmdb", item.UniqueIds[1].Type, "The second identifier type should be split off!");
        Assert.IsFalse(item.UniqueIds[1].IsDefault, "Only the first identifier should be the default one!");
    }

    /// <summary>
    /// A file path containing a null character is rejected rather than written to disk
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task PlexClientGetMediaItemWithInvalidFilePathClearsFilePath()
    {
        using var handler = new StubHttpMessageHandler();

        handler.Responses["/library/metadata/12345"] = InvalidFilePathJson;

        using var httpClient = CreateHttpClient(handler);

        var client = CreateClient(httpClient);

        var item = await client.GetMediaItemAsync("12345", CancellationToken.None);

        Assert.IsNotNull(item, "The movie should still have been mapped!");
        Assert.IsNull(item.FilePath, "A file path containing a null character should be rejected!");
    }

    /// <summary>
    /// A file path exceeding the accepted maximum length is rejected rather than written to disk
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task PlexClientGetMediaItemWithOverlongFilePathClearsFilePath()
    {
        using var handler = new StubHttpMessageHandler();

        var overlongPath = $"/data/Movies/{new string('a', 4200)}.mkv";
        var json = $$"""
                     {
                       "MediaContainer": {
                         "Metadata": [
                           {
                             "ratingKey": "12345",
                             "type": "movie",
                             "title": "Heat",
                             "Media": [ { "Part": [ { "file": "{{overlongPath}}" } ] } ]
                           }
                         ]
                       }
                     }
                     """;

        handler.Responses["/library/metadata/12345"] = json;

        using var httpClient = CreateHttpClient(handler);

        var client = CreateClient(httpClient);

        var item = await client.GetMediaItemAsync("12345", CancellationToken.None);

        Assert.IsNotNull(item, "The movie should still have been mapped!");
        Assert.IsNull(item.FilePath, "An overlong file path should be rejected!");
    }

    /// <summary>
    /// Oversized text fields are truncated to a sane maximum length instead of being written unbounded
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task PlexClientGetMediaItemWithOversizedTextFieldsTruncatesThem()
    {
        using var handler = new StubHttpMessageHandler();

        var oversizedTitle = new string('t', 600);
        var oversizedSummary = new string('s', 4100);
        var json = $$"""
                     {
                       "MediaContainer": {
                         "Metadata": [
                           {
                             "ratingKey": "12345",
                             "type": "movie",
                             "title": "{{oversizedTitle}}",
                             "summary": "{{oversizedSummary}}"
                           }
                         ]
                       }
                     }
                     """;

        handler.Responses["/library/metadata/12345"] = json;

        using var httpClient = CreateHttpClient(handler);

        var client = CreateClient(httpClient);

        var item = await client.GetMediaItemAsync("12345", CancellationToken.None);

        Assert.IsNotNull(item, "The movie should still have been mapped!");
        Assert.AreEqual(512, item.Title.Length, "The title should be capped to the maximum short text length!");
        Assert.AreEqual(4000, item.Plot!.Length, "The plot should be capped to the maximum plot length!");
    }

    /// <summary>
    /// Truncating an oversized text field never splits a surrogate pair in two
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task PlexClientGetMediaItemWithTitleEndingInSurrogatePairDoesNotSplitIt()
    {
        using var handler = new StubHttpMessageHandler();

        var oversizedTitle = new string('t', 511) + "\U0001F600";
        var json = $$"""
                     {
                       "MediaContainer": {
                         "Metadata": [
                           {
                             "ratingKey": "12345",
                             "type": "movie",
                             "title": "{{oversizedTitle}}"
                           }
                         ]
                       }
                     }
                     """;

        handler.Responses["/library/metadata/12345"] = json;

        using var httpClient = CreateHttpClient(handler);

        var client = CreateClient(httpClient);

        var item = await client.GetMediaItemAsync("12345", CancellationToken.None);

        Assert.IsNotNull(item, "The movie should still have been mapped!");
        Assert.AreEqual(511, item.Title.Length, "The truncation should drop the whole surrogate pair rather than split it!");
        Assert.IsFalse(char.IsSurrogate(item.Title[^1]), "The truncated title should not end in a lone surrogate!");
    }

    /// <summary>
    /// An item carrying a rating key with control characters is skipped instead of being mapped
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task PlexClientGetMediaItemWithInvalidRatingKeyReturnsNull()
    {
        using var handler = new StubHttpMessageHandler();

        handler.Responses["/library/metadata/12345"] = InvalidRatingKeyJson;

        using var httpClient = CreateHttpClient(handler);

        var client = CreateClient(httpClient);

        var item = await client.GetMediaItemAsync("12345", CancellationToken.None);

        Assert.IsNull(item, "An item with an invalid rating key should be skipped!");
    }

    /// <summary>
    /// An item carrying a rating key that exceeds the accepted maximum length is skipped instead of being mapped
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task PlexClientGetMediaItemWithOverlongRatingKeyReturnsNull()
    {
        using var handler = new StubHttpMessageHandler();

        var overlongRatingKey = new string('1', 65);
        var json = $$"""
                     {
                       "MediaContainer": {
                         "Metadata": [
                           {
                             "ratingKey": "{{overlongRatingKey}}",
                             "type": "movie",
                             "title": "Heat"
                           }
                         ]
                       }
                     }
                     """;

        handler.Responses[$"/library/metadata/{overlongRatingKey}"] = json;

        using var httpClient = CreateHttpClient(handler);

        var client = CreateClient(httpClient);

        var item = await client.GetMediaItemAsync(overlongRatingKey, CancellationToken.None);

        Assert.IsNull(item, "An item with an overlong rating key should be skipped!");
    }

    /// <summary>
    /// An empty metadata container yields no item
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task PlexClientGetMediaItemWithoutMetadataReturnsNull()
    {
        using var handler = new StubHttpMessageHandler();

        handler.Responses["/library/metadata/999"] = EmptyContainerJson;

        using var httpClient = CreateHttpClient(handler);

        var client = CreateClient(httpClient);

        var item = await client.GetMediaItemAsync("999", CancellationToken.None);

        Assert.IsNull(item, "An empty container should not yield an item!");
    }

    /// <summary>
    /// An unsuccessful response is surfaced to the caller
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task PlexClientGetMediaItemOnErrorResponseThrows()
    {
        using var handler = new StubHttpMessageHandler();
        using var httpClient = CreateHttpClient(handler);

        var client = CreateClient(httpClient);

        await Assert.ThrowsAsync<HttpRequestException>(async () => await client.GetMediaItemAsync("999", CancellationToken.None),
                                                       "An unsuccessful response should be surfaced to the caller!");
    }

    /// <summary>
    /// Episodes are mapped including their season and episode numbers
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task PlexClientGetEpisodesMapsSeasonAndEpisodeNumbers()
    {
        using var handler = new StubHttpMessageHandler();

        handler.Responses["/library/metadata/500/allLeaves"] = EpisodesJson;

        using var httpClient = CreateHttpClient(handler);

        var client = CreateClient(httpClient);

        var episodes = await client.GetEpisodesAsync("500", CancellationToken.None);

        Assert.HasCount(1, episodes, "The episode should have been mapped!");
        Assert.AreEqual(MediaKind.Episode, episodes[0].Kind, "The media kind should be mapped!");
        Assert.AreEqual(2, episodes[0].SeasonNumber, "The season number should be taken from the parent index!");
        Assert.AreEqual(1, episodes[0].EpisodeNumber, "The episode number should be taken from the index!");
        Assert.AreEqual("Breaking Bad", episodes[0].ShowTitle, "The show title should be mapped!");
        Assert.AreEqual("500", episodes[0].ShowRatingKey, "The show rating key should be mapped!");
        Assert.IsFalse(episodes[0].Watch.Watched, "An episode without view count should not be watched!");
    }

    /// <summary>
    /// An episode with an invalid show rating key still maps, but with a cleared show rating key
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task PlexClientGetEpisodesWithInvalidShowRatingKeyClearsShowRatingKey()
    {
        using var handler = new StubHttpMessageHandler();

        const string json = """
                            {
                              "MediaContainer": {
                                "Metadata": [
                                  {
                                    "ratingKey": "501",
                                    "type": "episode",
                                    "title": "Pilot",
                                    "grandparentTitle": "Breaking Bad",
                                    "grandparentRatingKey": "5\n00"
                                  }
                                ]
                              }
                            }
                            """;

        handler.Responses["/library/metadata/500/allLeaves"] = json;

        using var httpClient = CreateHttpClient(handler);

        var client = CreateClient(httpClient);

        var episodes = await client.GetEpisodesAsync("500", CancellationToken.None);

        Assert.HasCount(1, episodes, "The episode should still have been mapped!");
        Assert.IsNull(episodes[0].ShowRatingKey, "An invalid show rating key should be dropped!");
    }

    /// <summary>
    /// The episodes request carries paging parameters bounding the container size
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task PlexClientGetEpisodesSendsContainerPaging()
    {
        using var handler = new StubHttpMessageHandler();

        handler.Responses["/library/metadata/500/allLeaves"] = EpisodesJson;

        using var httpClient = CreateHttpClient(handler);

        var client = CreateClient(httpClient);

        await client.GetEpisodesAsync("500", CancellationToken.None);

        Assert.HasCount(1, handler.Requests, "A single, incomplete page should not trigger a further request!");
        Assert.Contains("X-Plex-Container-Start=0", handler.Requests[0], "The first request should start at offset zero!");
        Assert.Contains("X-Plex-Container-Size=200", handler.Requests[0], "The request should bound the page size!");
    }

    /// <summary>
    /// A full first page of episodes is followed by a second page instead of being silently truncated
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task PlexClientGetEpisodesPagesBeyondFirstFullPage()
    {
        using var handler = new StubHttpMessageHandler();

        // Page 1: a full 200-entry page.
        var firstPageEntries = Enumerable.Range(0, 200)
                                         .Select(i => $$"""{ "ratingKey": "{{1000 + i}}", "type": "episode", "grandparentRatingKey": "500" }""");
        var firstPageJson = $$"""{ "MediaContainer": { "Metadata": [ {{string.Join(",", firstPageEntries)}} ] } }""";

        // Page 2: a single entry, ending the pagination.
        const string secondPageJson = """
                                      {
                                        "MediaContainer": {
                                          "Metadata": [
                                            { "ratingKey": "2000", "type": "episode", "grandparentRatingKey": "500" }
                                          ]
                                        }
                                      }
                                      """;

        handler.ResponseSequences["/library/metadata/500/allLeaves"] = new Queue<string>([firstPageJson, secondPageJson]);

        using var httpClient = CreateHttpClient(handler);

        var client = CreateClient(httpClient);

        var episodes = await client.GetEpisodesAsync("500", CancellationToken.None);

        Assert.HasCount(2, handler.Requests, "A full first page should trigger a second, paged request!");
        Assert.Contains("X-Plex-Container-Start=0", handler.Requests[0], "The first request should start at offset zero!");
        Assert.Contains("X-Plex-Container-Start=200", handler.Requests[1], "The second request should start after the first page!");
        Assert.HasCount(201, episodes, "Episodes from both pages should be reported, not silently truncated at the container size!");
    }

    /// <summary>
    /// Library items are mapped and a missing metadata list yields an empty result
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task PlexClientGetLibraryItemsMapsMetadata()
    {
        using var handler = new StubHttpMessageHandler();

        handler.Responses["/library/sections/1/all"] = MovieJson;
        handler.Responses["/library/sections/9/all"] = EmptyContainerJson;

        using var httpClient = CreateHttpClient(handler);

        var client = CreateClient(httpClient);

        var items = await client.GetLibraryItemsAsync("1", CancellationToken.None);
        var empty = await client.GetLibraryItemsAsync("9", CancellationToken.None);

        Assert.HasCount(1, items, "The library item should have been mapped!");
        Assert.AreEqual("Heat", items[0].Title, "The title should be mapped!");
        Assert.IsEmpty(empty, "An empty container should be reported as an empty list!");
    }

    /// <summary>
    /// An item with an invalid rating key is skipped rather than being included in the mapped list
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task PlexClientGetLibraryItemsSkipsItemsWithInvalidRatingKey()
    {
        using var handler = new StubHttpMessageHandler();

        const string json = """
                            {
                              "MediaContainer": {
                                "Metadata": [
                                  { "ratingKey": "12345", "type": "movie", "title": "Heat" },
                                  { "ratingKey": "12\n345", "type": "movie", "title": "Broken" }
                                ]
                              }
                            }
                            """;

        handler.Responses["/library/sections/1/all"] = json;

        using var httpClient = CreateHttpClient(handler);

        var client = CreateClient(httpClient);

        var items = await client.GetLibraryItemsAsync("1", CancellationToken.None);

        Assert.HasCount(1, items, "Only the item with a valid rating key should be reported!");
        Assert.AreEqual("Heat", items[0].Title, "The valid item should have been mapped!");
    }

    /// <summary>
    /// The library items request carries paging parameters bounding the container size
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task PlexClientGetLibraryItemsSendsContainerPaging()
    {
        using var handler = new StubHttpMessageHandler();

        handler.Responses["/library/sections/1/all"] = MovieJson;

        using var httpClient = CreateHttpClient(handler);

        var client = CreateClient(httpClient);

        await client.GetLibraryItemsAsync("1", CancellationToken.None);

        Assert.HasCount(1, handler.Requests, "A single, incomplete page should not trigger a further request!");
        Assert.Contains("X-Plex-Container-Start=0", handler.Requests[0], "The first request should start at offset zero!");
        Assert.Contains("X-Plex-Container-Size=200", handler.Requests[0], "The request should bound the page size!");
    }

    /// <summary>
    /// A full first page of library items is followed by a second page instead of being silently truncated
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task PlexClientGetLibraryItemsPagesBeyondFirstFullPage()
    {
        using var handler = new StubHttpMessageHandler();

        // Page 1: a full 200-entry page.
        var firstPageEntries = Enumerable.Range(0, 200)
                                         .Select(i => $$"""{ "ratingKey": "{{1000 + i}}", "type": "movie" }""");
        var firstPageJson = $$"""{ "MediaContainer": { "Metadata": [ {{string.Join(",", firstPageEntries)}} ] } }""";

        // Page 2: a single entry, ending the pagination.
        const string secondPageJson = """
                                      {
                                        "MediaContainer": {
                                          "Metadata": [
                                            { "ratingKey": "2000", "type": "movie" }
                                          ]
                                        }
                                      }
                                      """;

        handler.ResponseSequences["/library/sections/1/all"] = new Queue<string>([firstPageJson, secondPageJson]);

        using var httpClient = CreateHttpClient(handler);

        var client = CreateClient(httpClient);

        var items = await client.GetLibraryItemsAsync("1", CancellationToken.None);

        Assert.HasCount(2, handler.Requests, "A full first page should trigger a second, paged request!");
        Assert.Contains("X-Plex-Container-Start=0", handler.Requests[0], "The first request should start at offset zero!");
        Assert.Contains("X-Plex-Container-Start=200", handler.Requests[1], "The second request should start after the first page!");
        Assert.HasCount(201, items, "Items from both pages should be reported, not silently truncated at the container size!");
    }

    /// <summary>
    /// A server that ignores the paging parameters and keeps returning the same full library page
    /// does not loop forever
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task PlexClientGetLibraryItemsStopsOnNonAdvancingPage()
    {
        using var handler = new StubHttpMessageHandler();

        // A full 200-entry page; a well-behaved server would be asked for a further page.
        var pageEntries = Enumerable.Range(0, 200)
                                    .Select(i => $$"""{ "ratingKey": "{{1000 + i}}", "type": "movie" }""");
        var pageJson = $$"""{ "MediaContainer": { "Metadata": [ {{string.Join(",", pageEntries)}} ] } }""";

        // Registered as a plain response rather than a sequence: every request to this path, no
        // matter its "X-Plex-Container-Start", is answered with the exact same full first page.
        handler.Responses["/library/sections/1/all"] = pageJson;

        using var httpClient = CreateHttpClient(handler);

        var client = CreateClient(httpClient);

        var items = await client.GetLibraryItemsAsync("1", CancellationToken.None);

        Assert.HasCount(2, handler.Requests, "A non-advancing repeat of the first page should stop pagination after one retry!");
        Assert.HasCount(200, items, "Only the items from the first page should be reported, not duplicated!");
    }

    /// <summary>
    /// Create an HTTP client bound to the given handler
    /// </summary>
    /// <param name="handler">Handler serving the responses</param>
    /// <returns>The HTTP client</returns>
    private static HttpClient CreateHttpClient(StubHttpMessageHandler handler)
    {
        return new HttpClient(handler, disposeHandler: false)
               {
                   BaseAddress = new Uri("http://plex.test")
               };
    }

    /// <summary>
    /// Create a Plex client using the given HTTP client and options
    /// </summary>
    /// <param name="httpClient">HTTP client</param>
    /// <param name="options">Plex options, or <c>null</c> for the defaults</param>
    /// <returns>The Plex client under test</returns>
    private static PlexClient CreateClient(HttpClient httpClient, PlexOptions? options = null)
    {
        return new PlexClient(httpClient, NullLogger<PlexClient>.Instance, Options.Create(options ?? new PlexOptions()));
    }

    #endregion // Methods
}