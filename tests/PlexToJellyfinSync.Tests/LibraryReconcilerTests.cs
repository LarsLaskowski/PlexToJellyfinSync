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

        var reconciler = CreateReconciler(new SyncOptions { WriteSeriesSeasonAggregates = true });

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

        var reconciler = CreateReconciler(new SyncOptions { WriteSeriesSeasonAggregates = false });

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

        var reconciler = CreateReconciler();

        await reconciler.ReconcileLibraryAsync(CreateLibrary("1", MediaKind.Movie), _testContext.CancellationToken);

        var snapshot = _status.GetSnapshot();

        Assert.HasCount(2, _nfoWriter.WritesOf(MediaKind.Movie), "The remaining movies should still be written!");
        Assert.AreEqual(1L, snapshot.Errors, "The failure should be counted!");
        Assert.AreEqual("disk full", snapshot.LastError, "The last error should be set!");
        Assert.IsFalse(snapshot.PlexConnected, "A failing item should not change the connection state!");
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

        var reconciler = CreateReconciler(new SyncOptions { WriteSeriesSeasonAggregates = false });

        await reconciler.ReconcileLibraryAsync(CreateLibrary("2", MediaKind.Series), _testContext.CancellationToken);

        var snapshot = _status.GetSnapshot();

        Assert.HasCount(2, _nfoWriter.WritesOf(MediaKind.Episode), "The remaining episodes should still be written!");
        Assert.AreEqual(1L, snapshot.Errors, "The failure should be counted!");
        Assert.AreEqual("disk full", snapshot.LastError, "The last error should be set!");
        Assert.IsFalse(snapshot.PlexConnected, "A failing item should not change the connection state!");
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

        var reconciler = CreateReconciler(new SyncOptions { WriteSeriesSeasonAggregates = false });

        await reconciler.ReconcileLibraryAsync(CreateLibrary("2", MediaKind.Series), _testContext.CancellationToken);

        var snapshot = _status.GetSnapshot();
        var written = _nfoWriter.WritesOf(MediaKind.Episode);

        Assert.HasCount(2, written, "The episodes of the other show should still be written!");
        Assert.IsTrue(written.All(write => write.Item.ShowRatingKey == "s2"), "Only the episodes of the healthy show should be written!");
        Assert.AreEqual(1L, snapshot.Errors, "The failing show should be counted once!");
        Assert.AreEqual("plex down", snapshot.LastError, "The last error should be set!");
        Assert.IsFalse(snapshot.PlexConnected, "A failing show should not change the connection state!");
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

        var reconciler = CreateReconciler(new SyncOptions { EpisodeReconcileParallelism = 2, WriteSeriesSeasonAggregates = false });

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

        var reconciler = CreateReconciler(new SyncOptions { EpisodeReconcileParallelism = parallelism, WriteSeriesSeasonAggregates = false });

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
        var sharedFile = _seasonDirectory + Path.DirectorySeparatorChar + "S01E01-E02.mkv";
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

        _plexClient.Items["s1"] = new MediaItem { RatingKey = "s1", Kind = MediaKind.Series, Title = "Breaking Bad" };
        _plexClient.Episodes["s1"] = episodes;
        _plexClient.LibraryItems["2"] = [_plexClient.Items["s1"]];
        _nfoWriter.ConcurrencyGate = 2;

        var reconciler = CreateReconciler(new SyncOptions { EpisodeReconcileParallelism = 4, WriteSeriesSeasonAggregates = false });

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

        var reconciler = CreateReconciler(new SyncOptions { EpisodeReconcileParallelism = 2, WriteSeriesSeasonAggregates = false });

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
                             FilePath = _seasonDirectory + Path.DirectorySeparatorChar + $"{showRatingKey}S01E{number:D2}.mkv"
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