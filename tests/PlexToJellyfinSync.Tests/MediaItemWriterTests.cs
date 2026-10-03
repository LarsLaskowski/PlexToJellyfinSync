using Microsoft.Extensions.Logging.Abstractions;

using PlexToJellyfinSync.Core.Enums;
using PlexToJellyfinSync.Core.Models;
using PlexToJellyfinSync.Service;

namespace PlexToJellyfinSync.Tests;

/// <summary>
/// Tests for <see cref="MediaItemWriter"/>
/// </summary>
[TestClass]
public sealed class MediaItemWriterTests
{
    #region Fields

    private readonly TestContext _testContext;

    private RecordingNfoWriter _nfoWriter = new();
    private StubPathMapper _pathMapper = new();
    private SyncStatusService _status = new(NullLogger<SyncStatusService>.Instance);

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="testContext">Test context</param>
    public MediaItemWriterTests(TestContext testContext)
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
        _nfoWriter = new RecordingNfoWriter();
        _pathMapper = new StubPathMapper();
        _status = new SyncStatusService(NullLogger<SyncStatusService>.Instance);
    }

    /// <summary>
    /// An item without a usable file path is skipped without touching the NFO writer or the counters
    /// </summary>
    /// <param name="filePath">File path of the item</param>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public async Task MediaItemWriterWriteItemWithoutFilePathSkipsItem(string? filePath)
    {
        var writer = CreateWriter();

        await writer.WriteItemAsync(CreateMovie(filePath), _testContext.CancellationToken);

        var snapshot = _status.GetSnapshot();

        Assert.IsEmpty(_nfoWriter.Writes, "An item without a file path should not be written!");
        Assert.AreEqual(0L, snapshot.NfoCreated, "No NFO should be counted as created!");
        Assert.AreEqual(0L, snapshot.NfoUpdated, "No NFO should be counted as updated!");
    }

    /// <summary>
    /// A file path without a path mapping is always skipped instead of being passed through
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task MediaItemWriterWriteItemUnmappedPathSkipsItem()
    {
        _pathMapper.Unmapped.Add("/data/Movies/Heat/Heat.mkv");

        var writer = CreateWriter();

        await writer.WriteItemAsync(CreateMovie("/data/Movies/Heat/Heat.mkv"), _testContext.CancellationToken);

        var snapshot = _status.GetSnapshot();

        Assert.IsEmpty(_nfoWriter.Writes, "An unmapped path should never be written!");
        Assert.AreEqual(0L, snapshot.NfoCreated, "No NFO should be counted as created!");
        Assert.AreEqual(0L, snapshot.NfoUpdated, "No NFO should be counted as updated!");
    }

    /// <summary>
    /// A mapped item is written once at the mapped local path
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task MediaItemWriterWriteItemMappedPathWritesAtLocalPath()
    {
        var movie = CreateMovie("/data/Movies/Heat/Heat.mkv");
        var writer = CreateWriter();

        await writer.WriteItemAsync(movie, _testContext.CancellationToken);

        Assert.HasCount(1, _nfoWriter.Writes, "The item should be written exactly once!");
        Assert.AreSame(movie, _nfoWriter.Writes[0].Item, "The given item should be written!");
        Assert.AreEqual("/data/Movies/Heat/Heat.mkv", _nfoWriter.Writes[0].LocalPath, "The mapped local path should be used!");
    }

    /// <summary>
    /// The write outcome decides which counter grows
    /// </summary>
    /// <param name="outcome">Outcome returned by the NFO writer</param>
    /// <param name="created">Expected created counter</param>
    /// <param name="updated">Expected updated counter</param>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    [DataRow(NfoWriteOutcome.Created, 1L, 0L)]
    [DataRow(NfoWriteOutcome.Updated, 0L, 1L)]
    [DataRow(NfoWriteOutcome.Skipped, 0L, 0L)]
    public async Task MediaItemWriterWriteItemCountsOutcome(NfoWriteOutcome outcome, long created, long updated)
    {
        _nfoWriter.Outcome = outcome;

        var writer = CreateWriter();

        await writer.WriteItemAsync(CreateMovie("/data/Movies/Heat/Heat.mkv"), _testContext.CancellationToken);

        var snapshot = _status.GetSnapshot();

        Assert.AreEqual(created, snapshot.NfoCreated, "The created counter should match the outcome!");
        Assert.AreEqual(updated, snapshot.NfoUpdated, "The updated counter should match the outcome!");
    }

    /// <summary>
    /// An aggregate is written to the given directory unchanged, without path mapping
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task MediaItemWriterWriteAggregateWritesAtGivenDirectory()
    {
        var season = new MediaItem
                     {
                         Kind = MediaKind.Season,
                         Title = "Season 1"
                     };

        // A mapper that rejects everything proves that the directory is not mapped again
        _pathMapper.Unmapped.Add("/local/Show/Season 01");

        var writer = CreateWriter();

        await writer.WriteAggregateAsync(season, "/local/Show/Season 01", _testContext.CancellationToken);

        Assert.HasCount(1, _nfoWriter.Writes, "The aggregate should be written exactly once!");
        Assert.AreSame(season, _nfoWriter.Writes[0].Item, "The given aggregate should be written!");
        Assert.AreEqual("/local/Show/Season 01", _nfoWriter.Writes[0].LocalPath, "The directory should be used unchanged!");
    }

    /// <summary>
    /// The outcome of an aggregate write is counted like the one of an item write
    /// </summary>
    /// <param name="outcome">Outcome returned by the NFO writer</param>
    /// <param name="created">Expected created counter</param>
    /// <param name="updated">Expected updated counter</param>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    [DataRow(NfoWriteOutcome.Created, 1L, 0L)]
    [DataRow(NfoWriteOutcome.Updated, 0L, 1L)]
    [DataRow(NfoWriteOutcome.Skipped, 0L, 0L)]
    public async Task MediaItemWriterWriteAggregateCountsOutcome(NfoWriteOutcome outcome, long created, long updated)
    {
        _nfoWriter.Outcome = outcome;

        var writer = CreateWriter();

        await writer.WriteAggregateAsync(new MediaItem
                                         {
                                             Kind = MediaKind.Series
                                         },
                                         "/local/Show",
                                         _testContext.CancellationToken);

        var snapshot = _status.GetSnapshot();

        Assert.AreEqual(created, snapshot.NfoCreated, "The created counter should match the outcome!");
        Assert.AreEqual(updated, snapshot.NfoUpdated, "The updated counter should match the outcome!");
    }

    /// <summary>
    /// A failing NFO write propagates out of the item write and changes no counter
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task MediaItemWriterWriteItemWriterFailurePropagates()
    {
        _nfoWriter.FailuresByRatingKey["m1"] = new IOException("disk full");

        var writer = CreateWriter();

        var exception = await Assert.ThrowsAsync<IOException>(async () => await writer.WriteItemAsync(CreateMovie("/data/Movies/Heat/Heat.mkv"), _testContext.CancellationToken),
                                                              "A failing NFO write should propagate to the caller!");

        var snapshot = _status.GetSnapshot();

        Assert.AreEqual("disk full", exception.Message, "The original exception should propagate!");
        Assert.AreEqual(0L, snapshot.NfoCreated, "No NFO should be counted as created!");
        Assert.AreEqual(0L, snapshot.NfoUpdated, "No NFO should be counted as updated!");
        Assert.AreEqual(0L, snapshot.Errors, "The writer should leave the error handling to its caller!");
    }

    /// <summary>
    /// A failing NFO write propagates out of the aggregate write and changes no counter
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task MediaItemWriterWriteAggregateWriterFailurePropagates()
    {
        _nfoWriter.FailuresByRatingKey["s1"] = new IOException("disk full");

        var writer = CreateWriter();

        var series = new MediaItem
                     {
                         RatingKey = "s1",
                         Kind = MediaKind.Series
                     };

        await Assert.ThrowsAsync<IOException>(async () => await writer.WriteAggregateAsync(series, "/local/Show", _testContext.CancellationToken),
                                              "A failing NFO write should propagate to the caller!");

        var snapshot = _status.GetSnapshot();

        Assert.AreEqual(0L, snapshot.NfoCreated, "No NFO should be counted as created!");
        Assert.AreEqual(0L, snapshot.NfoUpdated, "No NFO should be counted as updated!");
        Assert.AreEqual(0L, snapshot.Errors, "The writer should leave the error handling to its caller!");
    }

    /// <summary>
    /// Create a movie
    /// </summary>
    /// <param name="filePath">Plex file path</param>
    /// <returns>The movie</returns>
    private static MediaItem CreateMovie(string? filePath)
    {
        return new MediaItem
               {
                   RatingKey = "m1",
                   Kind = MediaKind.Movie,
                   Title = "Heat",
                   FilePath = filePath
               };
    }

    /// <summary>
    /// Create the writer under test wired to the current test doubles
    /// </summary>
    /// <returns>The writer under test</returns>
    private MediaItemWriter CreateWriter()
    {
        return new MediaItemWriter(_nfoWriter, _pathMapper, _status, NullLogger<MediaItemWriter>.Instance);
    }

    #endregion // Methods
}