using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using PlexToJellyfinSync.Core.Enums;
using PlexToJellyfinSync.Core.Models;
using PlexToJellyfinSync.Core.Options;
using PlexToJellyfinSync.Service;

namespace PlexToJellyfinSync.Tests;

/// <summary>
/// Tests for <see cref="NfoWriter"/>
/// </summary>
[TestClass]
public sealed class NfoWriterTests
{
    #region Fields

    private string _tempDirectory = string.Empty;

    #endregion // Fields

    #region Methods

    /// <summary>
    /// Create a temporary working directory for each test
    /// </summary>
    [TestInitialize]
    public void Initialize()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "pjftests" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    /// <summary>
    /// Remove the temporary working directory after each test
    /// </summary>
    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    /// <summary>
    /// Create an NFO writer with the given options
    /// </summary>
    /// <param name="createMissing">Whether missing files are created</param>
    /// <returns>NFO writer</returns>
    private static NfoWriter CreateWriter(bool createMissing)
    {
        var nfoOptions = Options.Create(new NfoOptions());
        var syncOptions = Options.Create(new SyncOptions
                                         {
                                             CreateMissingNfo = createMissing
                                         });

        return new NfoWriter(nfoOptions, syncOptions, NullLogger<NfoWriter>.Instance);
    }

    /// <summary>
    /// A missing movie NFO is created with the watch state
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task NfoWriterMovieWithoutNfoCreatesFile()
    {
        var writer = CreateWriter(createMissing: true);
        var moviePath = Path.Combine(_tempDirectory, "Heat (1995).mkv");
        var item = new MediaItem
                   {
                       Kind = MediaKind.Movie,
                       Title = "Heat",
                       Watch = new WatchInfo
                               {
                                   Watched = true,
                                   PlayCount = 1,
                                   LastPlayed = DateTimeOffset.Now
                               }
                   };

        var outcome = await writer.WriteAsync(item, moviePath, CancellationToken.None);

        var nfoPath = Path.ChangeExtension(moviePath, ".nfo");

        Assert.AreEqual(NfoWriteOutcome.Created, outcome, "Outcome should be Created!");
        Assert.IsTrue(File.Exists(nfoPath), "NFO file should have been created!");

        var content = await File.ReadAllTextAsync(nfoPath);

        StringAssert.Contains(content, "<watched>true</watched>", "Watched flag missing!");
        StringAssert.Contains(content, "<title>Heat</title>", "Title missing!");
    }

    /// <summary>
    /// An existing NFO keeps unrelated nodes and only updates the watch state
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task NfoWriterExistingNfoPreservesOtherNodes()
    {
        var writer = CreateWriter(createMissing: false);
        var moviePath = Path.Combine(_tempDirectory, "Heat (1995).mkv");
        var nfoPath = Path.ChangeExtension(moviePath, ".nfo");

        await File.WriteAllTextAsync(nfoPath, "<movie><title>Custom Title</title><watched>false</watched><playcount>0</playcount></movie>");

        var item = new MediaItem
                   {
                       Kind = MediaKind.Movie,
                       Title = "Should Not Overwrite",
                       Watch = new WatchInfo
                               {
                                   Watched = true,
                                   PlayCount = 3
                               }
                   };

        var outcome = await writer.WriteAsync(item, moviePath, CancellationToken.None);
        var content = await File.ReadAllTextAsync(nfoPath);

        Assert.AreEqual(NfoWriteOutcome.Updated, outcome, "Outcome should be Updated!");
        StringAssert.Contains(content, "<title>Custom Title</title>", "Existing title should be preserved!");
        StringAssert.Contains(content, "<watched>true</watched>", "Watched flag should be updated!");
        StringAssert.Contains(content, "<playcount>3</playcount>", "Play count should be updated!");
    }

    /// <summary>
    /// When creation is disabled and no file exists nothing is written
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task NfoWriterMissingFileCreationDisabledSkips()
    {
        var writer = CreateWriter(createMissing: false);
        var moviePath = Path.Combine(_tempDirectory, "Heat (1995).mkv");
        var item = new MediaItem
                   {
                       Kind = MediaKind.Movie,
                       Title = "Heat",
                       Watch = new WatchInfo
                               {
                                   Watched = true
                               }
                   };

        var outcome = await writer.WriteAsync(item, moviePath, CancellationToken.None);

        Assert.AreEqual(NfoWriteOutcome.Skipped, outcome, "Outcome should be Skipped!");
        Assert.IsFalse(File.Exists(Path.ChangeExtension(moviePath, ".nfo")), "No NFO file should have been created!");
    }

    #endregion // Methods
}