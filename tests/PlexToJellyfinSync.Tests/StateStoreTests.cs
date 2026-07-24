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

    private static readonly DateTimeOffset _mark = new(2026, 2, 3, 4, 5, 6, TimeSpan.Zero);

    private string _tempDirectory = string.Empty;

    #endregion // Fields

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
        var content = await File.ReadAllTextAsync(GetStateFilePath());

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

        await File.WriteAllTextAsync(GetStateFilePath(), "{ \"HighWaterMark\": ");

        var store = CreateStore();

        var mark = await store.GetHighWaterMarkAsync(CancellationToken.None);

        Assert.IsNull(mark, "A corrupt state file should be treated as missing state!");
    }

    /// <summary>
    /// A corrupt state file is repaired by the next write
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task StateStoreWithCorruptFileIsRepairedByNextWrite()
    {
        Directory.CreateDirectory(_tempDirectory);

        await File.WriteAllTextAsync(GetStateFilePath(), "not json at all");

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

        await File.WriteAllTextAsync(GetStateFilePath(), "null");

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

            tasks.Add(Task.Run(async () => await store.SetHighWaterMarkAsync(value, CancellationToken.None)));
            tasks.Add(Task.Run(async () => await store.GetHighWaterMarkAsync(CancellationToken.None)));
        }

        await Task.WhenAll(tasks);

        var mark = await store.GetHighWaterMarkAsync(CancellationToken.None);

        Assert.IsNotNull(mark, "Concurrent access should leave a readable state file!");
        Assert.IsTrue(mark.Value >= _mark && mark.Value <= _mark.AddMinutes(24), "The reported value should be one of the written values!");
    }

    /// <summary>
    /// Create a state store working in the temporary directory
    /// </summary>
    /// <returns>The state store under test</returns>
    private StateStore CreateStore()
    {
        var options = Options.Create(new StateOptions
                                     {
                                         Directory = _tempDirectory
                                     });

        return new StateStore(options, NullLogger<StateStore>.Instance);
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