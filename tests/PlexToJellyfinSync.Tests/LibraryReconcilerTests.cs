using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using PlexToJellyfinSync.Core.Enums;
using PlexToJellyfinSync.Core.Models;
using PlexToJellyfinSync.Core.Options;
using PlexToJellyfinSync.Service;

namespace PlexToJellyfinSync.Tests;

/// <summary>
/// Tests for <see cref="LibraryReconciler"/>
/// </summary>
[TestClass]
public sealed class LibraryReconcilerTests
{
    #region Fields

    private static readonly DateTimeOffset _firstPlayed = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset _lastPlayed = new(2026, 1, 5, 0, 0, 0, TimeSpan.Zero);

    private readonly string _seasonDirectory = "/data/Shows/Breaking Bad/Season 01".Replace('/', Path.DirectorySeparatorChar);
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
    public LibraryReconcilerTests(TestContext testContext)
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
    /// Every movie of a movie library is written and counted
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task LibraryReconcilerMovieLibraryWritesEveryMovie()
    {
        AddMovies("1", 3);

        var reconciler = CreateReconciler();

        await reconciler.ReconcileLibraryAsync(CreateLibrary("1", MediaKind.Movie), _testContext.CancellationToken);

        Assert.HasCount(3, _nfoWriter.WritesOf(MediaKind.Movie), "Every movie should be written!");
        Assert.AreEqual(3L, _status.GetSnapshot().ItemsProcessed, "Every movie should be counted as processed!");
    }

    /// <summary>
    /// Every episode of every show of a series library is written and counted, and the aggregates are written
    /// when enabled
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task LibraryReconcilerSeriesLibraryWritesEpisodesAndAggregates()
    {
        AddShow("s1", "e", 3);
        AddShow("s2", "f", 2);
        _plexClient.LibraryItems["2"] = [_plexClient.Items["s1"], _plexClient.Items["s2"]];

        var reconciler = CreateReconciler(new SyncOptions
                                          {
                                              WriteSeriesSeasonAggregates = true
                                          });

        await reconciler.ReconcileLibraryAsync(CreateLibrary("2", MediaKind.Series), _testContext.CancellationToken);

        Assert.HasCount(5, _nfoWriter.WritesOf(MediaKind.Episode), "Every episode should be written!");
        Assert.AreEqual(5L, _status.GetSnapshot().ItemsProcessed, "Every episode should be counted as processed!");
        Assert.HasCount(2, _nfoWriter.WritesOf(MediaKind.Season), "One season per show should be written!");
        Assert.HasCount(2, _nfoWriter.WritesOf(MediaKind.Series), "One series item per show should be written!");
    }

    /// <summary>
    /// Without the aggregate option no season or series item is written
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task LibraryReconcilerSeriesLibraryAggregatesDisabledWritesEpisodesOnly()
    {
        AddShow("s1", "e", 2);
        _plexClient.LibraryItems["2"] = [_plexClient.Items["s1"]];

        var reconciler = CreateReconciler(new SyncOptions
                                          {
                                              WriteSeriesSeasonAggregates = false
                                          });

        await reconciler.ReconcileLibraryAsync(CreateLibrary("2", MediaKind.Series), _testContext.CancellationToken);

        Assert.HasCount(2, _nfoWriter.WritesOf(MediaKind.Episode), "Every episode should be written!");
        Assert.IsEmpty(_nfoWriter.WritesOf(MediaKind.Season), "No season item should be written!");
        Assert.IsEmpty(_nfoWriter.WritesOf(MediaKind.Series), "No series item should be written!");
    }

    /// <summary>
    /// A library of another kind is neither requested nor written
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task LibraryReconcilerUnknownKindDoesNothing()
    {
        AddMovies("3", 2);

        var reconciler = CreateReconciler();

        await reconciler.ReconcileLibraryAsync(CreateLibrary("3", MediaKind.Unknown), _testContext.CancellationToken);

        Assert.IsEmpty(_plexClient.LibraryItemRequests, "No items should be requested for an unknown library kind!");
        Assert.IsEmpty(_nfoWriter.Writes, "Nothing should be written for an unknown library kind!");
    }

    /// <summary>
    /// A failing movie is counted as an error and the other movies are still written
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task LibraryReconcilerFailingMovieIsIsolated()
    {
        AddMovies("1", 3);
        _nfoWriter.FailuresByRatingKey["m2"] = new IOException("disk full");
        _status.Update(s => s.PlexConnected = true);

        var reconciler = CreateReconciler();

        await reconciler.ReconcileLibraryAsync(CreateLibrary("1", MediaKind.Movie), _testContext.CancellationToken);

        var snapshot = _status.GetSnapshot();

        Assert.HasCount(2, _nfoWriter.WritesOf(MediaKind.Movie), "The remaining movies should still be written!");
        Assert.AreEqual(1L, snapshot.Errors, "The failure should be counted!");
        Assert.AreEqual("disk full", snapshot.LastError, "The last error should be set!");
        Assert.IsTrue(snapshot.PlexConnected, "A failing item should not change the connection state!");
        Assert.AreEqual(3L, snapshot.ItemsProcessed, "Every movie should be counted as processed!");
    }

    /// <summary>
    /// A movie failing with a timeout-style cancellation that the caller did not request is isolated like any
    /// other failure
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task LibraryReconcilerMovieTimeoutIsIsolated()
    {
        AddMovies("1", 3);
        _nfoWriter.FailuresByRatingKey["m1"] = new OperationCanceledException("timeout");

        var reconciler = CreateReconciler();

        await reconciler.ReconcileLibraryAsync(CreateLibrary("1", MediaKind.Movie), _testContext.CancellationToken);

        Assert.HasCount(2, _nfoWriter.WritesOf(MediaKind.Movie), "The remaining movies should still be written!");
        Assert.AreEqual(1L, _status.GetSnapshot().Errors, "The timeout should be counted as an error!");
    }

    /// <summary>
    /// A failing episode is counted as an error and the other episodes are still written
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task LibraryReconcilerFailingEpisodeIsIsolated()
    {
        AddShow("s1", "e", 3);
        _plexClient.LibraryItems["2"] = [_plexClient.Items["s1"]];
        _nfoWriter.FailuresByRatingKey["e2"] = new IOException("disk full");
        _status.Update(s => s.PlexConnected = true);

        var reconciler = CreateReconciler(new SyncOptions
                                          {
                                              WriteSeriesSeasonAggregates = false
                                          });

        await reconciler.ReconcileLibraryAsync(CreateLibrary("2", MediaKind.Series), _testContext.CancellationToken);

        var snapshot = _status.GetSnapshot();

        Assert.HasCount(2, _nfoWriter.WritesOf(MediaKind.Episode), "The remaining episodes should still be written!");
        Assert.AreEqual(1L, snapshot.Errors, "The failure should be counted!");
        Assert.AreEqual("disk full", snapshot.LastError, "The last error should be set!");
        Assert.IsTrue(snapshot.PlexConnected, "A failing item should not change the connection state!");
    }

    /// <summary>
    /// A failing show is counted as an error and the other shows are still written
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task LibraryReconcilerFailingShowIsIsolated()
    {
        AddShow("s1", "e", 2);
        AddShow("s2", "f", 2);
        _plexClient.LibraryItems["2"] = [_plexClient.Items["s1"], _plexClient.Items["s2"]];
        _plexClient.EpisodesExceptions["s1"] = new HttpRequestException("plex down");
        _status.Update(s => s.PlexConnected = true);

        var reconciler = CreateReconciler(new SyncOptions
                                          {
                                              WriteSeriesSeasonAggregates = false
                                          });

        await reconciler.ReconcileLibraryAsync(CreateLibrary("2", MediaKind.Series), _testContext.CancellationToken);

        var snapshot = _status.GetSnapshot();
        var written = _nfoWriter.WritesOf(MediaKind.Episode);

        Assert.HasCount(2, written, "The episodes of the other show should still be written!");
        Assert.IsTrue(written.All(write => write.Item.ShowRatingKey == "s2"), "Only the episodes of the healthy show should be written!");
        Assert.AreEqual(1L, snapshot.Errors, "The failing show should be counted once!");
        Assert.AreEqual("plex down", snapshot.LastError, "The last error should be set!");
        Assert.IsTrue(snapshot.PlexConnected, "A failing show should not change the connection state!");
    }

    /// <summary>
    /// Episodes of one show are written concurrently, bounded by the configured parallelism
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task LibraryReconcilerEpisodesRunConcurrentlyUpToConfiguredParallelism()
    {
        AddShow("s1", "e", 6);
        _plexClient.LibraryItems["2"] = [_plexClient.Items["s1"]];
        _nfoWriter.ConcurrencyGate = 2;

        var reconciler = CreateReconciler(new SyncOptions
                                          {
                                              EpisodeReconcileParallelism = 2,
                                              WriteSeriesSeasonAggregates = false
                                          });

        await reconciler.ReconcileLibraryAsync(CreateLibrary("2", MediaKind.Series), _testContext.CancellationToken);

        Assert.HasCount(6, _nfoWriter.WritesOf(MediaKind.Episode), "Every episode should be written!");
        Assert.AreEqual(2, _nfoWriter.MaxObservedConcurrency, "Episodes should overlap, but never beyond the configured limit!");
    }

    /// <summary>
    /// A parallelism of zero or less is treated as sequential
    /// </summary>
    /// <param name="parallelism">Configured parallelism</param>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public async Task LibraryReconcilerNonPositiveParallelismRunsSequentially(int parallelism)
    {
        AddShow("s1", "e", 3);
        _plexClient.LibraryItems["2"] = [_plexClient.Items["s1"]];
        _nfoWriter.ConcurrencyGate = 2;

        var reconciler = CreateReconciler(new SyncOptions
                                          {
                                              EpisodeReconcileParallelism = parallelism,
                                              WriteSeriesSeasonAggregates = false
                                          });

        await reconciler.ReconcileLibraryAsync(CreateLibrary("2", MediaKind.Series), _testContext.CancellationToken);

        Assert.HasCount(3, _nfoWriter.WritesOf(MediaKind.Episode), "Every episode should be written!");
        Assert.AreEqual(1, _nfoWriter.MaxObservedConcurrency, "A non-positive parallelism should be clamped to sequential writes!");
    }

    /// <summary>
    /// Episodes sharing one file are never written concurrently
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task LibraryReconcilerSharedFileEpisodesNeverOverlap()
    {
        var sharedFile = $"{_seasonDirectory}{Path.DirectorySeparatorChar}S01E01-E02.mkv";
        var episodes = new List<MediaItem>();

        for (var number = 1; number <= 2; number++)
        {
            episodes.Add(new MediaItem
                         {
                             RatingKey = $"e{number}",
                             Kind = MediaKind.Episode,
                             SeasonNumber = 1,
                             EpisodeNumber = number,
                             ShowRatingKey = "s1",
                             FilePath = sharedFile
                         });
        }

        _plexClient.Items["s1"] = new MediaItem
                                  {
                                      RatingKey = "s1",
                                      Kind = MediaKind.Series,
                                      Title = "Breaking Bad"
                                  };
        _plexClient.Episodes["s1"] = episodes;
        _plexClient.LibraryItems["2"] = [_plexClient.Items["s1"]];
        _nfoWriter.ConcurrencyGate = 2;

        var reconciler = CreateReconciler(new SyncOptions
                                          {
                                              EpisodeReconcileParallelism = 4,
                                              WriteSeriesSeasonAggregates = false
                                          });

        await reconciler.ReconcileLibraryAsync(CreateLibrary("2", MediaKind.Series), _testContext.CancellationToken);

        Assert.HasCount(2, _nfoWriter.WritesOf(MediaKind.Episode), "Both episodes should be written!");
        Assert.AreEqual(1, _nfoWriter.MaxObservedConcurrencyByPath[sharedFile], "Episodes sharing a file should never be written concurrently!");
    }

    /// <summary>
    /// Cancelling the caller's token propagates and is not counted as an error
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task LibraryReconcilerCancellationPropagatesWithoutError()
    {
        AddShow("s1", "e", 4);
        _plexClient.LibraryItems["2"] = [_plexClient.Items["s1"]];
        _nfoWriter.ConcurrencyGate = 3;
        _nfoWriter.ConcurrencyGateTimeout = TimeSpan.FromSeconds(5);
        _nfoWriter.NotifyAtConcurrency = 2;

        var reconciler = CreateReconciler(new SyncOptions
                                          {
                                              EpisodeReconcileParallelism = 2,
                                              WriteSeriesSeasonAggregates = false
                                          });

        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_testContext.CancellationToken);

        var reconcileTask = reconciler.ReconcileLibraryAsync(CreateLibrary("2", MediaKind.Series), cancellation.Token).AsTask();

        await _nfoWriter.ConcurrencyReached.WaitAsync(TimeSpan.FromSeconds(5), _testContext.CancellationToken);
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await reconcileTask,
                                                             "Cancellation should propagate to the caller!");

        Assert.AreEqual(0L, _status.GetSnapshot().Errors, "Cancellation should not be counted as an error!");
    }

    /// <summary>
    /// A failure while enumerating the library propagates, and a movie streamed before it is still written
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task LibraryReconcilerEnumerationFailurePropagatesAfterStreamedMovie()
    {
        AddMovies("1", 3);
        _plexClient.LibraryItemsFailAfter["1"] = (1, new HttpRequestException("page failed"));

        var reconciler = CreateReconciler();

        await Assert.ThrowsAsync<HttpRequestException>(async () => await reconciler.ReconcileLibraryAsync(CreateLibrary("1", MediaKind.Movie), _testContext.CancellationToken).AsTask(),
                                                       "An enumeration failure should propagate to the caller!");

        Assert.HasCount(1, _nfoWriter.WritesOf(MediaKind.Movie), "The movie streamed before the failure should still be written!");
        Assert.AreEqual(0L, _status.GetSnapshot().Errors, "The reconciler should leave the run-level error handling to its caller!");
    }

    /// <summary>
    /// A series reconcile with aggregates lists the episodes of each show exactly once
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task LibraryReconcilerSeriesReconcileRequestsEpisodesOncePerShow()
    {
        AddShow("s1", "e", 3);
        AddShow("s2", "f", 2);
        _plexClient.LibraryItems["2"] = [_plexClient.Items["s1"], _plexClient.Items["s2"]];

        var reconciler = CreateReconciler(new SyncOptions
                                          {
                                              WriteSeriesSeasonAggregates = true
                                          });

        await reconciler.ReconcileLibraryAsync(CreateLibrary("2", MediaKind.Series), _testContext.CancellationToken);

        Assert.AreSequenceEqual(new List<string>
                                {
                                    "s1",
                                    "s2"
                                },
                                _plexClient.EpisodeRequests,
                                "Each show's episodes should be listed exactly once!");
        Assert.HasCount(5, _nfoWriter.WritesOf(MediaKind.Episode), "Every episode should be written!");
        Assert.HasCount(2, _nfoWriter.WritesOf(MediaKind.Season), "One season per show should be written!");
        Assert.HasCount(2, _nfoWriter.WritesOf(MediaKind.Series), "One series item per show should be written!");
    }

    /// <summary>
    /// A show with more episodes than one Plex page is listed once and aggregated over all of its episodes
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task LibraryReconcilerShowWithMoreThanOnePageAggregatesAllEpisodes()
    {
        AddShow("s1", "e", 201);
        _plexClient.LibraryItems["2"] = [_plexClient.Items["s1"]];

        var episodes = _plexClient.Episodes["s1"];

        for (var index = 0; index < 200; index++)
        {
            SetWatched(episodes[index], true);
        }

        var reconciler = CreateReconciler(new SyncOptions
                                          {
                                              WriteSeriesSeasonAggregates = true
                                          });

        await reconciler.ReconcileLibraryAsync(CreateLibrary("2", MediaKind.Series), _testContext.CancellationToken);

        Assert.HasCount(1, _plexClient.EpisodeRequests, "The episodes should be listed exactly once!");
        Assert.HasCount(201, _nfoWriter.WritesOf(MediaKind.Episode), "Every episode should be written!");
        Assert.IsFalse(_nfoWriter.WritesOf(MediaKind.Season).Single().Item.Watch.Watched, "The 201st episode is unwatched, so the season should be unwatched!");
        Assert.IsFalse(_nfoWriter.WritesOf(MediaKind.Series).Single().Item.Watch.Watched, "The 201st episode is unwatched, so the series should be unwatched!");
    }

    /// <summary>
    /// The aggregates of each show follow the episodes listed for that show in the same run
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task LibraryReconcilerSeriesAggregatesFollowEachShowsOwnListing()
    {
        AddShow("s1", "e", 2);
        AddShow("s2", "f", 3);
        _plexClient.LibraryItems["2"] = [_plexClient.Items["s1"], _plexClient.Items["s2"]];

        var newest = _lastPlayed;

        SetWatched(_plexClient.Episodes["s1"][0], true, _firstPlayed);
        SetWatched(_plexClient.Episodes["s1"][1], true, newest);
        SetWatched(_plexClient.Episodes["s2"][0], true);
        SetWatched(_plexClient.Episodes["s2"][1], true);

        var reconciler = CreateReconciler(new SyncOptions
                                          {
                                              WriteSeriesSeasonAggregates = true
                                          });

        await reconciler.ReconcileLibraryAsync(CreateLibrary("2", MediaKind.Series), _testContext.CancellationToken);

        var first = SeriesWriteOf("s1");
        var second = SeriesWriteOf("s2");

        Assert.AreSequenceEqual(new List<string>
                                {
                                    "s1",
                                    "s2"
                                },
                                _plexClient.EpisodeRequests,
                                "Each show's episodes should be listed exactly once!");
        Assert.IsTrue(first.Item.Watch.Watched, "The fully watched show should be watched!");
        Assert.AreEqual(newest, first.Item.Watch.LastPlayed, "The fully watched show should carry its newest play date!");
        Assert.IsFalse(second.Item.Watch.Watched, "The show with an unwatched episode should be unwatched!");
    }

    /// <summary>
    /// Without the aggregate option the episodes are still listed exactly once and no aggregate is written
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task LibraryReconcilerAggregatesDisabledRequestsEpisodesOnce()
    {
        AddShow("s1", "e", 2);
        _plexClient.LibraryItems["2"] = [_plexClient.Items["s1"]];

        var reconciler = CreateReconciler(new SyncOptions
                                          {
                                              WriteSeriesSeasonAggregates = false
                                          });

        await reconciler.ReconcileLibraryAsync(CreateLibrary("2", MediaKind.Series), _testContext.CancellationToken);

        Assert.AreSequenceEqual(new List<string>
                                {
                                    "s1"
                                },
                                _plexClient.EpisodeRequests,
                                "The episodes should be listed exactly once!");
        Assert.HasCount(2, _nfoWriter.WritesOf(MediaKind.Episode), "Every episode should be written!");
        Assert.IsEmpty(_nfoWriter.WritesOf(MediaKind.Season), "No season item should be written!");
        Assert.IsEmpty(_nfoWriter.WritesOf(MediaKind.Series), "No series item should be written!");
    }

    /// <summary>
    /// A show without episodes writes nothing and is not counted as an error
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task LibraryReconcilerShowWithoutEpisodesWritesNothingAndCountsNoError()
    {
        AddShow("s1", "e", 0);
        _plexClient.LibraryItems["2"] = [_plexClient.Items["s1"]];

        var reconciler = CreateReconciler(new SyncOptions
                                          {
                                              WriteSeriesSeasonAggregates = true
                                          });

        await reconciler.ReconcileLibraryAsync(CreateLibrary("2", MediaKind.Series), _testContext.CancellationToken);

        Assert.IsEmpty(_nfoWriter.Writes, "Nothing should be written for a show without episodes!");
        Assert.AreEqual(0L, _status.GetSnapshot().Errors, "A show without episodes should not be counted as an error!");
    }

    /// <summary>
    /// Episodes without a mapping or a file path are left out of the aggregates of a series reconcile
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task LibraryReconcilerPartlyMappedShowAggregatesOnlyMappedEpisodes()
    {
        AddShow("s1", "e", 4);
        _plexClient.LibraryItems["2"] = [_plexClient.Items["s1"]];

        var episodes = _plexClient.Episodes["s1"];

        _pathMapper.Unmapped.Add(episodes[0].FilePath!);
        episodes[1].FilePath = null;
        SetWatched(episodes[2], true);
        SetWatched(episodes[3], true);

        var reconciler = CreateReconciler(new SyncOptions
                                          {
                                              WriteSeriesSeasonAggregates = true
                                          });

        await reconciler.ReconcileLibraryAsync(CreateLibrary("2", MediaKind.Series), _testContext.CancellationToken);

        Assert.HasCount(1, _plexClient.EpisodeRequests, "The episodes should be listed exactly once!");
        Assert.HasCount(2, _nfoWriter.WritesOf(MediaKind.Episode), "Only the mapped episodes should be written!");
        Assert.IsTrue(_nfoWriter.WritesOf(MediaKind.Season).Single().Item.Watch.Watched, "Only the mapped, watched episodes should count for the season!");
        Assert.IsTrue(SeriesWriteOf("s1").Item.Watch.Watched, "Only the mapped, watched episodes should count for the series!");
        Assert.AreEqual(0L, _status.GetSnapshot().Errors, "Unmapped episodes should not be counted as errors!");
    }

    /// <summary>
    /// A show whose episode listing fails writes no aggregate, is not listed again and the other show is unaffected
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task LibraryReconcilerFailingEpisodeListingWritesNoAggregateAndIsNotRequestedAgain()
    {
        AddShow("s1", "e", 2);
        AddShow("s2", "f", 2);
        _plexClient.LibraryItems["2"] = [_plexClient.Items["s1"], _plexClient.Items["s2"]];
        _plexClient.EpisodesExceptions["s1"] = new HttpRequestException("plex down");
        _status.Update(s => s.PlexConnected = true);

        var reconciler = CreateReconciler(new SyncOptions
                                          {
                                              WriteSeriesSeasonAggregates = true
                                          });

        await reconciler.ReconcileLibraryAsync(CreateLibrary("2", MediaKind.Series), _testContext.CancellationToken);

        var snapshot = _status.GetSnapshot();
        var series = _nfoWriter.WritesOf(MediaKind.Series);

        Assert.AreSequenceEqual(new List<string>
                                {
                                    "s1",
                                    "s2"
                                },
                                _plexClient.EpisodeRequests,
                                "The failing listing should not be requested again!");
        Assert.AreEqual(1L, snapshot.Errors, "The failing show should be counted once!");
        Assert.IsTrue(snapshot.PlexConnected, "A failing show should not change the connection state!");
        Assert.HasCount(1, series, "Only the healthy show should get a series item!");
        Assert.AreEqual("s2", series[0].Item.RatingKey, "The series item should belong to the healthy show!");
    }

    /// <summary>
    /// A failing episode write is counted once and the episode still counts in the aggregates
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task LibraryReconcilerFailingEpisodeWriteStillAggregatesItsWatchState()
    {
        AddShow("s1", "e", 3);
        _plexClient.LibraryItems["2"] = [_plexClient.Items["s1"]];
        SetWatched(_plexClient.Episodes["s1"][0], true);
        SetWatched(_plexClient.Episodes["s1"][2], true);
        _nfoWriter.FailuresByRatingKey["e2"] = new IOException("disk full");

        var reconciler = CreateReconciler(new SyncOptions
                                          {
                                              WriteSeriesSeasonAggregates = true
                                          });

        await reconciler.ReconcileLibraryAsync(CreateLibrary("2", MediaKind.Series), _testContext.CancellationToken);

        Assert.AreEqual(1L, _status.GetSnapshot().Errors, "The failing episode should be counted once!");
        Assert.HasCount(2, _nfoWriter.WritesOf(MediaKind.Episode), "The remaining episodes should still be written!");
        Assert.IsFalse(SeriesWriteOf("s1").Item.Watch.Watched, "The unwatched episode with the failing write should still count for the series!");
    }

    /// <summary>
    /// A failing aggregate write is counted once and keeps the episodes that were written
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task LibraryReconcilerFailingAggregateWriteKeepsEpisodesAndCountsOneError()
    {
        AddShow("s1", "e", 3);
        _plexClient.LibraryItems["2"] = [_plexClient.Items["s1"]];
        _nfoWriter.FailuresByRatingKey["s1"] = new IOException("disk full");

        var reconciler = CreateReconciler(new SyncOptions
                                          {
                                              WriteSeriesSeasonAggregates = true
                                          });

        await reconciler.ReconcileLibraryAsync(CreateLibrary("2", MediaKind.Series), _testContext.CancellationToken);

        Assert.HasCount(3, _nfoWriter.WritesOf(MediaKind.Episode), "All episodes should be written!");
        Assert.AreEqual(1L, _status.GetSnapshot().Errors, "The failing aggregate write should be counted once!");
    }

    /// <summary>
    /// Episodes sharing one file are all written and all count in the aggregates
    /// </summary>
    /// <param name="allWatched">Whether the episode on a separate file is watched as well</param>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task LibraryReconcilerSharedFileEpisodesCountInAggregates(bool allWatched)
    {
        AddShow("s1", "e", 3);
        _plexClient.LibraryItems["2"] = [_plexClient.Items["s1"]];

        var episodes = _plexClient.Episodes["s1"];

        episodes[1].FilePath = episodes[0].FilePath;
        SetWatched(episodes[0], true);
        SetWatched(episodes[1], true);
        SetWatched(episodes[2], allWatched);

        var reconciler = CreateReconciler(new SyncOptions
                                          {
                                              WriteSeriesSeasonAggregates = true
                                          });

        await reconciler.ReconcileLibraryAsync(CreateLibrary("2", MediaKind.Series), _testContext.CancellationToken);

        Assert.HasCount(3, _nfoWriter.WritesOf(MediaKind.Episode), "Both shared-file episodes and the separate one should be written!");
        Assert.AreEqual(allWatched, SeriesWriteOf("s1").Item.Watch.Watched, "Every episode, shared-file ones included, should count for the series!");
    }

    /// <summary>
    /// Cancelling the caller's token with aggregates enabled propagates and is not counted as an error
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task LibraryReconcilerCancellationWithAggregatesPropagatesWithoutError()
    {
        AddShow("s1", "e", 4);
        _plexClient.LibraryItems["2"] = [_plexClient.Items["s1"]];
        _nfoWriter.ConcurrencyGate = 3;
        _nfoWriter.ConcurrencyGateTimeout = TimeSpan.FromSeconds(5);
        _nfoWriter.NotifyAtConcurrency = 2;

        var reconciler = CreateReconciler(new SyncOptions
                                          {
                                              EpisodeReconcileParallelism = 2,
                                              WriteSeriesSeasonAggregates = true
                                          });

        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_testContext.CancellationToken);

        var reconcileTask = reconciler.ReconcileLibraryAsync(CreateLibrary("2", MediaKind.Series), cancellation.Token).AsTask();

        await _nfoWriter.ConcurrencyReached.WaitAsync(TimeSpan.FromSeconds(5), _testContext.CancellationToken);
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await reconcileTask,
                                                             "Cancellation should propagate to the caller!");

        Assert.AreEqual(0L, _status.GetSnapshot().Errors, "Cancellation should not be counted as an error!");
        Assert.IsEmpty(_nfoWriter.WritesOf(MediaKind.Series), "No series item should be written after the cancellation!");
    }

    /// <summary>
    /// Create a library
    /// </summary>
    /// <param name="key">Section key</param>
    /// <param name="kind">Library kind</param>
    /// <returns>The library</returns>
    private static PlexLibrary CreateLibrary(string key, MediaKind kind)
    {
        return new PlexLibrary
               {
                   Key = key,
                   Title = "Library " + key,
                   Kind = kind
               };
    }

    /// <summary>
    /// Set the watch state of an episode
    /// </summary>
    /// <param name="episode">Episode</param>
    /// <param name="watched">Whether the episode is watched</param>
    /// <param name="lastPlayed">Last play date of a watched episode, or <c>null</c> for the default</param>
    private static void SetWatched(MediaItem episode, bool watched, DateTimeOffset? lastPlayed = null)
    {
        episode.Watch = new WatchInfo
                        {
                            Watched = watched,
                            PlayCount = watched ? 1 : 0,
                            LastPlayed = watched ? lastPlayed ?? _firstPlayed : null
                        };
    }

    /// <summary>
    /// Get the written series item of a show
    /// </summary>
    /// <param name="showRatingKey">Rating key of the show</param>
    /// <returns>The write of the series item of the show</returns>
    private NfoWriteRecord SeriesWriteOf(string showRatingKey)
    {
        return _nfoWriter.WritesOf(MediaKind.Series).Single(write => write.Item.RatingKey == showRatingKey);
    }

    /// <summary>
    /// Create the reconciler under test wired to the current test doubles and real collaborators
    /// </summary>
    /// <param name="syncOptions">Sync options, or <c>null</c> for the defaults</param>
    /// <returns>The reconciler under test</returns>
    private LibraryReconciler CreateReconciler(SyncOptions? syncOptions = null)
    {
        var itemWriter = new MediaItemWriter(_nfoWriter, _pathMapper, _status, NullLogger<MediaItemWriter>.Instance);
        var aggregateWriter = new SeriesAggregateWriter(_plexClient, _pathMapper, itemWriter, new WatchAggregator());

        return new LibraryReconciler(_plexClient,
                                     itemWriter,
                                     aggregateWriter,
                                     _status,
                                     Options.Create(syncOptions ?? new SyncOptions()),
                                     NullLogger<LibraryReconciler>.Instance);
    }

    /// <summary>
    /// Register movies in a library
    /// </summary>
    /// <param name="libraryKey">Section key</param>
    /// <param name="count">Number of movies</param>
    private void AddMovies(string libraryKey, int count)
    {
        var movies = new List<MediaItem>();

        for (var number = 1; number <= count; number++)
        {
            movies.Add(new MediaItem
                       {
                           RatingKey = $"m{number}",
                           Kind = MediaKind.Movie,
                           Title = $"Movie {number}",
                           FilePath = $"/data/Movies/Movie {number}/Movie {number}.mkv"
                       });
        }

        _plexClient.LibraryItems[libraryKey] = movies;
    }

    /// <summary>
    /// Register a show with its episodes
    /// </summary>
    /// <param name="showRatingKey">Rating key of the show</param>
    /// <param name="episodePrefix">Prefix of the episode rating keys</param>
    /// <param name="episodeCount">Number of episodes</param>
    private void AddShow(string showRatingKey, string episodePrefix, int episodeCount)
    {
        var episodes = new List<MediaItem>();

        for (var number = 1; number <= episodeCount; number++)
        {
            episodes.Add(new MediaItem
                         {
                             RatingKey = $"{episodePrefix}{number}",
                             Kind = MediaKind.Episode,
                             Title = $"Episode {number}",
                             SeasonNumber = 1,
                             EpisodeNumber = number,
                             ShowRatingKey = showRatingKey,
                             ShowTitle = "Breaking Bad",
                             FilePath = $"{_seasonDirectory}{Path.DirectorySeparatorChar}{showRatingKey}S01E{number:D2}.mkv"
                         });
        }

        _plexClient.Items[showRatingKey] = new MediaItem
                                           {
                                               RatingKey = showRatingKey,
                                               Kind = MediaKind.Series,
                                               Title = "Breaking Bad"
                                           };
        _plexClient.Episodes[showRatingKey] = episodes;
    }

    #endregion // Methods
}