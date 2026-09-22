using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using PlexToJellyfinSync.Core.Enums;
using PlexToJellyfinSync.Core.Models;
using PlexToJellyfinSync.Core.Options;
using PlexToJellyfinSync.Service;

namespace PlexToJellyfinSync.Tests;

/// <summary>
/// Tests for <see cref="SyncOrchestrator"/>
/// </summary>
[TestClass]
public sealed class SyncOrchestratorTests
{
    #region Fields

    private static readonly DateTimeOffset _since = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly string _seasonDirectory = "/data/Shows/Breaking Bad/Season 01".Replace('/', Path.DirectorySeparatorChar);
    private readonly string _showDirectory = "/data/Shows/Breaking Bad".Replace('/', Path.DirectorySeparatorChar);

    private FakePlexClient _plexClient = new();
    private RecordingNfoWriter _nfoWriter = new();
    private StubPathMapper _pathMapper = new();
    private FakeStateStore _stateStore = new();
    private SyncStatusService _status = new();

    #endregion // Fields

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
        _stateStore = new FakeStateStore();
        _status = new SyncStatusService();
    }

    /// <summary>
    /// The very first run only seeds the high-water mark and does not read the history
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task SyncOrchestratorFirstRunSeedsHighWaterMark()
    {
        var orchestrator = CreateOrchestrator();

        await orchestrator.ProcessHistoryAsync(CancellationToken.None);

        var snapshot = _status.GetSnapshot();

        Assert.IsNotNull(_stateStore.HighWaterMark, "The first run should seed the high-water mark!");
        Assert.HasCount(1, _stateStore.Writes, "The first run should persist the high-water mark exactly once!");
        Assert.IsNull(_plexClient.LastHistorySince, "The first run should not read the watch history!");
        Assert.IsEmpty(_nfoWriter.Writes, "The first run should not write any NFO file!");
        Assert.IsTrue(snapshot.PlexConnected, "The first run should mark Plex as connected!");
        Assert.IsNotNull(snapshot.LastPollAt, "The first run should record the poll timestamp!");
        Assert.IsFalse(snapshot.IsRunning, "The run flag should be cleared afterwards!");
    }

    /// <summary>
    /// The persisted high-water mark and the resolved owner id are passed to the Plex client
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task SyncOrchestratorProcessHistoryQueriesPlexWithWatermarkAndOwner()
    {
        _plexClient.OwnerAccountId = 7;
        _stateStore.HighWaterMark = _since;

        var orchestrator = CreateOrchestrator();

        await orchestrator.ProcessHistoryAsync(CancellationToken.None);

        Assert.AreEqual(_since, _plexClient.LastHistorySince, "The persisted high-water mark should be used as lower bound!");
        Assert.AreEqual(7, _plexClient.LastHistoryAccountId, "The resolved owner account id should be used!");
    }

    /// <summary>
    /// A watched movie is written and the high-water mark advances to the newest entry
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task SyncOrchestratorProcessHistoryWritesMovieAndAdvancesWatermark()
    {
        _stateStore.HighWaterMark = _since;

        AddMovie("m1", "Heat", "/data/Movies/Heat (1995)/Heat.mkv");
        AddMovie("m2", "Alien", "/data/Movies/Alien (1979)/Alien.mkv");
        AddHistory("m1", _since.AddMinutes(10));
        AddHistory("m2", _since.AddMinutes(30));

        var orchestrator = CreateOrchestrator();

        await orchestrator.ProcessHistoryAsync(CancellationToken.None);

        var snapshot = _status.GetSnapshot();

        Assert.HasCount(2, _nfoWriter.Writes, "Both watched movies should have been written!");
        Assert.AreEqual("/data/Movies/Heat (1995)/Heat.mkv", _nfoWriter.Writes[0].LocalPath, "The mapped movie path should be used!");
        Assert.AreEqual(_since.AddMinutes(30), _stateStore.HighWaterMark, "The high-water mark should advance to the newest entry!");
        Assert.AreEqual(_since.AddMinutes(30), snapshot.HighWaterMark, "The status should report the new high-water mark!");
        Assert.AreEqual(2L, snapshot.ItemsProcessed, "Both items should be counted as processed!");
        Assert.AreEqual(2L, snapshot.NfoCreated, "Both writes should be counted as created!");
        Assert.AreEqual(0L, snapshot.Errors, "A successful run should not record an error!");
    }

    /// <summary>
    /// Updated NFO files are counted separately from created ones
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task SyncOrchestratorProcessHistoryCountsUpdatedNfoFiles()
    {
        _stateStore.HighWaterMark = _since;
        _nfoWriter.Outcome = NfoWriteOutcome.Updated;

        AddMovie("m1", "Heat", "/data/Movies/Heat (1995)/Heat.mkv");
        AddHistory("m1", _since.AddMinutes(10));

        var orchestrator = CreateOrchestrator();

        await orchestrator.ProcessHistoryAsync(CancellationToken.None);

        var snapshot = _status.GetSnapshot();

        Assert.AreEqual(1L, snapshot.NfoUpdated, "The update should be counted!");
        Assert.AreEqual(0L, snapshot.NfoCreated, "An update should not be counted as creation!");
    }

    /// <summary>
    /// A skipped NFO write is counted neither as created nor as updated
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task SyncOrchestratorProcessHistorySkippedWriteIsNotCounted()
    {
        _stateStore.HighWaterMark = _since;
        _nfoWriter.Outcome = NfoWriteOutcome.Skipped;

        AddMovie("m1", "Heat", "/data/Movies/Heat (1995)/Heat.mkv");
        AddHistory("m1", _since.AddMinutes(10));

        var orchestrator = CreateOrchestrator();

        await orchestrator.ProcessHistoryAsync(CancellationToken.None);

        var snapshot = _status.GetSnapshot();

        Assert.AreEqual(0L, snapshot.NfoCreated, "A skipped write should not be counted as creation!");
        Assert.AreEqual(0L, snapshot.NfoUpdated, "A skipped write should not be counted as update!");
        Assert.AreEqual(1L, snapshot.ItemsProcessed, "The item should still be counted as processed!");
    }

    /// <summary>
    /// Without new history entries the persisted high-water mark stays untouched
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task SyncOrchestratorProcessHistoryWithoutEntriesKeepsWatermark()
    {
        _stateStore.HighWaterMark = _since;

        AddMovie("m1", "Heat", "/data/Movies/Heat (1995)/Heat.mkv");
        AddHistory("m1", _since.AddMinutes(-10));

        var orchestrator = CreateOrchestrator();

        await orchestrator.ProcessHistoryAsync(CancellationToken.None);

        Assert.IsEmpty(_stateStore.Writes, "An empty history should not move the high-water mark!");
        Assert.IsEmpty(_nfoWriter.Writes, "An empty history should not write any NFO file!");
        Assert.IsNotNull(_status.GetSnapshot().LastPollAt, "The poll timestamp should be recorded anyway!");
    }

    /// <summary>
    /// An item without a file path is counted but not written
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task SyncOrchestratorProcessHistoryItemWithoutFilePathSkipsWrite()
    {
        _stateStore.HighWaterMark = _since;

        AddMovie("m1", "Heat", string.Empty);
        AddHistory("m1", _since.AddMinutes(10));

        var orchestrator = CreateOrchestrator();

        await orchestrator.ProcessHistoryAsync(CancellationToken.None);

        Assert.IsEmpty(_nfoWriter.Writes, "An item without a file path should not be written!");
        Assert.AreEqual(1L, _status.GetSnapshot().ItemsProcessed, "The item should still be counted as processed!");
    }

    /// <summary>
    /// An item whose path has no mapping is counted but not written
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task SyncOrchestratorProcessHistoryWithoutPathMappingSkipsWrite()
    {
        _stateStore.HighWaterMark = _since;

        AddMovie("m1", "Heat", "/unmapped/Heat.mkv");
        AddHistory("m1", _since.AddMinutes(10));
        _pathMapper.Unmapped.Add("/unmapped/Heat.mkv");

        var orchestrator = CreateOrchestrator();

        await orchestrator.ProcessHistoryAsync(CancellationToken.None);

        Assert.IsEmpty(_nfoWriter.Writes, "An unmapped item should not be written!");
        Assert.AreEqual(1L, _status.GetSnapshot().ItemsProcessed, "The item should still be counted as processed!");
    }

    /// <summary>
    /// A history entry whose item no longer exists is skipped without counting it
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task SyncOrchestratorProcessHistoryWithUnknownRatingKeySkipsItem()
    {
        _stateStore.HighWaterMark = _since;

        AddHistory("gone", _since.AddMinutes(10));

        var orchestrator = CreateOrchestrator();

        await orchestrator.ProcessHistoryAsync(CancellationToken.None);

        Assert.IsEmpty(_nfoWriter.Writes, "A missing item should not be written!");
        Assert.AreEqual(0L, _status.GetSnapshot().ItemsProcessed, "A missing item should not be counted as processed!");
        Assert.AreEqual(_since.AddMinutes(10), _stateStore.HighWaterMark, "The high-water mark should still advance past the entry!");
    }

    /// <summary>
    /// A watched episode also refreshes the aggregated season and series NFO files
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task SyncOrchestratorProcessHistoryEpisodeWritesSeasonAndSeriesAggregates()
    {
        _stateStore.HighWaterMark = _since;

        AddSeriesWithTwoEpisodes("s1");
        AddHistory("e1", _since.AddMinutes(10));

        var orchestrator = CreateOrchestrator();

        await orchestrator.ProcessHistoryAsync(CancellationToken.None);

        var seasons = _nfoWriter.WritesOf(MediaKind.Season);
        var series = _nfoWriter.WritesOf(MediaKind.Series);

        Assert.HasCount(3, _nfoWriter.Writes, "Episode, season and series should have been written!");
        Assert.HasCount(1, seasons, "Exactly one season aggregate should have been written!");
        Assert.AreEqual(_seasonDirectory, seasons[0].LocalPath, "The season aggregate should target the season directory!");
        Assert.AreEqual(1, seasons[0].Item.SeasonNumber, "The season number should be taken from the episodes!");
        Assert.IsFalse(seasons[0].Item.Watch.Watched, "A season with an unwatched episode should not be watched!");
        Assert.HasCount(1, series, "Exactly one series aggregate should have been written!");
        Assert.AreEqual(_showDirectory, series[0].LocalPath, "The series aggregate should target the show directory!");
        Assert.IsFalse(series[0].Item.Watch.Watched, "A series with an unwatched episode should not be watched!");
    }

    /// <summary>
    /// A series whose episodes are all watched is aggregated as watched
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task SyncOrchestratorProcessHistoryFullyWatchedSeriesAggregatesAsWatched()
    {
        _stateStore.HighWaterMark = _since;

        AddSeriesWithTwoEpisodes("s1");
        _plexClient.Items["e2"].Watch = new WatchInfo
                                        {
                                            Watched = true,
                                            PlayCount = 1,
                                            LastPlayed = _since.AddMinutes(20)
                                        };

        AddHistory("e1", _since.AddMinutes(10));

        var orchestrator = CreateOrchestrator();

        await orchestrator.ProcessHistoryAsync(CancellationToken.None);

        var series = _nfoWriter.WritesOf(MediaKind.Series);

        Assert.HasCount(1, series, "Exactly one series aggregate should have been written!");
        Assert.IsTrue(series[0].Item.Watch.Watched, "A fully watched series should be aggregated as watched!");
        Assert.AreEqual(_since.AddMinutes(20), series[0].Item.Watch.LastPlayed, "The newest playback timestamp should win!");
    }

    /// <summary>
    /// Each affected series is aggregated only once even with several history entries
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task SyncOrchestratorProcessHistoryAggregatesEachSeriesOnlyOnce()
    {
        _stateStore.HighWaterMark = _since;

        AddSeriesWithTwoEpisodes("s1");
        AddHistory("e1", _since.AddMinutes(10));
        AddHistory("e2", _since.AddMinutes(20));

        var orchestrator = CreateOrchestrator();

        await orchestrator.ProcessHistoryAsync(CancellationToken.None);

        Assert.HasCount(1, _plexClient.EpisodeRequests, "The episodes of a series should be fetched only once per run!");
        Assert.HasCount(1, _nfoWriter.WritesOf(MediaKind.Series), "The series aggregate should be written only once!");
    }

    /// <summary>
    /// With aggregates disabled only the episode itself is written
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task SyncOrchestratorProcessHistoryWithAggregatesDisabledSkipsAggregates()
    {
        _stateStore.HighWaterMark = _since;

        AddSeriesWithTwoEpisodes("s1");
        AddHistory("e1", _since.AddMinutes(10));

        var orchestrator = CreateOrchestrator(new SyncOptions
                                              {
                                                  WriteSeriesSeasonAggregates = false
                                              });

        await orchestrator.ProcessHistoryAsync(CancellationToken.None);

        Assert.HasCount(1, _nfoWriter.Writes, "Only the episode should have been written!");
        Assert.AreEqual(MediaKind.Episode, _nfoWriter.Writes[0].Item.Kind, "The written item should be the episode!");
        Assert.IsEmpty(_plexClient.EpisodeRequests, "Disabled aggregates should not fetch the episodes of the series!");
    }

    /// <summary>
    /// A failing Plex request is recorded as an error instead of being propagated
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task SyncOrchestratorProcessHistoryFailureRecordsError()
    {
        _stateStore.HighWaterMark = _since;
        _plexClient.HistoryException = new InvalidOperationException("plex is down");

        var orchestrator = CreateOrchestrator();

        await orchestrator.ProcessHistoryAsync(CancellationToken.None);

        var snapshot = _status.GetSnapshot();

        Assert.AreEqual(1L, snapshot.Errors, "The failure should be counted!");
        Assert.AreEqual("plex is down", snapshot.LastError, "The failure message should be recorded!");
        Assert.IsFalse(snapshot.PlexConnected, "A failure should mark Plex as disconnected!");
        Assert.IsFalse(snapshot.IsRunning, "The run flag should be cleared after a failure!");
        Assert.IsEmpty(_stateStore.Writes, "A failed run should not move the high-water mark!");
    }

    /// <summary>
    /// An HttpClient timeout on the upfront history request is treated as a normal sync failure rather than
    /// genuine cancellation, even though it also throws <see cref="OperationCanceledException"/>
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task SyncOrchestratorProcessHistoryUpfrontTimeoutRecordsErrorInsteadOfPropagating()
    {
        _stateStore.HighWaterMark = _since;
        _plexClient.HistoryException = new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout");

        var orchestrator = CreateOrchestrator();

        await orchestrator.ProcessHistoryAsync(CancellationToken.None);

        var snapshot = _status.GetSnapshot();

        Assert.AreEqual(1L, snapshot.Errors, "An upfront timeout should be counted as a sync failure, not propagated as cancellation!");
        Assert.IsFalse(snapshot.PlexConnected, "An upfront timeout should mark Plex as disconnected!");
        Assert.IsFalse(snapshot.IsRunning, "The run flag should be cleared after the timeout!");
    }

    /// <summary>
    /// A single item that fails to process does not abort the cycle, is counted as an error and does not block the
    /// remaining items or the high-water mark from advancing
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task SyncOrchestratorProcessHistoryItemFailureContinuesAndAdvancesWatermark()
    {
        _stateStore.HighWaterMark = _since;

        AddMovie("m1", "Heat", "/data/Movies/Heat (1995)/Heat.mkv");
        AddMovie("m2", "Alien", "/data/Movies/Alien (1979)/Alien.mkv");
        AddHistory("m1", _since.AddMinutes(10));
        AddHistory("m2", _since.AddMinutes(30));

        _nfoWriter.FailuresByRatingKey["m1"] = new IOException("disk full");

        var orchestrator = CreateOrchestrator();

        await orchestrator.ProcessHistoryAsync(CancellationToken.None);

        var snapshot = _status.GetSnapshot();

        Assert.HasCount(1, _nfoWriter.Writes, "Only the item after the failing one should have been written!");
        Assert.AreEqual("Alien", _nfoWriter.Writes[0].Item.Title, "The item after the failing one should still be written!");
        Assert.AreEqual(1L, snapshot.Errors, "The per-item failure should be counted as an error!");
        Assert.AreEqual("disk full", snapshot.LastError, "The failure message should be recorded!");
        Assert.IsTrue(snapshot.PlexConnected, "A per-item failure should not mark Plex as disconnected!");
        Assert.AreEqual(_since.AddMinutes(30), _stateStore.HighWaterMark, "The high-water mark should advance past the failing item!");
        Assert.AreEqual(_since.AddMinutes(30), snapshot.HighWaterMark, "The status should report the advanced high-water mark!");
        Assert.IsFalse(snapshot.IsRunning, "The run flag should be cleared afterwards!");
    }

    /// <summary>
    /// An HttpClient timeout on a single item is treated as an item failure rather than genuine cancellation,
    /// even though it also throws <see cref="OperationCanceledException"/>
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task SyncOrchestratorProcessHistoryItemTimeoutIsTreatedAsItemFailure()
    {
        _stateStore.HighWaterMark = _since;

        AddMovie("m1", "Heat", "/data/Movies/Heat (1995)/Heat.mkv");
        AddMovie("m2", "Alien", "/data/Movies/Alien (1979)/Alien.mkv");
        AddHistory("m1", _since.AddMinutes(10));
        AddHistory("m2", _since.AddMinutes(30));

        _nfoWriter.FailuresByRatingKey["m1"] = new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout");

        var orchestrator = CreateOrchestrator();

        await orchestrator.ProcessHistoryAsync(CancellationToken.None);

        var snapshot = _status.GetSnapshot();

        Assert.HasCount(1, _nfoWriter.Writes, "The item after the timed-out one should still be written!");
        Assert.AreEqual("Alien", _nfoWriter.Writes[0].Item.Title, "The item after the timed-out one should still be written!");
        Assert.AreEqual(1L, snapshot.Errors, "An item timeout should be counted as an item failure, not propagated as cancellation!");
        Assert.AreEqual(_since.AddMinutes(30), _stateStore.HighWaterMark, "The high-water mark should advance past the timed-out item!");
    }

    /// <summary>
    /// A failing series aggregate write does not abort the cycle or block the high-water mark from advancing
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task SyncOrchestratorProcessHistoryAggregateFailureContinuesAndAdvancesWatermark()
    {
        _stateStore.HighWaterMark = _since;

        AddSeriesWithTwoEpisodes("s1");
        AddHistory("e1", _since.AddMinutes(10));

        _nfoWriter.FailuresByRatingKey["s1"] = new IOException("disk full");

        var orchestrator = CreateOrchestrator();

        await orchestrator.ProcessHistoryAsync(CancellationToken.None);

        var snapshot = _status.GetSnapshot();

        Assert.HasCount(1, _nfoWriter.WritesOf(MediaKind.Season), "The season aggregate should still be written despite the series aggregate failing!");
        Assert.IsEmpty(_nfoWriter.WritesOf(MediaKind.Series), "The failing series aggregate should not be recorded as written!");
        Assert.AreEqual(1L, snapshot.Errors, "The aggregate failure should be counted as an error!");
        Assert.IsTrue(snapshot.PlexConnected, "An aggregate failure should not mark Plex as disconnected!");
        Assert.AreEqual(_since.AddMinutes(10), _stateStore.HighWaterMark, "The high-water mark should still advance despite the aggregate failure!");
        Assert.IsFalse(snapshot.IsRunning, "The run flag should be cleared afterwards!");
    }

    /// <summary>
    /// An HttpClient timeout on a series aggregate write is treated as an item failure rather than genuine
    /// cancellation
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task SyncOrchestratorProcessHistoryAggregateTimeoutContinuesAndAdvancesWatermark()
    {
        _stateStore.HighWaterMark = _since;

        AddSeriesWithTwoEpisodes("s1");
        AddHistory("e1", _since.AddMinutes(10));

        _nfoWriter.FailuresByRatingKey["s1"] = new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout");

        var orchestrator = CreateOrchestrator();

        await orchestrator.ProcessHistoryAsync(CancellationToken.None);

        var snapshot = _status.GetSnapshot();

        Assert.IsEmpty(_nfoWriter.WritesOf(MediaKind.Series), "The timed-out series aggregate should not be recorded as written!");
        Assert.AreEqual(1L, snapshot.Errors, "An aggregate timeout should be counted as an item failure, not propagated as cancellation!");
        Assert.AreEqual(_since.AddMinutes(10), _stateStore.HighWaterMark, "The high-water mark should still advance despite the aggregate timeout!");
    }

    /// <summary>
    /// Cancellation is propagated to the caller and still clears the run flag
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task SyncOrchestratorProcessHistoryCancellationPropagates()
    {
        _stateStore.HighWaterMark = _since;

        AddMovie("m1", "Heat", "/data/Movies/Heat (1995)/Heat.mkv");
        AddHistory("m1", _since.AddMinutes(10));

        using var cancellation = new CancellationTokenSource();

        await cancellation.CancelAsync();

        var orchestrator = CreateOrchestrator();

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await orchestrator.ProcessHistoryAsync(cancellation.Token),
                                                             "Cancellation should be propagated to the caller!");

        var snapshot = _status.GetSnapshot();

        Assert.IsFalse(snapshot.IsRunning, "The run flag should be cleared after cancellation!");
        Assert.AreEqual(0L, snapshot.Errors, "Cancellation should not be recorded as an error!");
        Assert.IsEmpty(_nfoWriter.Writes, "A cancelled run should not write any NFO file!");
    }

    /// <summary>
    /// The owner account id is resolved once and reused across runs
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task SyncOrchestratorResolvesOwnerAccountIdOnlyOnce()
    {
        _stateStore.HighWaterMark = _since;

        var orchestrator = CreateOrchestrator();

        await orchestrator.ProcessHistoryAsync(CancellationToken.None);
        await orchestrator.ProcessHistoryAsync(CancellationToken.None);

        Assert.AreEqual(1, _plexClient.OwnerAccountIdCalls, "The owner account id should be cached after the first resolution!");
    }

    /// <summary>
    /// A reconcile run writes every movie of a movie library
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task SyncOrchestratorReconcileWritesMovieLibraryItems()
    {
        _plexClient.Libraries.Add(new PlexLibrary
                                  {
                                      Key = "1",
                                      Title = "Movies",
                                      Kind = MediaKind.Movie
                                  });

        _plexClient.LibraryItems["1"] = [
                                            AddMovie("m1", "Heat", "/data/Movies/Heat (1995)/Heat.mkv"),
                                            AddMovie("m2", "Alien", "/data/Movies/Alien (1979)/Alien.mkv")
                                        ];

        var orchestrator = CreateOrchestrator();

        await orchestrator.ReconcileAsync(CancellationToken.None);

        var snapshot = _status.GetSnapshot();

        Assert.HasCount(2, _nfoWriter.Writes, "Both movies should have been written!");
        Assert.AreEqual(2L, snapshot.ItemsProcessed, "Both movies should be counted as processed!");
        Assert.IsTrue(snapshot.PlexConnected, "A successful reconcile should mark Plex as connected!");
        Assert.IsNotNull(snapshot.LastReconcileAt, "The reconcile timestamp should be recorded!");
        Assert.IsFalse(snapshot.IsRunning, "The run flag should be cleared afterwards!");
    }

    /// <summary>
    /// A reconcile run writes every episode of a series library plus its aggregates
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task SyncOrchestratorReconcileSeriesLibraryWritesEpisodesAndAggregates()
    {
        _plexClient.Libraries.Add(new PlexLibrary
                                  {
                                      Key = "2",
                                      Title = "Shows",
                                      Kind = MediaKind.Series
                                  });

        AddSeriesWithTwoEpisodes("s1");
        _plexClient.LibraryItems["2"] = [_plexClient.Items["s1"]];

        var orchestrator = CreateOrchestrator();

        await orchestrator.ReconcileAsync(CancellationToken.None);

        Assert.HasCount(2, _nfoWriter.WritesOf(MediaKind.Episode), "Both episodes should have been written!");
        Assert.HasCount(1, _nfoWriter.WritesOf(MediaKind.Season), "The season aggregate should have been written!");
        Assert.HasCount(1, _nfoWriter.WritesOf(MediaKind.Series), "The series aggregate should have been written!");
        Assert.AreEqual(2L, _status.GetSnapshot().ItemsProcessed, "Only the episodes should be counted as processed!");
    }

    /// <summary>
    /// A reconcile run only visits the configured library sections
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task SyncOrchestratorReconcileRespectsLibraryFilter()
    {
        _plexClient.Libraries.Add(new PlexLibrary
                                  {
                                      Key = "1",
                                      Title = "Movies",
                                      Kind = MediaKind.Movie
                                  });

        _plexClient.Libraries.Add(new PlexLibrary
                                  {
                                      Key = "3",
                                      Title = "Home Videos",
                                      Kind = MediaKind.Movie
                                  });

        _plexClient.LibraryItems["1"] = [AddMovie("m1", "Heat", "/data/Movies/Heat (1995)/Heat.mkv")];
        _plexClient.LibraryItems["3"] = [AddMovie("m9", "Birthday", "/data/Home/Birthday.mkv")];

        var orchestrator = CreateOrchestrator(plexOptions: new PlexOptions
                                                           {
                                                               Libraries = ["1"]
                                                           });

        await orchestrator.ReconcileAsync(CancellationToken.None);

        Assert.HasCount(1, _plexClient.LibraryItemRequests, "Only the configured library should be read!");
        Assert.AreEqual("1", _plexClient.LibraryItemRequests[0], "The configured library should be the one that is read!");
        Assert.HasCount(1, _nfoWriter.Writes, "Only the item of the configured library should be written!");
    }

    /// <summary>
    /// A movie that fails to write during reconcile does not abort the rest of the library
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task SyncOrchestratorReconcileMovieFailureContinuesWithRemainingItems()
    {
        _plexClient.Libraries.Add(new PlexLibrary
                                  {
                                      Key = "1",
                                      Title = "Movies",
                                      Kind = MediaKind.Movie
                                  });

        _plexClient.LibraryItems["1"] = [
                                            AddMovie("m1", "Heat", "/data/Movies/Heat (1995)/Heat.mkv"),
                                            AddMovie("m2", "Alien", "/data/Movies/Alien (1979)/Alien.mkv")
                                        ];

        _nfoWriter.FailuresByRatingKey["m1"] = new IOException("disk full");

        var orchestrator = CreateOrchestrator();

        await orchestrator.ReconcileAsync(CancellationToken.None);

        var snapshot = _status.GetSnapshot();

        Assert.HasCount(1, _nfoWriter.Writes, "The movie after the failing one should still be written!");
        Assert.AreEqual("Alien", _nfoWriter.Writes[0].Item.Title, "The movie after the failing one should still be written!");
        Assert.AreEqual(1L, snapshot.Errors, "The per-item failure should be counted as an error!");
        Assert.IsTrue(snapshot.PlexConnected, "A per-item failure should not mark Plex as disconnected!");
        Assert.IsNotNull(snapshot.LastReconcileAt, "The reconcile should still complete and record its timestamp!");
    }

    /// <summary>
    /// An HttpClient timeout on a movie during reconcile is treated as an item failure rather than genuine
    /// cancellation
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task SyncOrchestratorReconcileMovieTimeoutContinuesWithRemainingItems()
    {
        _plexClient.Libraries.Add(new PlexLibrary
                                  {
                                      Key = "1",
                                      Title = "Movies",
                                      Kind = MediaKind.Movie
                                  });

        _plexClient.LibraryItems["1"] = [
                                            AddMovie("m1", "Heat", "/data/Movies/Heat (1995)/Heat.mkv"),
                                            AddMovie("m2", "Alien", "/data/Movies/Alien (1979)/Alien.mkv")
                                        ];

        _nfoWriter.FailuresByRatingKey["m1"] = new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout");

        var orchestrator = CreateOrchestrator();

        await orchestrator.ReconcileAsync(CancellationToken.None);

        var snapshot = _status.GetSnapshot();

        Assert.HasCount(1, _nfoWriter.Writes, "The movie after the timed-out one should still be written!");
        Assert.AreEqual("Alien", _nfoWriter.Writes[0].Item.Title, "The movie after the timed-out one should still be written!");
        Assert.AreEqual(1L, snapshot.Errors, "A movie timeout should be counted as an item failure, not propagated as cancellation!");
    }

    /// <summary>
    /// An episode that fails to write during reconcile does not abort the rest of the series library
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task SyncOrchestratorReconcileEpisodeFailureContinuesWithRemainingEpisodesAndAggregates()
    {
        _plexClient.Libraries.Add(new PlexLibrary
                                  {
                                      Key = "2",
                                      Title = "Shows",
                                      Kind = MediaKind.Series
                                  });

        AddSeriesWithTwoEpisodes("s1");
        _plexClient.LibraryItems["2"] = [_plexClient.Items["s1"]];
        _nfoWriter.FailuresByRatingKey["e1"] = new IOException("disk full");

        var orchestrator = CreateOrchestrator();

        await orchestrator.ReconcileAsync(CancellationToken.None);

        var snapshot = _status.GetSnapshot();

        Assert.HasCount(1, _nfoWriter.WritesOf(MediaKind.Episode), "The episode after the failing one should still be written!");
        Assert.HasCount(1, _nfoWriter.WritesOf(MediaKind.Season), "The season aggregate should still be written despite the episode failure!");
        Assert.HasCount(1, _nfoWriter.WritesOf(MediaKind.Series), "The series aggregate should still be written despite the episode failure!");
        Assert.AreEqual(1L, snapshot.Errors, "The per-item failure should be counted as an error!");
        Assert.IsTrue(snapshot.PlexConnected, "A per-item failure should not mark Plex as disconnected!");
    }

    /// <summary>
    /// An HttpClient timeout on an episode during reconcile is treated as an item failure rather than genuine
    /// cancellation
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task SyncOrchestratorReconcileEpisodeTimeoutContinuesWithRemainingEpisodesAndAggregates()
    {
        _plexClient.Libraries.Add(new PlexLibrary
                                  {
                                      Key = "2",
                                      Title = "Shows",
                                      Kind = MediaKind.Series
                                  });

        AddSeriesWithTwoEpisodes("s1");
        _plexClient.LibraryItems["2"] = [_plexClient.Items["s1"]];
        _nfoWriter.FailuresByRatingKey["e1"] = new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout");

        var orchestrator = CreateOrchestrator();

        await orchestrator.ReconcileAsync(CancellationToken.None);

        var snapshot = _status.GetSnapshot();

        Assert.HasCount(1, _nfoWriter.WritesOf(MediaKind.Episode), "The episode after the timed-out one should still be written!");
        Assert.HasCount(1, _nfoWriter.WritesOf(MediaKind.Season), "The season aggregate should still be written despite the episode timeout!");
        Assert.HasCount(1, _nfoWriter.WritesOf(MediaKind.Series), "The series aggregate should still be written despite the episode timeout!");
        Assert.AreEqual(1L, snapshot.Errors, "An episode timeout should be counted as an item failure, not propagated as cancellation!");
    }

    /// <summary>
    /// A show whose aggregate write fails during reconcile does not abort the rest of the series library
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task SyncOrchestratorReconcileShowFailureContinuesWithRemainingShows()
    {
        _plexClient.Libraries.Add(new PlexLibrary
                                  {
                                      Key = "2",
                                      Title = "Shows",
                                      Kind = MediaKind.Series
                                  });

        AddSeriesWithTwoEpisodes("s1");
        _nfoWriter.FailuresByRatingKey["s1"] = new IOException("disk full");

        var secondShowDirectory = "/data/Shows/Better Call Saul/Season 01".Replace('/', Path.DirectorySeparatorChar);
        var secondShowEpisode = new MediaItem
                                {
                                    RatingKey = "e3",
                                    Kind = MediaKind.Episode,
                                    Title = "Uno",
                                    SeasonNumber = 1,
                                    EpisodeNumber = 1,
                                    ShowRatingKey = "s2",
                                    ShowTitle = "Better Call Saul",
                                    FilePath = secondShowDirectory + "/S01E01.mkv"
                                };

        _plexClient.Items["s2"] = new MediaItem
                                  {
                                      RatingKey = "s2",
                                      Kind = MediaKind.Series,
                                      Title = "Better Call Saul"
                                  };
        _plexClient.Items["e3"] = secondShowEpisode;
        _plexClient.Episodes["s2"] = [secondShowEpisode];
        _plexClient.LibraryItems["2"] = [_plexClient.Items["s1"], _plexClient.Items["s2"]];

        var orchestrator = CreateOrchestrator();

        await orchestrator.ReconcileAsync(CancellationToken.None);

        var snapshot = _status.GetSnapshot();

        Assert.HasCount(1, _nfoWriter.WritesOf(MediaKind.Series), "Only the healthy show's series aggregate should have been written!");
        Assert.AreEqual("Better Call Saul", _nfoWriter.WritesOf(MediaKind.Series)[0].Item.Title, "The show after the failing one should still be aggregated!");
        Assert.AreEqual(1L, snapshot.Errors, "The per-show failure should be counted as an error!");
        Assert.IsTrue(snapshot.PlexConnected, "A per-show failure should not mark Plex as disconnected!");
        Assert.IsNotNull(snapshot.LastReconcileAt, "The reconcile should still complete and record its timestamp!");
    }

    /// <summary>
    /// An HttpClient timeout on a show's aggregate write during reconcile is treated as an item failure rather
    /// than genuine cancellation
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task SyncOrchestratorReconcileShowTimeoutContinuesWithRemainingShows()
    {
        _plexClient.Libraries.Add(new PlexLibrary
                                  {
                                      Key = "2",
                                      Title = "Shows",
                                      Kind = MediaKind.Series
                                  });

        AddSeriesWithTwoEpisodes("s1");
        _nfoWriter.FailuresByRatingKey["s1"] = new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout");

        var secondShowDirectory = "/data/Shows/Better Call Saul/Season 01".Replace('/', Path.DirectorySeparatorChar);
        var secondShowEpisode = new MediaItem
                                {
                                    RatingKey = "e3",
                                    Kind = MediaKind.Episode,
                                    Title = "Uno",
                                    SeasonNumber = 1,
                                    EpisodeNumber = 1,
                                    ShowRatingKey = "s2",
                                    ShowTitle = "Better Call Saul",
                                    FilePath = secondShowDirectory + "/S01E01.mkv"
                                };

        _plexClient.Items["s2"] = new MediaItem
                                  {
                                      RatingKey = "s2",
                                      Kind = MediaKind.Series,
                                      Title = "Better Call Saul"
                                  };
        _plexClient.Items["e3"] = secondShowEpisode;
        _plexClient.Episodes["s2"] = [secondShowEpisode];
        _plexClient.LibraryItems["2"] = [_plexClient.Items["s1"], _plexClient.Items["s2"]];

        var orchestrator = CreateOrchestrator();

        await orchestrator.ReconcileAsync(CancellationToken.None);

        var snapshot = _status.GetSnapshot();

        Assert.HasCount(1, _nfoWriter.WritesOf(MediaKind.Series), "Only the healthy show's series aggregate should have been written!");
        Assert.AreEqual("Better Call Saul", _nfoWriter.WritesOf(MediaKind.Series)[0].Item.Title, "The show after the timed-out one should still be aggregated!");
        Assert.AreEqual(1L, snapshot.Errors, "A show timeout should be counted as an item failure, not propagated as cancellation!");
    }

    /// <summary>
    /// Episode writes within a series are processed concurrently instead of one at a time
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task SyncOrchestratorReconcileSeriesLibraryWritesEpisodesConcurrently()
    {
        _plexClient.Libraries.Add(new PlexLibrary
                                  {
                                      Key = "2",
                                      Title = "Shows",
                                      Kind = MediaKind.Series
                                  });

        AddSeriesWithEpisodes("s1", episodeCount: 4);
        _plexClient.LibraryItems["2"] = [_plexClient.Items["s1"]];
        _nfoWriter.ConcurrencyGate = 2;

        var orchestrator = CreateOrchestrator();

        await orchestrator.ReconcileAsync(CancellationToken.None);

        Assert.HasCount(4, _nfoWriter.WritesOf(MediaKind.Episode), "Every episode should still have been written!");
        Assert.IsGreaterThan(1, _nfoWriter.MaxObservedConcurrency, "Episode writes should overlap instead of running one at a time!");
    }

    /// <summary>
    /// Concurrent episode writes never exceed the configured parallelism limit
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task SyncOrchestratorReconcileSeriesLibraryRespectsEpisodeParallelismLimit()
    {
        _plexClient.Libraries.Add(new PlexLibrary
                                  {
                                      Key = "2",
                                      Title = "Shows",
                                      Kind = MediaKind.Series
                                  });

        AddSeriesWithEpisodes("s1", episodeCount: 6);
        _plexClient.LibraryItems["2"] = [_plexClient.Items["s1"]];

        // The gate is set one above the limit under test: a correctly bounded run never reaches it and every
        // write instead times out and proceeds, while a regression that lets a third write overlap fills it and
        // is caught immediately.
        _nfoWriter.ConcurrencyGate = 3;

        var orchestrator = CreateOrchestrator(new SyncOptions
                                              {
                                                  EpisodeReconcileParallelism = 2
                                              });

        await orchestrator.ReconcileAsync(CancellationToken.None);

        Assert.HasCount(6, _nfoWriter.WritesOf(MediaKind.Episode), "Every episode should still have been written!");
        Assert.IsLessThanOrEqualTo(2, _nfoWriter.MaxObservedConcurrency, "Concurrent episode writes should never exceed the configured limit!");
    }

    /// <summary>
    /// Episodes that share a multi-episode file resolve to the same NFO target and must never write to it at the
    /// same time, even though unrelated episodes are written concurrently
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task SyncOrchestratorReconcileSeriesLibrarySharedFileEpisodesNeverOverlap()
    {
        _plexClient.Libraries.Add(new PlexLibrary
                                  {
                                      Key = "2",
                                      Title = "Shows",
                                      Kind = MediaKind.Series
                                  });

        var sharedFile = _seasonDirectory + "/S01E01-E02.mkv";

        var first = new MediaItem
                    {
                        RatingKey = "e1",
                        Kind = MediaKind.Episode,
                        Title = "Multi Part One",
                        SeasonNumber = 1,
                        EpisodeNumber = 1,
                        ShowRatingKey = "s1",
                        ShowTitle = "Breaking Bad",
                        FilePath = sharedFile
                    };

        var second = new MediaItem
                     {
                         RatingKey = "e2",
                         Kind = MediaKind.Episode,
                         Title = "Multi Part Two",
                         SeasonNumber = 1,
                         EpisodeNumber = 2,
                         ShowRatingKey = "s1",
                         ShowTitle = "Breaking Bad",
                         FilePath = sharedFile
                     };

        var third = new MediaItem
                    {
                        RatingKey = "e3",
                        Kind = MediaKind.Episode,
                        Title = "Episode Three",
                        SeasonNumber = 1,
                        EpisodeNumber = 3,
                        ShowRatingKey = "s1",
                        ShowTitle = "Breaking Bad",
                        FilePath = _seasonDirectory + "/S01E03.mkv"
                    };

        _plexClient.Items["e1"] = first;
        _plexClient.Items["e2"] = second;
        _plexClient.Items["e3"] = third;
        _plexClient.Items["s1"] = new MediaItem
                                  {
                                      RatingKey = "s1",
                                      Kind = MediaKind.Series,
                                      Title = "Breaking Bad"
                                  };
        _plexClient.Episodes["s1"] = [first, second, third];
        _plexClient.LibraryItems["2"] = [_plexClient.Items["s1"]];

        // Grouping by file leaves only two groups (the shared file and the third episode's own file), so the
        // three-way gate below never fills in a correct implementation: a flat, ungrouped loop would let all
        // three episodes overlap and fill it instead, exposing the shared-path concurrency it must never reach.
        _nfoWriter.ConcurrencyGate = 3;

        var orchestrator = CreateOrchestrator();

        await orchestrator.ReconcileAsync(CancellationToken.None);

        Assert.HasCount(3, _nfoWriter.WritesOf(MediaKind.Episode), "Every episode should still have been written!");
        Assert.AreEqual(1, _nfoWriter.MaxObservedConcurrencyByPath[sharedFile], "Episodes resolving to the same NFO target must never write to it concurrently!");
    }

    /// <summary>
    /// A configured parallelism of zero is clamped to sequential writes instead of throwing
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task SyncOrchestratorReconcileSeriesLibraryZeroParallelismRunsSequentially()
    {
        _plexClient.Libraries.Add(new PlexLibrary
                                  {
                                      Key = "2",
                                      Title = "Shows",
                                      Kind = MediaKind.Series
                                  });

        AddSeriesWithEpisodes("s1", episodeCount: 3);
        _plexClient.LibraryItems["2"] = [_plexClient.Items["s1"]];

        // One above the sequential bound: a correctly clamped run never reaches it, while a broken clamp that
        // lets a second write overlap fills it and is caught.
        _nfoWriter.ConcurrencyGate = 2;

        var orchestrator = CreateOrchestrator(new SyncOptions
                                              {
                                                  EpisodeReconcileParallelism = 0
                                              });

        await orchestrator.ReconcileAsync(CancellationToken.None);

        Assert.HasCount(3, _nfoWriter.WritesOf(MediaKind.Episode), "Every episode should still have been written!");
        Assert.AreEqual(1, _nfoWriter.MaxObservedConcurrency, "A zero parallelism setting should be clamped to sequential writes rather than throwing!");
    }

    /// <summary>
    /// A configured parallelism below zero is clamped to sequential writes instead of being treated as unbounded
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task SyncOrchestratorReconcileSeriesLibraryNegativeParallelismRunsSequentially()
    {
        _plexClient.Libraries.Add(new PlexLibrary
                                  {
                                      Key = "2",
                                      Title = "Shows",
                                      Kind = MediaKind.Series
                                  });

        AddSeriesWithEpisodes("s1", episodeCount: 3);
        _plexClient.LibraryItems["2"] = [_plexClient.Items["s1"]];

        // One above the sequential bound: a correctly clamped run never reaches it, while treating -1 as
        // unbounded would let a second write overlap, filling it and being caught.
        _nfoWriter.ConcurrencyGate = 2;

        var orchestrator = CreateOrchestrator(new SyncOptions
                                              {
                                                  EpisodeReconcileParallelism = -1
                                              });

        await orchestrator.ReconcileAsync(CancellationToken.None);

        Assert.HasCount(3, _nfoWriter.WritesOf(MediaKind.Episode), "Every episode should still have been written!");
        Assert.AreEqual(1, _nfoWriter.MaxObservedConcurrency, "A negative parallelism setting should be clamped to sequential writes rather than treated as unbounded!");
    }

    /// <summary>
    /// A library kind that is neither movie nor series is ignored
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task SyncOrchestratorReconcileIgnoresUnsupportedLibraryKind()
    {
        _plexClient.Libraries.Add(new PlexLibrary
                                  {
                                      Key = "4",
                                      Title = "Music",
                                      Kind = MediaKind.Unknown
                                  });

        var orchestrator = CreateOrchestrator();

        await orchestrator.ReconcileAsync(CancellationToken.None);

        Assert.IsEmpty(_plexClient.LibraryItemRequests, "An unsupported library kind should not be read!");
        Assert.IsNotNull(_status.GetSnapshot().LastReconcileAt, "The reconcile timestamp should still be recorded!");
    }

    /// <summary>
    /// A failing reconcile run is recorded as an error instead of being propagated
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task SyncOrchestratorReconcileFailureRecordsError()
    {
        _plexClient.LibrariesException = new HttpRequestException("no route to host");

        var orchestrator = CreateOrchestrator();

        await orchestrator.ReconcileAsync(CancellationToken.None);

        var snapshot = _status.GetSnapshot();

        Assert.AreEqual(1L, snapshot.Errors, "The failure should be counted!");
        Assert.AreEqual("no route to host", snapshot.LastError, "The failure message should be recorded!");
        Assert.IsFalse(snapshot.PlexConnected, "A failure should mark Plex as disconnected!");
        Assert.IsNull(snapshot.LastReconcileAt, "A failed reconcile should not record a completion timestamp!");
    }

    /// <summary>
    /// An HttpClient timeout on the upfront libraries request is treated as a normal sync failure rather than
    /// genuine cancellation, even though it also throws <see cref="OperationCanceledException"/>
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task SyncOrchestratorReconcileUpfrontTimeoutRecordsErrorInsteadOfPropagating()
    {
        _plexClient.LibrariesException = new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout");

        var orchestrator = CreateOrchestrator();

        await orchestrator.ReconcileAsync(CancellationToken.None);

        var snapshot = _status.GetSnapshot();

        Assert.AreEqual(1L, snapshot.Errors, "An upfront timeout should be counted as a sync failure, not propagated as cancellation!");
        Assert.IsFalse(snapshot.PlexConnected, "An upfront timeout should mark Plex as disconnected!");
        Assert.IsNull(snapshot.LastReconcileAt, "A timed-out reconcile should not record a completion timestamp!");
        Assert.IsFalse(snapshot.IsRunning, "The run flag should be cleared after the timeout!");
    }

    /// <summary>
    /// Cancelling a reconcile run propagates the cancellation and clears the run flag
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task SyncOrchestratorReconcileCancellationPropagates()
    {
        _plexClient.Libraries.Add(new PlexLibrary
                                  {
                                      Key = "1",
                                      Title = "Movies",
                                      Kind = MediaKind.Movie
                                  });

        _plexClient.LibraryItems["1"] = [AddMovie("m1", "Heat", "/data/Movies/Heat (1995)/Heat.mkv")];

        using var cancellation = new CancellationTokenSource();

        await cancellation.CancelAsync();

        var orchestrator = CreateOrchestrator();

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await orchestrator.ReconcileAsync(cancellation.Token),
                                                             "Cancellation should be propagated to the caller!");

        Assert.IsFalse(_status.GetSnapshot().IsRunning, "The run flag should be cleared after cancellation!");
        Assert.IsEmpty(_nfoWriter.Writes, "A cancelled reconcile should not write any NFO file!");
    }

    /// <summary>
    /// Cancelling the token while episode writes for a series are actually in flight propagates the cancellation
    /// out of the parallel reconcile and clears the run flag, mirroring
    /// <see cref="SyncOrchestratorReconcileCancellationPropagates"/> but for the concurrent episode-writing path
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task SyncOrchestratorReconcileSeriesLibraryCancellationDuringParallelWritesPropagates()
    {
        _plexClient.Libraries.Add(new PlexLibrary
                                  {
                                      Key = "2",
                                      Title = "Shows",
                                      Kind = MediaKind.Series
                                  });

        AddSeriesWithEpisodes("s1", episodeCount: 4);
        _plexClient.LibraryItems["2"] = [_plexClient.Items["s1"]];

        // The gate is set one above the configured parallelism so the two concurrently running writes never
        // reach it and instead await the gate release with a generous timeout, so the test's own cancellation -
        // not the timeout - is what unblocks them.
        _nfoWriter.ConcurrencyGate = 3;
        _nfoWriter.ConcurrencyGateTimeout = TimeSpan.FromSeconds(5);
        _nfoWriter.NotifyAtConcurrency = 2;

        var orchestrator = CreateOrchestrator(new SyncOptions
                                              {
                                                  EpisodeReconcileParallelism = 2
                                              });

        using var cancellation = new CancellationTokenSource();

        var reconcileTask = orchestrator.ReconcileAsync(cancellation.Token);

        await _nfoWriter.ConcurrencyReached.WaitAsync(TimeSpan.FromSeconds(5));
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await reconcileTask,
                                                             "Cancellation during in-flight episode writes should be propagated to the caller!");

        var snapshot = _status.GetSnapshot();

        Assert.IsFalse(snapshot.IsRunning, "The run flag should be cleared after cancellation!");
        Assert.AreEqual(0L, snapshot.Errors, "Cancellation should propagate instead of being recorded as an item error!");
        Assert.IsEmpty(_nfoWriter.WritesOf(MediaKind.Episode), "None of the in-flight episode writes should have completed after cancellation!");
    }

    /// <summary>
    /// Create an orchestrator wired to the current test doubles
    /// </summary>
    /// <param name="syncOptions">Sync options, or <c>null</c> for the defaults</param>
    /// <param name="plexOptions">Plex options, or <c>null</c> for the defaults</param>
    /// <returns>The orchestrator under test</returns>
    private SyncOrchestrator CreateOrchestrator(SyncOptions? syncOptions = null, PlexOptions? plexOptions = null)
    {
        return new SyncOrchestrator(_plexClient,
                                    _nfoWriter,
                                    _pathMapper,
                                    new WatchAggregator(),
                                    _stateStore,
                                    _status,
                                    Options.Create(plexOptions ?? new PlexOptions()),
                                    Options.Create(syncOptions ?? new SyncOptions()),
                                    NullLogger<SyncOrchestrator>.Instance);
    }

    /// <summary>
    /// Register a movie that is returned for the given rating key
    /// </summary>
    /// <param name="ratingKey">Rating key</param>
    /// <param name="title">Movie title</param>
    /// <param name="filePath">Plex file path</param>
    /// <returns>The registered movie</returns>
    private MediaItem AddMovie(string ratingKey, string title, string filePath)
    {
        var movie = new MediaItem
                    {
                        RatingKey = ratingKey,
                        Kind = MediaKind.Movie,
                        Title = title,
                        FilePath = filePath
                    };

        _plexClient.Items[ratingKey] = movie;

        return movie;
    }

    /// <summary>
    /// Register a series with two episodes, of which only the first one is watched
    /// </summary>
    /// <param name="showRatingKey">Rating key of the series</param>
    private void AddSeriesWithTwoEpisodes(string showRatingKey)
    {
        var first = new MediaItem
                    {
                        RatingKey = "e1",
                        Kind = MediaKind.Episode,
                        Title = "Pilot",
                        SeasonNumber = 1,
                        EpisodeNumber = 1,
                        ShowRatingKey = showRatingKey,
                        ShowTitle = "Breaking Bad",
                        FilePath = _seasonDirectory + "/S01E01.mkv",
                        Watch = new WatchInfo
                                {
                                    Watched = true,
                                    PlayCount = 1,
                                    LastPlayed = _since.AddMinutes(5)
                                }
                    };

        var second = new MediaItem
                     {
                         RatingKey = "e2",
                         Kind = MediaKind.Episode,
                         Title = "Cat's in the Bag...",
                         SeasonNumber = 1,
                         EpisodeNumber = 2,
                         ShowRatingKey = showRatingKey,
                         ShowTitle = "Breaking Bad",
                         FilePath = _seasonDirectory + "/S01E02.mkv"
                     };

        _plexClient.Items["e1"] = first;
        _plexClient.Items["e2"] = second;
        _plexClient.Items[showRatingKey] = new MediaItem
                                           {
                                               RatingKey = showRatingKey,
                                               Kind = MediaKind.Series,
                                               Title = "Breaking Bad"
                                           };

        _plexClient.Episodes[showRatingKey] = [first, second];
    }

    /// <summary>
    /// Register a series with the given number of unwatched episodes, each mapped to its own file
    /// </summary>
    /// <param name="showRatingKey">Rating key of the series</param>
    /// <param name="episodeCount">Number of episodes to register</param>
    private void AddSeriesWithEpisodes(string showRatingKey, int episodeCount)
    {
        var episodes = new List<MediaItem>();

        for (var episodeNumber = 1; episodeNumber <= episodeCount; episodeNumber++)
        {
            var episode = new MediaItem
                          {
                              RatingKey = $"e{episodeNumber}",
                              Kind = MediaKind.Episode,
                              Title = $"Episode {episodeNumber}",
                              SeasonNumber = 1,
                              EpisodeNumber = episodeNumber,
                              ShowRatingKey = showRatingKey,
                              ShowTitle = "Breaking Bad",
                              FilePath = _seasonDirectory + $"/S01E{episodeNumber:D2}.mkv"
                          };

            _plexClient.Items[episode.RatingKey] = episode;
            episodes.Add(episode);
        }

        _plexClient.Items[showRatingKey] = new MediaItem
                                           {
                                               RatingKey = showRatingKey,
                                               Kind = MediaKind.Series,
                                               Title = "Breaking Bad"
                                           };

        _plexClient.Episodes[showRatingKey] = episodes;
    }

    /// <summary>
    /// Register a watch-history entry
    /// </summary>
    /// <param name="ratingKey">Rating key of the watched item</param>
    /// <param name="viewedAt">Timestamp at which the item was viewed</param>
    private void AddHistory(string ratingKey, DateTimeOffset viewedAt)
    {
        _plexClient.History.Add(new PlexHistoryEntry
                                {
                                    RatingKey = ratingKey,
                                    AccountId = _plexClient.OwnerAccountId,
                                    ViewedAt = viewedAt
                                });
    }

    #endregion // Methods
}