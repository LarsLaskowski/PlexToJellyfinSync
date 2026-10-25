using System.Globalization;
using System.Runtime.Versioning;
using System.Text.Json;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using PlexToJellyfinSync.Core.Options;
using PlexToJellyfinSync.Service.State;

namespace PlexToJellyfinSync.Tests;

/// <summary>
/// Tests for <see cref="StateStore"/>
/// </summary>
[TestClass]
public sealed class StateStoreTests
{
    #region Fields

    private const string CorruptContent = "{ \"HighWaterMark\": ";

    private static readonly DateTimeOffset _mark = new(2026, 2, 3, 4, 5, 6, TimeSpan.Zero);

    private readonly TestContext _testContext;

    private string _tempDirectory = string.Empty;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="testContext">Test context</param>
    public StateStoreTests(TestContext testContext)
    {
        _testContext = testContext;
    }

    #endregion // Constructors

    #region Methods

    /// <summary>
    /// Create a temporary state directory for each test
    /// </summary>
    [TestInitialize]
    public void Initialize()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "pjfstate" + Guid.NewGuid().ToString("N"));
    }

    /// <summary>
    /// Remove the temporary state directory after each test
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
    /// Without a persisted file no high-water mark is reported
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task StateStoreWithoutFileReturnsNull()
    {
        var store = CreateStore();

        var mark = await store.GetHighWaterMarkAsync(CancellationToken.None);

        Assert.IsNull(mark, "Without a state file there should be no high-water mark!");
    }

    /// <summary>
    /// A persisted high-water mark is read back unchanged, also by a fresh instance
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task StateStoreRoundTripReturnsPersistedValue()
    {
        var store = CreateStore();

        await store.SetHighWaterMarkAsync(_mark, CancellationToken.None);

        var sameInstance = await store.GetHighWaterMarkAsync(CancellationToken.None);
        var otherInstance = await CreateStore().GetHighWaterMarkAsync(CancellationToken.None);

        Assert.AreEqual(_mark, sameInstance, "The persisted value should be read back unchanged!");
        Assert.AreEqual(_mark, otherInstance, "A fresh instance should read the persisted value!");
    }

    /// <summary>
    /// Writing into a missing directory creates the directory
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task StateStoreWithMissingDirectoryCreatesIt()
    {
        var store = CreateStore();

        Assert.IsFalse(Directory.Exists(_tempDirectory), "The state directory should not exist before the first write!");

        await store.SetHighWaterMarkAsync(_mark, CancellationToken.None);

        Assert.IsTrue(File.Exists(GetStateFilePath()), "The state file should have been created!");
    }

    /// <summary>
    /// A second write replaces the previously persisted value completely
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task StateStoreSecondWriteReplacesPreviousValue()
    {
        var store = CreateStore();
        var later = _mark.AddHours(3);

        await store.SetHighWaterMarkAsync(_mark, CancellationToken.None);
        await store.SetHighWaterMarkAsync(later, CancellationToken.None);

        var mark = await store.GetHighWaterMarkAsync(CancellationToken.None);
        var content = await File.ReadAllTextAsync(GetStateFilePath(), _testContext.CancellationToken);

        Assert.AreEqual(later, mark, "The newest value should be reported!");
        Assert.IsFalse(content.Contains("04:05:06", StringComparison.Ordinal), "The overwritten value should not linger in the file!");
    }

    /// <summary>
    /// A corrupt state file is tolerated and treated as if no state existed
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task StateStoreWithCorruptFileReturnsNull()
    {
        Directory.CreateDirectory(_tempDirectory);

        await File.WriteAllTextAsync(GetStateFilePath(), "{ \"HighWaterMark\": ", _testContext.CancellationToken);

        var store = CreateStore();

        var mark = await store.GetHighWaterMarkAsync(CancellationToken.None);

        Assert.IsNull(mark, "A corrupt state file should be treated as missing state!");
    }

    /// <summary>
    /// A corrupt state file is preserved alongside the fresh state instead of being discarded
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task StateStoreWithCorruptFilePreservesItAsCorrupt()
    {
        Directory.CreateDirectory(_tempDirectory);

        const string corruptContent = "{ \"HighWaterMark\": ";

        await File.WriteAllTextAsync(GetStateFilePath(), corruptContent, _testContext.CancellationToken);

        var store = CreateStore();

        await store.GetHighWaterMarkAsync(CancellationToken.None);

        Assert.IsFalse(File.Exists(GetStateFilePath()), "The corrupt file should have been moved aside!");

        var preservedContent = await File.ReadAllTextAsync(GetStateFilePath() + ".corrupt", _testContext.CancellationToken);

        Assert.AreEqual(corruptContent, preservedContent, "The corrupt file's original content should be preserved for investigation!");
    }

    /// <summary>
    /// A write that fails partway through must not leave the existing state file truncated
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [UnsupportedOSPlatform("windows")]
    public async Task StateStoreSetHighWaterMarkAsyncWriteFailurePreservesExistingFile()
    {
        var store = CreateStore();

        await store.SetHighWaterMarkAsync(_mark, CancellationToken.None);
        Directory.CreateDirectory(GetStateFilePath() + ".tmp");

        await Assert.ThrowsExactlyAsync<UnauthorizedAccessException>(() => store.SetHighWaterMarkAsync(_mark.AddHours(3), CancellationToken.None),
                                                                     "Opening a temp path that is a directory should surface the original failure!");

        var mark = await store.GetHighWaterMarkAsync(CancellationToken.None);

        Assert.AreEqual(_mark, mark, "A write that fails partway through must not lose the previously persisted value!");
    }

    /// <summary>
    /// A second write to an existing state file preserves its Unix file permissions
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [UnsupportedOSPlatform("windows")]
    public async Task StateStoreSetHighWaterMarkAsyncPreservesFileMode()
    {
        var store = CreateStore();

        await store.SetHighWaterMarkAsync(_mark, CancellationToken.None);
        File.SetUnixFileMode(GetStateFilePath(), UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.OtherRead);

        await store.SetHighWaterMarkAsync(_mark.AddHours(3), CancellationToken.None);

        var mode = File.GetUnixFileMode(GetStateFilePath());

        Assert.IsTrue(mode.HasFlag(UnixFileMode.GroupWrite), "Group write permission should survive an atomic replace so a shared configuration volume keeps working!");
    }

    /// <summary>
    /// A successful write does not leave the temp file behind
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task StateStoreSetHighWaterMarkAsyncDoesNotLeaveTempFileBehind()
    {
        var store = CreateStore();

        await store.SetHighWaterMarkAsync(_mark, CancellationToken.None);

        Assert.IsFalse(File.Exists(GetStateFilePath() + ".tmp"), "A successful write must not leave a temp file behind!");
    }

    /// <summary>
    /// A failed move of the temp file onto the state file deletes the temp file
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [UnsupportedOSPlatform("windows")]
    public async Task StateStoreSetHighWaterMarkAsyncMoveFailureDeletesTempFile()
    {
        var store = CreateStore();

        Directory.CreateDirectory(GetStateFilePath());

        await Assert.ThrowsExactlyAsync<IOException>(() => store.SetHighWaterMarkAsync(_mark, CancellationToken.None),
                                                     "Moving the temp file onto a directory should surface the original failure!");

        Assert.IsFalse(File.Exists(GetStateFilePath() + ".tmp"), "A failed move must not leave the temp file behind!");
    }

    /// <summary>
    /// A failed move surfaces the original exception and the next write succeeds once the cause is removed
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [UnsupportedOSPlatform("windows")]
    public async Task StateStoreSetHighWaterMarkAsyncMoveFailureRethrowsOriginalException()
    {
        var store = CreateStore();

        Directory.CreateDirectory(GetStateFilePath());

        await Assert.ThrowsExactlyAsync<IOException>(() => store.SetHighWaterMarkAsync(_mark, CancellationToken.None),
                                                     "The original move failure should reach the caller unchanged in type!");

        Directory.Delete(GetStateFilePath());

        await store.SetHighWaterMarkAsync(_mark.AddHours(3), CancellationToken.None);

        var mark = await store.GetHighWaterMarkAsync(CancellationToken.None);

        Assert.AreEqual(_mark.AddHours(3), mark, "The write after the cause was removed should succeed and be read back!");
    }

    /// <summary>
    /// A corrupt state file is repaired by the next write
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task StateStoreWithCorruptFileIsRepairedByNextWrite()
    {
        Directory.CreateDirectory(_tempDirectory);

        await File.WriteAllTextAsync(GetStateFilePath(), "not json at all", _testContext.CancellationToken);

        var store = CreateStore();

        await store.SetHighWaterMarkAsync(_mark, CancellationToken.None);

        var mark = await store.GetHighWaterMarkAsync(CancellationToken.None);

        Assert.AreEqual(_mark, mark, "A write should replace the corrupt file with valid state!");
    }

    /// <summary>
    /// A file holding JSON <c>null</c> is treated as if no state existed
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task StateStoreWithNullContentReturnsNull()
    {
        Directory.CreateDirectory(_tempDirectory);

        await File.WriteAllTextAsync(GetStateFilePath(), "null", _testContext.CancellationToken);

        var store = CreateStore();

        var mark = await store.GetHighWaterMarkAsync(CancellationToken.None);

        Assert.IsNull(mark, "A state file holding null should be treated as missing state!");
    }

    /// <summary>
    /// Concurrent writes and reads leave the file readable and report one of the written values
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task StateStoreConcurrentAccessKeepsFileReadable()
    {
        var store = CreateStore();
        var tasks = new List<Task>();

        for (var index = 0; index < 25; index++)
        {
            var value = _mark.AddMinutes(index);

            tasks.Add(Task.Run(async () => await store.SetHighWaterMarkAsync(value, CancellationToken.None), _testContext.CancellationToken));
            tasks.Add(Task.Run(async () => await store.GetHighWaterMarkAsync(CancellationToken.None), _testContext.CancellationToken));
        }

        await Task.WhenAll(tasks);

        var mark = await store.GetHighWaterMarkAsync(CancellationToken.None);

        Assert.IsNotNull(mark, "Concurrent access should leave a readable state file!");
        Assert.IsTrue(mark.Value >= _mark && mark.Value <= _mark.AddMinutes(24), "The reported value should be one of the written values!");
    }

    /// <summary>
    /// A whole-second mark in the documented format is read exactly
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task StateStoreReadsWholeSecondMarkInDocumentedFormat()
    {
        Directory.CreateDirectory(_tempDirectory);

        await File.WriteAllTextAsync(GetStateFilePath(), "{ \"HighWaterMark\": \"2026-02-03T04:05:06+00:00\" }", _testContext.CancellationToken);

        var mark = await CreateStore().GetHighWaterMarkAsync(CancellationToken.None);

        Assert.AreEqual(_mark, mark, "The documented whole-second format should be read exactly!");
        Assert.AreEqual(_mark.UtcTicks, mark?.UtcTicks, "The ticks of the documented whole-second format should match!");
    }

    /// <summary>
    /// A mark with a 7-digit fraction in the documented format is read to the tick
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task StateStoreReadsFractionalMarkInDocumentedFormat()
    {
        Directory.CreateDirectory(_tempDirectory);

        await File.WriteAllTextAsync(GetStateFilePath(), "{ \"HighWaterMark\": \"2026-02-03T04:05:06.1234567+00:00\" }", _testContext.CancellationToken);

        var expected = _mark.AddTicks(1234567);
        var mark = await CreateStore().GetHighWaterMarkAsync(CancellationToken.None);

        Assert.AreEqual(expected, mark, "The documented fractional format should be read exactly!");
        Assert.AreEqual(expected.UtcTicks, mark?.UtcTicks, "The fraction should be kept to the tick!");
    }

    /// <summary>
    /// An additional unknown value in the state file does not prevent reading the mark
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task StateStoreWithUnknownValueReadsMark()
    {
        Directory.CreateDirectory(_tempDirectory);

        await File.WriteAllTextAsync(GetStateFilePath(), "{ \"HighWaterMark\": \"2026-02-03T04:05:06+00:00\", \"Comment\": \"x\" }", _testContext.CancellationToken);

        var mark = await CreateStore().GetHighWaterMarkAsync(CancellationToken.None);

        Assert.AreEqual(_mark, mark, "An unknown additional value should be ignored!");
    }

    /// <summary>
    /// The written file is a JSON object with the values SchemaVersion (the number 1) and HighWaterMark (ISO 8601), in this order
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task StateStoreWritesMarkInDocumentedFormat()
    {
        var mark = _mark.AddTicks(1234567);

        await CreateStore().SetHighWaterMarkAsync(mark, CancellationToken.None);

        await AssertVersionOneFileAsync(mark);
    }

    /// <summary>
    /// A corrupt state file is logged as an error naming both paths
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task StateStoreWithCorruptFileLogsErrorWithBothPaths()
    {
        Directory.CreateDirectory(_tempDirectory);

        await File.WriteAllTextAsync(GetStateFilePath(), CorruptContent, _testContext.CancellationToken);

        var logger = new RecordingStateStoreLogger();
        var mark = await CreateStore(logger).GetHighWaterMarkAsync(CancellationToken.None);

        Assert.IsNull(mark, "A corrupt state file should yield no mark!");
        Assert.HasCount(1, logger.Entries, "Exactly one entry should be logged!");
        Assert.AreEqual(LogLevel.Error, logger.Entries[0].Level, "The corrupt file should be logged as an error!");
        Assert.AreEqual(GetCorruptPreservedMessage(), logger.Entries[0].Message, "The message should name both paths!");
        Assert.IsInstanceOfType<JsonException>(logger.Entries[0].Exception, "The parse failure should be attached!");
    }

    /// <summary>
    /// A well-formed file whose mark is not a time is preserved as corrupt
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task StateStoreWithNonTimeMarkPreservesItAsCorrupt()
    {
        Directory.CreateDirectory(_tempDirectory);

        const string content = "{ \"HighWaterMark\": \"yesterday\" }";

        await File.WriteAllTextAsync(GetStateFilePath(), content, _testContext.CancellationToken);

        var mark = await CreateStore().GetHighWaterMarkAsync(CancellationToken.None);

        Assert.IsNull(mark, "A mark that is not a time should yield no mark!");
        Assert.IsFalse(File.Exists(GetStateFilePath()), "The file should have been moved aside!");

        var preserved = await File.ReadAllTextAsync(GetStateFilePath() + ".corrupt", _testContext.CancellationToken);

        Assert.AreEqual(content, preserved, "The original content should be preserved!");
    }

    /// <summary>
    /// A corrupt file that cannot be moved aside stays in place and the error is logged
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [UnsupportedOSPlatform("windows")]
    public async Task StateStoreWithUnmovableCorruptFileKeepsItAndLogsError()
    {
        await CreateUnmovableCorruptFileAsync();

        var logger = new RecordingStateStoreLogger();
        var mark = await CreateStore(logger).GetHighWaterMarkAsync(CancellationToken.None);

        Assert.IsNull(mark, "A corrupt state file should yield no mark!");
        Assert.IsTrue(File.Exists(GetStateFilePath()), "The file that could not be moved should stay in place!");

        var content = await File.ReadAllTextAsync(GetStateFilePath(), _testContext.CancellationToken);

        Assert.AreEqual(CorruptContent, content, "The content should be unchanged!");
        Assert.HasCount(1, logger.Entries, "Exactly one entry should be logged!");
        Assert.AreEqual(LogLevel.Error, logger.Entries[0].Level, "The failure should be logged as an error!");
        Assert.AreEqual(GetUnpreservedMessage(), logger.Entries[0].Message, "The message should say the file could not be preserved!");
    }

    /// <summary>
    /// A corrupt file that cannot be moved aside is replaced by the next write
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [UnsupportedOSPlatform("windows")]
    public async Task StateStoreWithUnmovableCorruptFileIsOverwrittenByNextWrite()
    {
        await CreateUnmovableCorruptFileAsync();

        var logger = new RecordingStateStoreLogger();
        var store = CreateStore(logger);

        await store.GetHighWaterMarkAsync(CancellationToken.None);
        await store.SetHighWaterMarkAsync(_mark, CancellationToken.None);

        var mark = await store.GetHighWaterMarkAsync(CancellationToken.None);

        Assert.AreEqual(_mark, mark, "The new mark should be read back!");
        Assert.HasCount(2, logger.Entries, "The write's own read should log the error again!");
        Assert.IsTrue(logger.Entries.TrueForAll(entry => entry.Level == LogLevel.Error && entry.Message == GetUnpreservedMessage()), "Both entries should be the could-not-be-preserved error!");
    }

    /// <summary>
    /// A state file that cannot be opened is logged as a warning and left in place
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [UnsupportedOSPlatform("windows")]
    public async Task StateStoreWithUnreadableFileLogsWarningAndKeepsIt()
    {
        await CreateStore().SetHighWaterMarkAsync(_mark, CancellationToken.None);

        var logger = new RecordingStateStoreLogger();
        DateTimeOffset? mark;

        await using (new FileStream(GetStateFilePath(), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            mark = await CreateStore(logger).GetHighWaterMarkAsync(CancellationToken.None);
        }

        Assert.IsNull(mark, "An unreadable state file should yield no mark!");
        Assert.HasCount(1, logger.Entries, "Exactly one entry should be logged!");
        Assert.AreEqual(LogLevel.Warning, logger.Entries[0].Level, "A read failure should be logged as a warning!");
        Assert.AreEqual($"Could not read state file {GetStateFilePath()}, starting fresh", logger.Entries[0].Message, "The message should name the state file!");
        Assert.IsInstanceOfType<IOException>(logger.Entries[0].Exception, "The read failure should be attached!");
        Assert.IsTrue(File.Exists(GetStateFilePath()), "The unreadable file should stay in place!");
        Assert.IsFalse(File.Exists(GetStateFilePath() + ".corrupt"), "The unreadable file should not be moved!");
    }

    /// <summary>
    /// A new corrupt file replaces an earlier preserved copy
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task StateStoreWithCorruptFileReplacesEarlierCorruptCopy()
    {
        Directory.CreateDirectory(_tempDirectory);

        await File.WriteAllTextAsync(GetStateFilePath() + ".corrupt", "earlier", _testContext.CancellationToken);
        await File.WriteAllTextAsync(GetStateFilePath(), CorruptContent, _testContext.CancellationToken);

        var logger = new RecordingStateStoreLogger();
        var mark = await CreateStore(logger).GetHighWaterMarkAsync(CancellationToken.None);

        Assert.IsNull(mark, "A corrupt state file should yield no mark!");

        var preserved = await File.ReadAllTextAsync(GetStateFilePath() + ".corrupt", _testContext.CancellationToken);

        Assert.AreEqual(CorruptContent, preserved, "The earlier copy should be replaced by the new content!");
        Assert.HasCount(1, logger.Entries, "Exactly one entry should be logged!");
        Assert.AreEqual(GetCorruptPreservedMessage(), logger.Entries[0].Message, "The corrupt-and-preserved error should be logged!");
    }

    /// <summary>
    /// A write over a file of version 1 produces the current layout with the new mark
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task StateStoreWithVersionOneFileWritesSchemaVersionOnNextWrite()
    {
        await WriteStateFileAsync("{ \"SchemaVersion\": 1, \"HighWaterMark\": \"2026-02-03T04:05:06+00:00\", \"Comment\": \"x\" }");

        var later = _mark.AddTicks(1234567).AddHours(3);

        await CreateStore().SetHighWaterMarkAsync(later, CancellationToken.None);

        await AssertVersionOneFileAsync(later);
    }

    /// <summary>
    /// A file of an earlier release without a schema version returns its mark, logs nothing and stays in place
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task StateStoreWithoutSchemaVersionReadsMarkAndKeepsFile()
    {
        const string content = "{ \"HighWaterMark\": \"2026-02-03T04:05:06+00:00\" }";

        await WriteStateFileAsync(content);

        var logger = new RecordingStateStoreLogger();
        var mark = await CreateStore(logger).GetHighWaterMarkAsync(CancellationToken.None);

        Assert.AreEqual(_mark, mark, "The stored mark should be returned!");
        Assert.IsEmpty(logger.Entries, "Nothing should be logged about the missing version!");
        await AssertFileKeptAsync(content);
    }

    /// <summary>
    /// A file with a null schema version is read as a file of version 1
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task StateStoreWithNullSchemaVersionReadsMark()
    {
        const string content = "{ \"SchemaVersion\": null, \"HighWaterMark\": \"2026-02-03T04:05:06+00:00\" }";

        await WriteStateFileAsync(content);

        var logger = new RecordingStateStoreLogger();
        var mark = await CreateStore(logger).GetHighWaterMarkAsync(CancellationToken.None);

        Assert.AreEqual(_mark, mark, "The stored mark should be returned!");
        Assert.IsEmpty(logger.Entries, "Nothing should be logged for a null version!");
        await AssertFileKeptAsync(content);
    }

    /// <summary>
    /// A file without a schema version, or with a null one, is upgraded by the next write
    /// </summary>
    /// <param name="content">File content</param>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    [DataRow("{ \"HighWaterMark\": \"2026-02-03T04:05:06+00:00\" }")]
    [DataRow("{ \"SchemaVersion\": null, \"HighWaterMark\": \"2026-02-03T04:05:06+00:00\" }")]
    public async Task StateStoreWithoutSchemaVersionIsUpgradedByNextWrite(string content)
    {
        await WriteStateFileAsync(content);

        var later = _mark.AddHours(3);

        await CreateStore().SetHighWaterMarkAsync(later, CancellationToken.None);

        await AssertVersionOneFileAsync(later);
    }

    /// <summary>
    /// A file of version 1 returns its mark, logs nothing and stays in place
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task StateStoreWithSchemaVersionOneReadsMark()
    {
        const string content = "{ \"SchemaVersion\": 1, \"HighWaterMark\": \"2026-02-03T04:05:06+00:00\" }";

        await WriteStateFileAsync(content);

        var logger = new RecordingStateStoreLogger();
        var mark = await CreateStore(logger).GetHighWaterMarkAsync(CancellationToken.None);

        Assert.AreEqual(_mark, mark, "The stored mark should be returned!");
        Assert.IsEmpty(logger.Entries, "Nothing should be logged for version 1!");
        await AssertFileKeptAsync(content);
    }

    /// <summary>
    /// A file of version 1 without a mark returns no mark, logs nothing and stays in place
    /// </summary>
    /// <param name="content">File content</param>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    [DataRow("{ \"SchemaVersion\": 1 }")]
    [DataRow("{ \"SchemaVersion\": 1, \"HighWaterMark\": null }")]
    public async Task StateStoreWithSchemaVersionOneWithoutMarkReturnsNull(string content)
    {
        await WriteStateFileAsync(content);

        var logger = new RecordingStateStoreLogger();
        var mark = await CreateStore(logger).GetHighWaterMarkAsync(CancellationToken.None);

        Assert.IsNull(mark, "A file without a mark should yield no mark!");
        Assert.IsEmpty(logger.Entries, "Nothing should be logged for a file without a mark!");
        await AssertFileKeptAsync(content);
    }

    /// <summary>
    /// A file of an unsupported version is moved aside as unsupported and yields no mark, even with a readable mark
    /// </summary>
    /// <param name="version">Schema version in the file</param>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    [DataRow(2)]
    [DataRow(0)]
    [DataRow(-1)]
    public async Task StateStoreWithUnsupportedSchemaVersionPreservesItAsUnsupported(int version)
    {
        var content = CreateContentWithVersion(version.ToString(CultureInfo.InvariantCulture));

        await WriteStateFileAsync(content);

        var logger = new RecordingStateStoreLogger();
        var mark = await CreateStore(logger).GetHighWaterMarkAsync(CancellationToken.None);

        Assert.IsNull(mark, "The mark of an unsupported file should not be used!");
        Assert.IsFalse(File.Exists(GetStateFilePath()), "The unsupported file should have been moved aside!");

        var preserved = await File.ReadAllTextAsync(GetStateFilePath() + ".unsupported", _testContext.CancellationToken);

        Assert.AreEqual(content, preserved, "The content should be preserved unchanged!");
        Assert.HasCount(1, logger.Entries, "Exactly one entry should be logged!");
        Assert.AreEqual(LogLevel.Error, logger.Entries[0].Level, "The unsupported file should be logged as an error!");
        Assert.AreEqual(GetUnsupportedPreservedMessage(version), logger.Entries[0].Message, "The message should name the path and the version!");
        Assert.IsNull(logger.Entries[0].Exception, "No exception should be attached!");
    }

    /// <summary>
    /// A file of an unsupported version is preserved as unsupported even when its mark is not a time
    /// </summary>
    /// <param name="content">File content</param>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    [DataRow("{ \"SchemaVersion\": 2, \"HighWaterMark\": 1767000000 }")]
    [DataRow("{ \"SchemaVersion\": 2, \"HighWaterMark\": { \"Utc\": \"x\" } }")]
    [DataRow("{ \"SchemaVersion\": 2, \"HighWaterMark\": \"not a time\" }")]
    public async Task StateStoreWithUnsupportedSchemaVersionAndNonTimeMarkPreservesItAsUnsupported(string content)
    {
        await WriteStateFileAsync(content);

        var logger = new RecordingStateStoreLogger();
        var mark = await CreateStore(logger).GetHighWaterMarkAsync(CancellationToken.None);

        Assert.IsNull(mark, "The mark of an unsupported file should not be used!");
        Assert.IsFalse(File.Exists(GetStateFilePath()), "The unsupported file should have been moved aside!");
        Assert.IsFalse(File.Exists(GetStateFilePath() + ".corrupt"), "The file should not be treated as corrupt!");

        var preserved = await File.ReadAllTextAsync(GetStateFilePath() + ".unsupported", _testContext.CancellationToken);

        Assert.AreEqual(content, preserved, "The content should be preserved unchanged!");
        Assert.HasCount(1, logger.Entries, "Exactly one entry should be logged!");
        Assert.AreEqual(LogLevel.Error, logger.Entries[0].Level, "The unsupported file should be logged as an error!");
        Assert.AreEqual(GetUnsupportedPreservedMessage(2), logger.Entries[0].Message, "The message should name the path and the version!");
        Assert.IsNull(logger.Entries[0].Exception, "No exception should be attached!");
    }

    /// <summary>
    /// A file of version 1 whose mark is not a time is preserved as corrupt
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task StateStoreWithSchemaVersionOneAndNonTimeMarkPreservesItAsCorrupt()
    {
        const string content = "{ \"SchemaVersion\": 1, \"HighWaterMark\": \"not a time\" }";

        await WriteStateFileAsync(content);

        var logger = new RecordingStateStoreLogger();
        var mark = await CreateStore(logger).GetHighWaterMarkAsync(CancellationToken.None);

        Assert.IsNull(mark, "A mark that is not a time should yield no mark!");
        Assert.IsFalse(File.Exists(GetStateFilePath()), "The file should have been moved aside!");
        Assert.IsFalse(File.Exists(GetStateFilePath() + ".unsupported"), "The file should not be treated as unsupported!");

        var preserved = await File.ReadAllTextAsync(GetStateFilePath() + ".corrupt", _testContext.CancellationToken);

        Assert.AreEqual(content, preserved, "The original content should be preserved as corrupt!");
        Assert.HasCount(1, logger.Entries, "Exactly one entry should be logged!");
        Assert.AreEqual(GetCorruptPreservedMessage(), logger.Entries[0].Message, "The existing corrupt message should be logged!");
        Assert.IsInstanceOfType<JsonException>(logger.Entries[0].Exception, "The parse failure should be attached!");
    }

    /// <summary>
    /// A file starting with a UTF-8 byte order mark returns its mark, logs nothing and stays unchanged
    /// </summary>
    /// <param name="json">JSON text of the file</param>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    [DataRow("{ \"SchemaVersion\": 1, \"HighWaterMark\": \"2026-02-03T04:05:06+00:00\" }")]
    [DataRow("{ \"HighWaterMark\": \"2026-02-03T04:05:06+00:00\" }")]
    public async Task StateStoreWithByteOrderMarkReadsMarkAndKeepsFile(string json)
    {
        var bytes = await WriteStateFileWithByteOrderMarkAsync(json);

        var logger = new RecordingStateStoreLogger();
        var mark = await CreateStore(logger).GetHighWaterMarkAsync(CancellationToken.None);

        Assert.AreEqual(_mark, mark, "The stored mark should be returned despite the byte order mark!");
        Assert.IsEmpty(logger.Entries, "Nothing should be logged for a file with a byte order mark!");

        var actual = await File.ReadAllBytesAsync(GetStateFilePath(), _testContext.CancellationToken);

        Assert.AreSequenceEqual(bytes, actual, "The file should stay in place unchanged!");
        Assert.IsFalse(File.Exists(GetStateFilePath() + ".corrupt"), "The file should not be preserved as corrupt!");
    }

    /// <summary>
    /// A file starting with a UTF-8 byte order mark and holding an unsupported version is preserved as unsupported
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task StateStoreWithByteOrderMarkAndUnsupportedSchemaVersionPreservesItAsUnsupported()
    {
        var bytes = await WriteStateFileWithByteOrderMarkAsync(CreateContentWithVersion("2"));

        var logger = new RecordingStateStoreLogger();
        var mark = await CreateStore(logger).GetHighWaterMarkAsync(CancellationToken.None);

        Assert.IsNull(mark, "The mark of an unsupported file should not be used!");
        Assert.IsFalse(File.Exists(GetStateFilePath()), "The unsupported file should have been moved aside!");
        Assert.IsFalse(File.Exists(GetStateFilePath() + ".corrupt"), "The file should not be treated as corrupt!");

        var preserved = await File.ReadAllBytesAsync(GetStateFilePath() + ".unsupported", _testContext.CancellationToken);

        Assert.AreSequenceEqual(bytes, preserved, "The content should be preserved unchanged!");
        Assert.HasCount(1, logger.Entries, "Exactly one entry should be logged!");
        Assert.AreEqual(GetUnsupportedPreservedMessage(2), logger.Entries[0].Message, "The message should name the path and the version!");
    }

    /// <summary>
    /// A new unsupported file replaces an earlier preserved copy
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task StateStoreWithUnsupportedSchemaVersionReplacesEarlierUnsupportedCopy()
    {
        var content = CreateContentWithVersion("2");

        Directory.CreateDirectory(_tempDirectory);

        await File.WriteAllTextAsync(GetStateFilePath() + ".unsupported", "earlier", _testContext.CancellationToken);
        await WriteStateFileAsync(content);

        var logger = new RecordingStateStoreLogger();
        var mark = await CreateStore(logger).GetHighWaterMarkAsync(CancellationToken.None);

        Assert.IsNull(mark, "An unsupported file should yield no mark!");

        var preserved = await File.ReadAllTextAsync(GetStateFilePath() + ".unsupported", _testContext.CancellationToken);

        Assert.AreEqual(content, preserved, "The earlier copy should be replaced by the new content!");
        Assert.HasCount(1, logger.Entries, "Exactly one entry should be logged!");
        Assert.AreEqual(GetUnsupportedPreservedMessage(2), logger.Entries[0].Message, "The unsupported-and-preserved error should be logged!");
    }

    /// <summary>
    /// A write that finds an unsupported file logs the error once, moves the file and writes the current layout
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task StateStoreWithUnsupportedSchemaVersionIsReplacedByNextWrite()
    {
        var content = CreateContentWithVersion("2");

        await WriteStateFileAsync(content);

        var logger = new RecordingStateStoreLogger();
        var later = _mark.AddHours(3);

        await CreateStore(logger).SetHighWaterMarkAsync(later, CancellationToken.None);

        Assert.HasCount(1, logger.Entries, "The write's own read should log the error once!");
        Assert.AreEqual(GetUnsupportedPreservedMessage(2), logger.Entries[0].Message, "The unsupported-and-preserved error should be logged!");

        var preserved = await File.ReadAllTextAsync(GetStateFilePath() + ".unsupported", _testContext.CancellationToken);

        Assert.AreEqual(content, preserved, "The unsupported file should have been preserved unchanged!");
        await AssertVersionOneFileAsync(later);
    }

    /// <summary>
    /// An unsupported file that cannot be moved aside stays in place and the error is logged
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [UnsupportedOSPlatform("windows")]
    public async Task StateStoreWithUnmovableUnsupportedFileKeepsItAndLogsError()
    {
        var content = await CreateUnmovableUnsupportedFileAsync();

        var logger = new RecordingStateStoreLogger();
        var mark = await CreateStore(logger).GetHighWaterMarkAsync(CancellationToken.None);

        Assert.IsNull(mark, "An unsupported file should yield no mark!");
        Assert.IsTrue(File.Exists(GetStateFilePath()), "The file that could not be moved should stay in place!");

        var actual = await File.ReadAllTextAsync(GetStateFilePath(), _testContext.CancellationToken);

        Assert.AreEqual(content, actual, "The content should be unchanged!");
        Assert.HasCount(1, logger.Entries, "Exactly one entry should be logged!");
        Assert.AreEqual(LogLevel.Error, logger.Entries[0].Level, "The failure should be logged as an error!");
        Assert.AreEqual(GetUnsupportedUnpreservedMessage(2), logger.Entries[0].Message, "The message should say the file could not be preserved!");
        Assert.IsNull(logger.Entries[0].Exception, "No exception should be attached!");
    }

    /// <summary>
    /// An unsupported file that cannot be moved aside is replaced by the next write
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [UnsupportedOSPlatform("windows")]
    public async Task StateStoreWithUnmovableUnsupportedFileIsOverwrittenByNextWrite()
    {
        await CreateUnmovableUnsupportedFileAsync();

        var logger = new RecordingStateStoreLogger();
        var store = CreateStore(logger);
        var later = _mark.AddHours(3);

        await store.GetHighWaterMarkAsync(CancellationToken.None);
        await store.SetHighWaterMarkAsync(later, CancellationToken.None);

        var mark = await store.GetHighWaterMarkAsync(CancellationToken.None);

        Assert.AreEqual(later, mark, "The new mark should be read back!");
        Assert.HasCount(2, logger.Entries, "The write's own read should log the error again!");
        Assert.IsTrue(logger.Entries.TrueForAll(entry => entry.Level == LogLevel.Error && entry.Message == GetUnsupportedUnpreservedMessage(2)), "Both entries should be the could-not-be-preserved error!");
        await AssertVersionOneFileAsync(later);
    }

    /// <summary>
    /// A schema version that is not an integer literal in the 32-bit range makes the file corrupt
    /// </summary>
    /// <param name="version">Raw JSON text of the schema version</param>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    [DataRow("\"1\"")]
    [DataRow("1.5")]
    [DataRow("1.0")]
    [DataRow("true")]
    [DataRow("2147483648")]
    public async Task StateStoreWithNonIntegerSchemaVersionPreservesItAsCorrupt(string version)
    {
        var content = CreateContentWithVersion(version);

        await WriteStateFileAsync(content);

        var logger = new RecordingStateStoreLogger();
        var mark = await CreateStore(logger).GetHighWaterMarkAsync(CancellationToken.None);

        Assert.IsNull(mark, "A file with a non-integer version should yield no mark!");
        Assert.IsFalse(File.Exists(GetStateFilePath()), "The corrupt file should have been moved aside!");

        var preserved = await File.ReadAllTextAsync(GetStateFilePath() + ".corrupt", _testContext.CancellationToken);

        Assert.AreEqual(content, preserved, "The original content should be preserved as corrupt!");
        Assert.IsFalse(File.Exists(GetStateFilePath() + ".unsupported"), "The file should not be treated as unsupported!");
        Assert.HasCount(1, logger.Entries, "Exactly one entry should be logged!");
        Assert.AreEqual(LogLevel.Error, logger.Entries[0].Level, "The corrupt file should be logged as an error!");
        Assert.AreEqual(GetCorruptPreservedMessage(), logger.Entries[0].Message, "The existing corrupt message should be logged!");
        Assert.IsInstanceOfType<JsonException>(logger.Entries[0].Exception, "The parse failure should be attached!");
    }

    /// <summary>
    /// A differently cased schema version is an unknown value, so the file counts as version 1
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task StateStoreWithDifferentlyCasedSchemaVersionReadsMark()
    {
        const string content = "{ \"schemaversion\": 2, \"HighWaterMark\": \"2026-02-03T04:05:06+00:00\" }";

        await WriteStateFileAsync(content);

        var logger = new RecordingStateStoreLogger();
        var mark = await CreateStore(logger).GetHighWaterMarkAsync(CancellationToken.None);

        Assert.AreEqual(_mark, mark, "The stored mark should be returned!");
        Assert.IsEmpty(logger.Entries, "Nothing should be logged for an unknown value!");
        await AssertFileKeptAsync(content);
    }

    /// <summary>
    /// With a duplicated schema version the last occurrence counts
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task StateStoreWithDuplicateSchemaVersionUsesLastOccurrence()
    {
        const string content = "{ \"SchemaVersion\": 1, \"SchemaVersion\": 2, \"HighWaterMark\": \"2026-02-03T04:05:06+00:00\" }";

        await WriteStateFileAsync(content);

        var logger = new RecordingStateStoreLogger();
        var mark = await CreateStore(logger).GetHighWaterMarkAsync(CancellationToken.None);

        Assert.IsNull(mark, "The last occurrence is unsupported, so no mark should be returned!");
        Assert.IsFalse(File.Exists(GetStateFilePath()), "The unsupported file should have been moved aside!");

        var preserved = await File.ReadAllTextAsync(GetStateFilePath() + ".unsupported", _testContext.CancellationToken);

        Assert.AreEqual(content, preserved, "The content should be preserved unchanged!");
        Assert.HasCount(1, logger.Entries, "Exactly one entry should be logged!");
        Assert.AreEqual(GetUnsupportedPreservedMessage(2), logger.Entries[0].Message, "The unsupported-and-preserved error should be logged!");
    }

    /// <summary>
    /// Create the content of a state file with a readable mark and the given raw schema version
    /// </summary>
    /// <param name="version">Raw JSON text of the schema version</param>
    /// <returns>File content</returns>
    private static string CreateContentWithVersion(string version)
    {
        return $"{{ \"SchemaVersion\": {version}, \"HighWaterMark\": \"2026-02-03T04:05:06+00:00\" }}";
    }

    /// <summary>
    /// Create a corrupt state file and a non-empty directory in the way of its preserved copy
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    private async Task CreateUnmovableCorruptFileAsync()
    {
        Directory.CreateDirectory(_tempDirectory);
        Directory.CreateDirectory(GetStateFilePath() + ".corrupt");

        await File.WriteAllTextAsync(Path.Combine(GetStateFilePath() + ".corrupt", "obstacle.txt"), "x", _testContext.CancellationToken);
        await File.WriteAllTextAsync(GetStateFilePath(), CorruptContent, _testContext.CancellationToken);
    }

    /// <summary>
    /// Get the expected message for a corrupt file that was preserved
    /// </summary>
    /// <returns>Expected message</returns>
    private string GetCorruptPreservedMessage()
    {
        return $"State file {GetStateFilePath()} is corrupt, preserved as {GetStateFilePath()}.corrupt; starting fresh with no high-water mark";
    }

    /// <summary>
    /// Get the expected message for a corrupt file that could not be preserved
    /// </summary>
    /// <returns>Expected message</returns>
    private string GetUnpreservedMessage()
    {
        return $"State file {GetStateFilePath()} is corrupt and could not be preserved; starting fresh with no high-water mark";
    }

    /// <summary>
    /// Create an unsupported state file and a non-empty directory in the way of its preserved copy
    /// </summary>
    /// <returns>Returns the content of the state file</returns>
    private async Task<string> CreateUnmovableUnsupportedFileAsync()
    {
        var content = CreateContentWithVersion("2");

        Directory.CreateDirectory(_tempDirectory);
        Directory.CreateDirectory(GetStateFilePath() + ".unsupported");

        await File.WriteAllTextAsync(Path.Combine(GetStateFilePath() + ".unsupported", "obstacle.txt"), "x", _testContext.CancellationToken);
        await File.WriteAllTextAsync(GetStateFilePath(), content, _testContext.CancellationToken);

        return content;
    }

    /// <summary>
    /// Write the state file in the temporary directory
    /// </summary>
    /// <param name="content">File content</param>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    private async Task WriteStateFileAsync(string content)
    {
        Directory.CreateDirectory(_tempDirectory);

        await File.WriteAllTextAsync(GetStateFilePath(), content, _testContext.CancellationToken);
    }

    /// <summary>
    /// Write the state file in the temporary directory, starting with a UTF-8 byte order mark
    /// </summary>
    /// <param name="json">JSON text of the file</param>
    /// <returns>Returns the bytes written</returns>
    private async Task<byte[]> WriteStateFileWithByteOrderMarkAsync(string json)
    {
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(System.Text.Encoding.UTF8.GetBytes(json)).ToArray();

        Directory.CreateDirectory(_tempDirectory);

        await File.WriteAllBytesAsync(GetStateFilePath(), bytes, _testContext.CancellationToken);

        return bytes;
    }

    /// <summary>
    /// Assert that the state file is still in place with the given content
    /// </summary>
    /// <param name="expected">Expected content</param>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    private async Task AssertFileKeptAsync(string expected)
    {
        Assert.IsTrue(File.Exists(GetStateFilePath()), "The file should stay in place!");

        var actual = await File.ReadAllTextAsync(GetStateFilePath(), _testContext.CancellationToken);

        Assert.AreEqual(expected, actual, "The content should be unchanged!");
        Assert.IsFalse(File.Exists(GetStateFilePath() + ".corrupt"), "The file should not be preserved as corrupt!");
        Assert.IsFalse(File.Exists(GetStateFilePath() + ".unsupported"), "The file should not be preserved as unsupported!");
    }

    /// <summary>
    /// Assert that the state file holds exactly SchemaVersion 1 and then the given mark
    /// </summary>
    /// <param name="expectedMark">Expected mark</param>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    private async Task AssertVersionOneFileAsync(DateTimeOffset expectedMark)
    {
        var content = await File.ReadAllTextAsync(GetStateFilePath(), _testContext.CancellationToken);

        using var document = JsonDocument.Parse(content);

        var root = document.RootElement;

        Assert.AreEqual(JsonValueKind.Object, root.ValueKind, "The state file should hold a JSON object!");

        var properties = root.EnumerateObject().ToList();

        Assert.HasCount(2, properties, "The state file should hold exactly two values!");
        Assert.AreEqual("SchemaVersion", properties[0].Name, "The first value should be named SchemaVersion!");
        Assert.AreEqual(JsonValueKind.Number, properties[0].Value.ValueKind, "The version should be written as a number!");
        Assert.AreEqual("1", properties[0].Value.GetRawText(), "The version should be the number 1!");
        Assert.AreEqual("HighWaterMark", properties[1].Name, "The second value should be named HighWaterMark!");
        Assert.AreEqual(JsonValueKind.String, properties[1].Value.ValueKind, "The mark should be written as a string!");

        var parsed = DateTimeOffset.Parse(properties[1].Value.GetString()!, CultureInfo.InvariantCulture);

        Assert.AreEqual(expectedMark.UtcTicks, parsed.UtcTicks, "The written time should parse back to the tick!");
    }

    /// <summary>
    /// Get the expected message for an unsupported file that was preserved
    /// </summary>
    /// <param name="version">Schema version in the file</param>
    /// <returns>Expected message</returns>
    private string GetUnsupportedPreservedMessage(int version)
    {
        return $"State file {GetStateFilePath()} has unsupported schema version {version.ToString(CultureInfo.InvariantCulture)}, preserved as {GetStateFilePath()}.unsupported; starting fresh with no high-water mark";
    }

    /// <summary>
    /// Get the expected message for an unsupported file that could not be preserved
    /// </summary>
    /// <param name="version">Schema version in the file</param>
    /// <returns>Expected message</returns>
    private string GetUnsupportedUnpreservedMessage(int version)
    {
        return $"State file {GetStateFilePath()} has unsupported schema version {version.ToString(CultureInfo.InvariantCulture)} and could not be preserved; starting fresh with no high-water mark";
    }

    /// <summary>
    /// Create a state store working in the temporary directory
    /// </summary>
    /// <returns>The state store under test</returns>
    private StateStore CreateStore()
    {
        return CreateStore(NullLogger<StateStore>.Instance);
    }

    /// <summary>
    /// Create a state store working in the temporary directory
    /// </summary>
    /// <param name="logger">Logger of the store</param>
    /// <returns>The state store under test</returns>
    private StateStore CreateStore(ILogger<StateStore> logger)
    {
        var options = Options.Create(new StateOptions
                                     {
                                         Directory = _tempDirectory
                                     });

        return new StateStore(options, logger);
    }

    /// <summary>
    /// Get the path of the state file in the temporary directory
    /// </summary>
    /// <returns>Path of the state file</returns>
    private string GetStateFilePath()
    {
        return Path.Combine(_tempDirectory, "state.json");
    }

    #endregion // Methods
}