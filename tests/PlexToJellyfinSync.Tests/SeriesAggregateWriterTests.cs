using Microsoft.Extensions.Logging.Abstractions;

using PlexToJellyfinSync.Core.Enums;
using PlexToJellyfinSync.Core.Models;
using PlexToJellyfinSync.Service;

namespace PlexToJellyfinSync.Tests;

/// <summary>
/// Tests for <see cref="SeriesAggregateWriter"/>
/// </summary>
[TestClass]
public sealed class SeriesAggregateWriterTests
{
    #region Fields

    private static readonly DateTimeOffset _firstPlayed = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset _lastPlayed = new(2026, 1, 5, 0, 0, 0, TimeSpan.Zero);

    private readonly string _showDirectory = "/data/Shows/Breaking Bad".Replace('/', Path.DirectorySeparatorChar);
    private readonly string _seasonOneDirectory = "/data/Shows/Breaking Bad/Season 01".Replace('/', Path.DirectorySeparatorChar);
    private readonly string _seasonTwoDirectory = "/data/Shows/Breaking Bad/Season 02".Replace('/', Path.DirectorySeparatorChar);
    private readonly TestContext _testContext;

    private FakePlexClient _plexClient = new();
    private RecordingNfoWriter _nfoWriter = new();
    private StubPathMapper _pathMapper = new();
    private SyncStatusService _status = new(NullLogger<SyncStatusService>.Instance);

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="testContext">Test context</param>
    public SeriesAggregateWriterTests(TestContext testContext)
    {
        _testContext = testContext;
    }

    #endregion // Constructors

    #region Methods

    /// <summary>
    /// Create fresh test doubles for each test
    /// </summary>
    [TestInitialize]
    public void Initialize()
    {
        _plexClient = new FakePlexClient();
        _nfoWriter = new RecordingNfoWriter();
        _pathMapper = new StubPathMapper();
        _status = new SyncStatusService(NullLogger<SyncStatusService>.Instance);
    }

    /// <summary>
    /// A partially watched season produces an unwatched season item in the episodes' directory and an unwatched
    /// series item in the parent directory
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task SeriesAggregateWriterPartiallyWatchedWritesUnwatchedSeasonAndSeries()
    {
        _plexClient.Items["s1"] = new MediaItem
                                  {
                                      RatingKey = "s1",
                                      Kind = MediaKind.Series,
                                      Title = "Breaking Bad"
                                  };
        _plexClient.Episodes["s1"] = [
                                         CreateEpisode("e1", 1, _seasonOneDirectory, watched: true),
                                         CreateEpisode("e2", 1, _seasonOneDirectory, watched: false)
                                     ];

        var writer = CreateWriter();

        await writer.WriteAggregatesAsync("s1", _testContext.CancellationToken);

        var seasons = _nfoWriter.WritesOf(MediaKind.Season);
        var series = _nfoWriter.WritesOf(MediaKind.Series);

        Assert.HasCount(1, seasons, "One season item should be written!");
        Assert.AreEqual(1, seasons[0].Item.SeasonNumber, "The season number should be set!");
        Assert.AreEqual("Season 1", seasons[0].Item.Title, "The season title should be derived from its number!");
        Assert.IsFalse(seasons[0].Item.Watch.Watched, "A partially watched season should not be watched!");
        Assert.AreEqual(_seasonOneDirectory, seasons[0].LocalPath, "The season should be written to the episodes' directory!");
        Assert.HasCount(1, series, "One series item should be written!");
        Assert.AreEqual("Breaking Bad", series[0].Item.Title, "The series title should come from the show metadata!");
        Assert.IsFalse(series[0].Item.Watch.Watched, "A partially watched series should not be watched!");
        Assert.AreEqual(_showDirectory, series[0].LocalPath, "The series should be written to the parent of the season directory!");
    }

    /// <summary>
    /// When every episode is watched, season and series are watched with the latest play date
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task SeriesAggregateWriterAllWatchedWritesWatchedSeasonAndSeries()
    {
        _plexClient.Items["s1"] = new MediaItem
                                  {
                                      RatingKey = "s1",
                                      Kind = MediaKind.Series,
                                      Title = "Breaking Bad"
                                  };
        _plexClient.Episodes["s1"] = [
                                         CreateEpisode("e1", 1, _seasonOneDirectory, watched: true, lastPlayed: _firstPlayed),
                                         CreateEpisode("e2", 1, _seasonOneDirectory, watched: true, lastPlayed: _lastPlayed)
                                     ];

        var writer = CreateWriter();

        await writer.WriteAggregatesAsync("s1", _testContext.CancellationToken);

        var season = _nfoWriter.WritesOf(MediaKind.Season).Single().Item;
        var series = _nfoWriter.WritesOf(MediaKind.Series).Single().Item;

        Assert.IsTrue(season.Watch.Watched, "The season should be watched!");
        Assert.AreEqual(_lastPlayed, season.Watch.LastPlayed, "The season should carry the latest play date!");
        Assert.IsTrue(series.Watch.Watched, "The series should be watched!");
        Assert.AreEqual(_lastPlayed, series.Watch.LastPlayed, "The series should carry the latest play date!");
    }

    /// <summary>
    /// Every season is written to its own directory and the series once
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task SeriesAggregateWriterTwoSeasonsWritesOneItemPerSeasonDirectory()
    {
        _plexClient.Items["s1"] = new MediaItem
                                  {
                                      RatingKey = "s1",
                                      Kind = MediaKind.Series,
                                      Title = "Breaking Bad"
                                  };
        _plexClient.Episodes["s1"] = [
                                         CreateEpisode("e1", 1, _seasonOneDirectory, watched: true),
                                         CreateEpisode("e2", 2, _seasonTwoDirectory, watched: false)
                                     ];

        var writer = CreateWriter();

        await writer.WriteAggregatesAsync("s1", _testContext.CancellationToken);

        var seasons = _nfoWriter.WritesOf(MediaKind.Season);

        Assert.HasCount(2, seasons, "One season item per season should be written!");
        Assert.AreEqual(_seasonOneDirectory, seasons.Single(write => write.Item.SeasonNumber == 1).LocalPath, "Season 1 should be written to its own directory!");
        Assert.AreEqual(_seasonTwoDirectory, seasons.Single(write => write.Item.SeasonNumber == 2).LocalPath, "Season 2 should be written to its own directory!");
        Assert.HasCount(1, _nfoWriter.WritesOf(MediaKind.Series), "Exactly one series item should be written!");
    }

    /// <summary>
    /// A season without a season number gets the generic title
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task SeriesAggregateWriterEpisodesWithoutSeasonNumberWriteGenericSeasonTitle()
    {
        _plexClient.Items["s1"] = new MediaItem
                                  {
                                      RatingKey = "s1",
                                      Kind = MediaKind.Series,
                                      Title = "Breaking Bad"
                                  };
        _plexClient.Episodes["s1"] = [CreateEpisode("e1", null, _seasonOneDirectory, watched: true)];

        var writer = CreateWriter();

        await writer.WriteAggregatesAsync("s1", _testContext.CancellationToken);

        var season = _nfoWriter.WritesOf(MediaKind.Season).Single().Item;

        Assert.AreEqual("Season", season.Title, "A season without a number should get the generic title!");
        Assert.IsNull(season.SeasonNumber, "The season number should stay unset!");
    }

    /// <summary>
    /// Episodes without a file path or without a mapping are left out of the aggregates
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task SeriesAggregateWriterUnmappedEpisodesAreLeftOut()
    {
        _plexClient.Items["s1"] = new MediaItem
                                  {
                                      RatingKey = "s1",
                                      Kind = MediaKind.Series,
                                      Title = "Breaking Bad"
                                  };

        var noPath = CreateEpisode("e1", 1, _seasonOneDirectory, watched: false);
        var unmapped = CreateEpisode("e2", 1, _seasonOneDirectory, watched: false);
        var mapped = CreateEpisode("e3", 1, _seasonOneDirectory, watched: true);

        noPath.FilePath = null;
        _pathMapper.Unmapped.Add(unmapped.FilePath!);
        _plexClient.Episodes["s1"] = [noPath, unmapped, mapped];

        var writer = CreateWriter();

        await writer.WriteAggregatesAsync("s1", _testContext.CancellationToken);

        Assert.IsTrue(_nfoWriter.WritesOf(MediaKind.Season).Single().Item.Watch.Watched, "Only the mapped, watched episode should count!");
        Assert.IsTrue(_nfoWriter.WritesOf(MediaKind.Series).Single().Item.Watch.Watched, "Only the mapped, watched episode should count!");
    }

    /// <summary>
    /// Without any mapped episode nothing is written and the show metadata is not requested
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task SeriesAggregateWriterNoMappedEpisodeWritesNothing()
    {
        var noPath = CreateEpisode("e1", 1, _seasonOneDirectory, watched: true);
        var unmapped = CreateEpisode("e2", 1, _seasonOneDirectory, watched: true);

        noPath.FilePath = null;
        _pathMapper.Unmapped.Add(unmapped.FilePath!);
        _plexClient.Episodes["s1"] = [noPath, unmapped];

        var writer = CreateWriter();

        await writer.WriteAggregatesAsync("s1", _testContext.CancellationToken);

        Assert.IsEmpty(_nfoWriter.Writes, "Nothing should be written without a mapped episode!");
        Assert.IsEmpty(_plexClient.MediaItemRequests, "The show metadata should not be requested!");
    }

    /// <summary>
    /// A missing show metadata falls back to the show title of the first mapped episode
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task SeriesAggregateWriterMissingShowMetadataUsesEpisodeShowTitle()
    {
        _plexClient.Episodes["s1"] = [CreateEpisode("e1", 1, _seasonOneDirectory, watched: true)];

        var writer = CreateWriter();

        await writer.WriteAggregatesAsync("s1", _testContext.CancellationToken);

        var series = _nfoWriter.WritesOf(MediaKind.Series).Single().Item;

        Assert.AreEqual("Breaking Bad", series.Title, "The title should fall back to the episode's show title!");
        Assert.AreEqual(MediaKind.Series, series.Kind, "The item should be a series!");
    }

    /// <summary>
    /// A missing show metadata and a missing episode show title produce an empty series title
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task SeriesAggregateWriterMissingShowMetadataAndTitleUsesEmptyTitle()
    {
        var episode = CreateEpisode("e1", 1, _seasonOneDirectory, watched: true);

        episode.ShowTitle = null;
        _plexClient.Episodes["s1"] = [episode];

        var writer = CreateWriter();

        await writer.WriteAggregatesAsync("s1", _testContext.CancellationToken);

        Assert.AreEqual(string.Empty, _nfoWriter.WritesOf(MediaKind.Series).Single().Item.Title, "The title should be empty!");
    }

    /// <summary>
    /// A local path without a parent directory produces neither a season nor a series write
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task SeriesAggregateWriterPathWithoutDirectoryWritesNothing()
    {
        _plexClient.Items["s1"] = new MediaItem
                                  {
                                      RatingKey = "s1",
                                      Kind = MediaKind.Series,
                                      Title = "Breaking Bad"
                                  };

        var episode = CreateEpisode("e1", 1, _seasonOneDirectory, watched: true);

        episode.FilePath = "S01E01.mkv";
        _plexClient.Episodes["s1"] = [episode];

        var writer = CreateWriter();

        await writer.WriteAggregatesAsync("s1", _testContext.CancellationToken);

        Assert.IsEmpty(_nfoWriter.Writes, "A path without a directory should not produce any aggregate write!");
    }

    /// <summary>
    /// An exception from the Plex client propagates to the caller
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task SeriesAggregateWriterPlexFailurePropagates()
    {
        _plexClient.EpisodesExceptions["s1"] = new HttpRequestException("plex down");

        var writer = CreateWriter();

        await Assert.ThrowsAsync<HttpRequestException>(async () => await writer.WriteAggregatesAsync("s1", _testContext.CancellationToken),
                                                       "A failing Plex request should propagate to the caller!");

        Assert.AreEqual(0L, _status.GetSnapshot().Errors, "The writer should leave the error handling to its caller!");
    }

    /// <summary>
    /// An exception from the NFO writer propagates to the caller
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task SeriesAggregateWriterNfoFailurePropagates()
    {
        _plexClient.Items["s1"] = new MediaItem
                                  {
                                      RatingKey = "s1",
                                      Kind = MediaKind.Series,
                                      Title = "Breaking Bad"
                                  };
        _plexClient.Episodes["s1"] = [CreateEpisode("e1", 1, _seasonOneDirectory, watched: true)];
        _nfoWriter.FailuresByRatingKey["s1"] = new IOException("disk full");

        var writer = CreateWriter();

        await Assert.ThrowsAsync<IOException>(async () => await writer.WriteAggregatesAsync("s1", _testContext.CancellationToken),
                                              "A failing NFO write should propagate to the caller!");

        Assert.AreEqual(0L, _status.GetSnapshot().Errors, "The writer should leave the error handling to its caller!");
    }

    /// <summary>
    /// Create an episode
    /// </summary>
    /// <param name="ratingKey">Rating key</param>
    /// <param name="seasonNumber">Season number</param>
    /// <param name="directory">Directory of the episode's file</param>
    /// <param name="watched">Whether the episode is watched</param>
    /// <param name="lastPlayed">Last play date</param>
    /// <returns>The episode</returns>
    private static MediaItem CreateEpisode(string ratingKey, int? seasonNumber, string directory, bool watched, DateTimeOffset? lastPlayed = null)
    {
        return new MediaItem
               {
                   RatingKey = ratingKey,
                   Kind = MediaKind.Episode,
                   Title = $"Episode {ratingKey}",
                   SeasonNumber = seasonNumber,
                   ShowRatingKey = "s1",
                   ShowTitle = "Breaking Bad",
                   FilePath = Path.Combine(directory, $"{ratingKey}.mkv"),
                   Watch = new WatchInfo
                           {
                               Watched = watched,
                               PlayCount = watched ? 1 : 0,
                               LastPlayed = watched ? lastPlayed ?? _firstPlayed : null
                           }
               };
    }

    /// <summary>
    /// Create the writer under test wired to the current test doubles and a real item writer
    /// </summary>
    /// <returns>The writer under test</returns>
    private SeriesAggregateWriter CreateWriter()
    {
        var itemWriter = new MediaItemWriter(_nfoWriter, _pathMapper, _status, NullLogger<MediaItemWriter>.Instance);

        return new SeriesAggregateWriter(_plexClient, _pathMapper, itemWriter, new WatchAggregator());
    }

    #endregion // Methods
}