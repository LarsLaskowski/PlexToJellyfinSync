using System.Runtime.Versioning;

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

    private readonly TestContext _testContext;

    private string _tempDirectory = string.Empty;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="testContext">Test context</param>
    public NfoWriterTests(TestContext testContext)
    {
        _testContext = testContext;
    }

    #endregion // Constructors

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

    /// <summary>
    /// A write that fails partway through must not leave the existing NFO truncated
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task NfoWriterExistingNfoFailedWritePreservesContent()
    {
        var writer = CreateWriter(createMissing: false, dateTimeFormat: "'\u0001'");
        var moviePath = Path.Combine(_tempDirectory, "Heat (1995).mkv");
        var nfoPath = Path.ChangeExtension(moviePath, ".nfo");
        const string originalContent = "<movie><title>Custom Title</title><watched>false</watched><playcount>0</playcount></movie>";

        await File.WriteAllTextAsync(nfoPath, originalContent, CancellationToken.None);

        var item = new MediaItem
                   {
                       Kind = MediaKind.Movie,
                       Title = "Should Not Overwrite",
                       Watch = new WatchInfo
                               {
                                   Watched = true,
                                   PlayCount = 3,
                                   LastPlayed = DateTimeOffset.Now
                               }
                   };

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => writer.WriteAsync(item, moviePath, CancellationToken.None),
                                                           "A serialization failure while formatting lastplayed should surface as an exception!");

        var content = await File.ReadAllTextAsync(nfoPath, CancellationToken.None);

        Assert.AreEqual(originalContent, content, "A write that fails partway through must not truncate the existing NFO!");
        Assert.IsFalse(File.Exists(nfoPath + ".tmp"), "A failed write must not leave a temp file behind!");
    }

    /// <summary>
    /// A temp file that cannot be deleted after a write failure does not mask the original error
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    [OSCondition(OperatingSystems.Linux | OperatingSystems.OSX)]
    [UnsupportedOSPlatform("windows")]
    public async Task NfoWriterTempFileCleanupFailureSurfacesOriginalError()
    {
        var writer = CreateWriter(createMissing: false);
        var moviePath = Path.Combine(_tempDirectory, "Heat (1995).mkv");
        var nfoPath = Path.ChangeExtension(moviePath, ".nfo");
        const string originalContent = "<movie><title>Custom Title</title><watched>false</watched><playcount>0</playcount></movie>";

        await File.WriteAllTextAsync(nfoPath, originalContent, CancellationToken.None);
        Directory.CreateDirectory(nfoPath + ".tmp");

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

        await Assert.ThrowsExactlyAsync<UnauthorizedAccessException>(() => writer.WriteAsync(item, moviePath, CancellationToken.None),
                                                                     "Opening a temp path that is a directory should surface the original failure!");

        var content = await File.ReadAllTextAsync(nfoPath, CancellationToken.None);

        Assert.AreEqual(originalContent, content, "A blocked temp write must not touch the existing NFO!");
    }

    /// <summary>
    /// Updating an existing NFO preserves its Unix file permissions
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    [OSCondition(OperatingSystems.Linux | OperatingSystems.OSX)]
    [UnsupportedOSPlatform("windows")]
    public async Task NfoWriterExistingNfoUpdatePreservesFileMode()
    {
        var writer = CreateWriter(createMissing: false);
        var moviePath = Path.Combine(_tempDirectory, "Heat (1995).mkv");
        var nfoPath = Path.ChangeExtension(moviePath, ".nfo");

        await File.WriteAllTextAsync(nfoPath, "<movie><title>Custom Title</title><watched>false</watched><playcount>0</playcount></movie>", CancellationToken.None);
        File.SetUnixFileMode(nfoPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.OtherRead);

        var item = new MediaItem
                   {
                       Kind = MediaKind.Movie,
                       Title = "Heat",
                       Watch = new WatchInfo
                               {
                                   Watched = true,
                                   PlayCount = 3
                               }
                   };

        await writer.WriteAsync(item, moviePath, CancellationToken.None);

        var mode = File.GetUnixFileMode(nfoPath);

        Assert.IsTrue(mode.HasFlag(UnixFileMode.GroupWrite), "Group write permission should survive an atomic replace so shared media volumes keep working!");
    }

    /// <summary>
    /// Concurrent writes that resolve to the same NFO target - for example the same folder shared by two Plex
    /// libraries - are serialized instead of racing on the shared temp file, which would otherwise make the
    /// losing write delete the winner's in-progress temp file and fail
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task NfoWriterConcurrentWritesToSameTargetNeverRace()
    {
        var writer = CreateWriter(createMissing: true);
        var moviePath = Path.Combine(_tempDirectory, "Heat (1995).mkv");

        var writes = Enumerable.Range(0, 20)
                               .Select(playCount => writer.WriteAsync(new MediaItem
                                                                      {
                                                                          Kind = MediaKind.Movie,
                                                                          Title = "Heat",
                                                                          Watch = new WatchInfo
                                                                                  {
                                                                                      Watched = true,
                                                                                      PlayCount = playCount
                                                                                  }
                                                                      },
                                                                      moviePath,
                                                                      CancellationToken.None))
                               .ToArray();

        await Task.WhenAll(writes);

        var nfoPath = Path.ChangeExtension(moviePath, ".nfo");

        Assert.IsTrue(File.Exists(nfoPath), "The NFO file should exist once every concurrent write to the same target has completed!");
        Assert.IsFalse(File.Exists(nfoPath + ".tmp"), "No temp file should be left behind once every concurrent write has completed!");

        var content = await File.ReadAllTextAsync(nfoPath, _testContext.CancellationToken);

        Assert.Contains("<watched>true</watched>", content, "The resulting NFO should still contain a valid watched element!");
    }

    /// <summary>
    /// A resolved target that falls outside every configured local root is refused, even when the
    /// caller-supplied local path was not caught by the path mapper's own traversal guard
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task NfoWriterTargetOutsideMappedRootIsSkipped()
    {
        var mappedRoot = Path.Combine(_tempDirectory, "media");

        Directory.CreateDirectory(mappedRoot);

        var writer = CreateWriter(createMissing: true, localRoot: mappedRoot);
        var escapingPath = Path.Combine(mappedRoot, "..", "..", "etc", "cron.d", "evil.mkv");
        var item = new MediaItem
                   {
                       Kind = MediaKind.Movie,
                       Title = "Escape",
                       Watch = new WatchInfo
                               {
                                   Watched = true
                               }
                   };

        var outcome = await writer.WriteAsync(item, escapingPath, CancellationToken.None);

        Assert.AreEqual(NfoWriteOutcome.Skipped, outcome, "A write target outside every mapped local root must be refused!");
        Assert.IsFalse(File.Exists(Path.GetFullPath(Path.ChangeExtension(escapingPath, ".nfo"))), "No NFO file should have been created outside the mapped root!");
    }

    /// <summary>
    /// A configured local root with a trailing directory separator still accepts writes under it
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task NfoWriterMappedRootWithTrailingSeparatorAcceptsWrite()
    {
        var writer = CreateWriter(createMissing: true, localRoot: _tempDirectory + Path.DirectorySeparatorChar);
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

        Assert.AreEqual(NfoWriteOutcome.Created, outcome, "A mapped root with a trailing separator should still accept writes under it!");
        Assert.IsTrue(File.Exists(Path.ChangeExtension(moviePath, ".nfo")), "NFO file should have been created!");
    }

    /// <summary>
    /// A configured local root that is the filesystem root itself still accepts writes under it
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task NfoWriterFilesystemRootMappingAcceptsWrite()
    {
        var writer = CreateWriter(createMissing: true, localRoot: Path.GetPathRoot(_tempDirectory)!);
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

        Assert.AreEqual(NfoWriteOutcome.Created, outcome, "A mapped root that is the filesystem root should still accept writes under it!");
        Assert.IsTrue(File.Exists(Path.ChangeExtension(moviePath, ".nfo")), "NFO file should have been created!");
    }

    /// <summary>
    /// Create an NFO writer with the given options
    /// </summary>
    /// <param name="createMissing">Whether missing files are created</param>
    /// <param name="dateTimeFormat">Optional custom date/time format used for the "lastplayed" element</param>
    /// <param name="localRoot">Local root the writer accepts targets under; defaults to the test's temp directory</param>
    /// <returns>NFO writer</returns>
    private NfoWriter CreateWriter(bool createMissing, string? dateTimeFormat = null, string? localRoot = null)
    {
        var nfoOptions = Options.Create(new NfoOptions
                                        {
                                            DateTimeFormat = dateTimeFormat ?? new NfoOptions().DateTimeFormat
                                        });
        var syncOptions = Options.Create(new SyncOptions
                                         {
                                             CreateMissingNfo = createMissing
                                         });
        var pathMappings = Options.Create(new List<PathMapping>
                                          {
                                              new()
                                              {
                                                  Plex = "/data",
                                                  Local = localRoot ?? _tempDirectory
                                              }
                                          });

        return new NfoWriter(nfoOptions, syncOptions, pathMappings, NullLogger<NfoWriter>.Instance);
    }

    #endregion // Methods
}