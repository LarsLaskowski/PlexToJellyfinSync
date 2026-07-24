using System.Globalization;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using PlexToJellyfinSync.Core.Models;
using PlexToJellyfinSync.Core.Options;
using PlexToJellyfinSync.Service.Logging;

namespace PlexToJellyfinSync.Tests;

/// <summary>
/// Tests for <see cref="InMemoryLogStore"/>
/// </summary>
[TestClass]
public sealed class InMemoryLogStoreTests
{
    #region Methods

    /// <summary>
    /// A new store is empty
    /// </summary>
    [TestMethod]
    public void InMemoryLogStoreNewInstanceIsEmpty()
    {
        var store = CreateStore(10);

        Assert.IsEmpty(store.GetEntries(), "A new store should not hold any entry!");
    }

    /// <summary>
    /// Entries are reported in insertion order
    /// </summary>
    [TestMethod]
    public void InMemoryLogStoreKeepsInsertionOrder()
    {
        var store = CreateStore(10);

        store.Add(CreateEntry("first"));
        store.Add(CreateEntry("second"));

        var entries = store.GetEntries();

        Assert.HasCount(2, entries, "Both entries should be buffered!");
        Assert.AreEqual("first", entries[0].Message, "The oldest entry should come first!");
        Assert.AreEqual("second", entries[1].Message, "The newest entry should come last!");
    }

    /// <summary>
    /// Exceeding the buffer size drops the oldest entries
    /// </summary>
    [TestMethod]
    public void InMemoryLogStoreBeyondCapacityDropsOldestEntries()
    {
        var store = CreateStore(3);

        for (var index = 0; index < 6; index++)
        {
            store.Add(CreateEntry(index.ToString(CultureInfo.InvariantCulture)));
        }

        var entries = store.GetEntries();

        Assert.HasCount(3, entries, "The buffer should not grow beyond its capacity!");
        Assert.AreEqual("3", entries[0].Message, "The oldest surviving entry should be the fourth one!");
        Assert.AreEqual("5", entries[2].Message, "The newest entry should be kept!");
    }

    /// <summary>
    /// A non-positive buffer size still keeps the newest entry
    /// </summary>
    [TestMethod]
    public void InMemoryLogStoreNonPositiveCapacityKeepsOneEntry()
    {
        var store = CreateStore(0);

        store.Add(CreateEntry("first"));
        store.Add(CreateEntry("second"));

        var entries = store.GetEntries();

        Assert.HasCount(1, entries, "A non-positive buffer size should be clamped to one entry!");
        Assert.AreEqual("second", entries[0].Message, "The newest entry should be kept!");
    }

    /// <summary>
    /// The reported list is a snapshot that is unaffected by later additions
    /// </summary>
    [TestMethod]
    public void InMemoryLogStoreEntriesAreDetachedSnapshot()
    {
        var store = CreateStore(10);

        store.Add(CreateEntry("first"));

        var entries = store.GetEntries();

        store.Add(CreateEntry("second"));

        Assert.HasCount(1, entries, "An earlier snapshot should not see later entries!");
        Assert.HasCount(2, store.GetEntries(), "A new snapshot should see both entries!");
    }

    /// <summary>
    /// Every addition notifies the subscribers with the added entry
    /// </summary>
    [TestMethod]
    public void InMemoryLogStoreAddRaisesEntryAdded()
    {
        var store = CreateStore(10);
        var received = new List<LogEntry>();

        store.EntryAdded += entry => received.Add(entry);

        store.Add(CreateEntry("first"));
        store.Add(CreateEntry("second"));

        Assert.HasCount(2, received, "Every addition should notify the subscribers!");
        Assert.AreEqual("second", received[1].Message, "The added entry should be handed to the subscribers!");
    }

    /// <summary>
    /// Concurrent additions neither throw nor exceed the configured capacity
    /// </summary>
    [TestMethod]
    public void InMemoryLogStoreConcurrentAddsStayWithinCapacity()
    {
        var store = CreateStore(50);

        Parallel.For(0, 1000, index => store.Add(CreateEntry(index.ToString(CultureInfo.InvariantCulture))));

        Assert.HasCount(50, store.GetEntries(), "Concurrent additions should respect the capacity!");
    }

    /// <summary>
    /// Reading while writing never observes a torn buffer
    /// </summary>
    [TestMethod]
    public void InMemoryLogStoreConcurrentReadsStayWithinCapacity()
    {
        var store = CreateStore(20);

        Parallel.For(0,
                     1000,
                     index =>
                     {
                         if (index % 2 == 0)
                         {
                             store.Add(CreateEntry(index.ToString(CultureInfo.InvariantCulture)));
                         }
                         else
                         {
                             Assert.IsTrue(store.GetEntries().Count <= 20, "A snapshot should never exceed the capacity!");
                         }
                     });

        Assert.HasCount(20, store.GetEntries(), "The buffer should be full afterwards!");
    }

    /// <summary>
    /// Create a log store with the given buffer size
    /// </summary>
    /// <param name="bufferSize">Configured buffer size</param>
    /// <returns>The log store under test</returns>
    private static InMemoryLogStore CreateStore(int bufferSize)
    {
        var options = Options.Create(new DashboardOptions
                                     {
                                         LogBufferSize = bufferSize
                                     });

        return new InMemoryLogStore(options);
    }

    /// <summary>
    /// Create a log entry carrying the given message
    /// </summary>
    /// <param name="message">Message of the entry</param>
    /// <returns>The log entry</returns>
    private static LogEntry CreateEntry(string message)
    {
        return new LogEntry
               {
                   Timestamp = DateTimeOffset.UtcNow,
                   Level = LogLevel.Information,
                   Category = "Test",
                   Message = message
               };
    }

    #endregion // Methods
}