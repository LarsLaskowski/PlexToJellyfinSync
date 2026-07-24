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