using Microsoft.Extensions.Logging;

using PlexToJellyfinSync.Components.Pages;
using PlexToJellyfinSync.Core.Models;

namespace PlexToJellyfinSync.Tests;

/// <summary>
/// Tests for <see cref="LogFilterCache"/>
/// </summary>
[TestClass]
public sealed class LogFilterCacheTests
{
    #region Methods

    /// <summary>
    /// Reset builds the filtered view newest first from the given entries
    /// </summary>
    [TestMethod]
    public void LogFilterCacheResetBuildsFilteredViewNewestFirst()
    {
        var cache = new LogFilterCache();
        var entries = new List<LogEntry>
                      {
                          CreateEntry(LogLevel.Information, "first"),
                          CreateEntry(LogLevel.Information, "second")
                      };

        cache.Reset(entries, LogLevel.Trace, null);

        Assert.HasCount(2, cache.Filtered, "Both entries should be present!");
        Assert.AreEqual("second", cache.Filtered[0].Message, "The newest entry should come first!");
    }

    /// <summary>
    /// A matching appended entry is inserted at the front of the filtered view
    /// </summary>
    [TestMethod]
    public void LogFilterCacheAppendMatchingEntryInsertsAtFront()
    {
        var cache = new LogFilterCache();
        var entries = new List<LogEntry>
                      {
                          CreateEntry(LogLevel.Information, "first")
                      };

        cache.Reset(entries, LogLevel.Trace, null);

        var added = CreateEntry(LogLevel.Information, "second");
        var updatedEntries = new List<LogEntry>(entries)
                             {
                                 added
                             };

        cache.Append(added, updatedEntries);

        Assert.HasCount(2, cache.Filtered, "Both entries should be present after the append!");
        Assert.AreEqual("second", cache.Filtered[0].Message, "The newly appended entry should be newest!");
    }

    /// <summary>
    /// An appended entry that fails the filter is not added to the filtered view, even though the buffer still tracks it
    /// </summary>
    [TestMethod]
    public void LogFilterCacheAppendNonMatchingEntryIsExcluded()
    {
        var cache = new LogFilterCache();
        var entries = new List<LogEntry>
                      {
                          CreateEntry(LogLevel.Information, "first")
                      };

        cache.Reset(entries, LogLevel.Warning, null);

        var added = CreateEntry(LogLevel.Information, "second");
        var updatedEntries = new List<LogEntry>(entries)
                             {
                                 added
                             };

        cache.Append(added, updatedEntries);

        Assert.IsEmpty(cache.Filtered, "An entry below the minimum level should not appear in the filtered view!");
    }

    /// <summary>
    /// When an append evicts the oldest entry from the buffer, that entry is dropped from the filtered view too
    /// </summary>
    [TestMethod]
    public void LogFilterCacheAppendEvictingOldestEntryRemovesItFromFilteredView()
    {
        var cache = new LogFilterCache();
        var oldest = CreateEntry(LogLevel.Information, "oldest");
        var middle = CreateEntry(LogLevel.Information, "middle");

        cache.Reset(new List<LogEntry>
                    {
                        oldest,
                        middle
                    },
                    LogLevel.Trace,
                    null);

        var added = CreateEntry(LogLevel.Information, "newest");
        var updatedEntries = new List<LogEntry>
                             {
                                 middle,
                                 added
                             };

        cache.Append(added, updatedEntries);

        Assert.HasCount(2, cache.Filtered, "The evicted entry should have been replaced by the newly added one!");
        Assert.IsFalse(cache.Filtered.Contains(oldest), "The entry evicted from the buffer should no longer be shown!");
        Assert.AreEqual("newest", cache.Filtered[0].Message, "The newest entry should come first!");
        Assert.AreEqual("middle", cache.Filtered[1].Message, "The surviving entry should still follow it!");
    }

    /// <summary>
    /// A single-entry buffer (the minimum LogBufferSize clamps to) evicts and replaces its sole entry without throwing
    /// </summary>
    [TestMethod]
    public void LogFilterCacheAppendWithSingleEntryBufferReplacesEntry()
    {
        var cache = new LogFilterCache();
        var only = CreateEntry(LogLevel.Information, "only");

        cache.Reset(new List<LogEntry>
                    {
                        only
                    },
                    LogLevel.Trace,
                    null);

        var added = CreateEntry(LogLevel.Information, "newest");
        var updatedEntries = new List<LogEntry>
                             {
                                 added
                             };

        cache.Append(added, updatedEntries);

        Assert.HasCount(1, cache.Filtered, "A single-entry buffer should still hold exactly one entry after the append!");
        Assert.AreEqual("newest", cache.Filtered[0].Message, "The sole entry should have been replaced by the newly added one!");
    }

    /// <summary>
    /// Changing the minimum level rebuilds the filtered view
    /// </summary>
    [TestMethod]
    public void LogFilterCacheApplyFilterWithChangedLevelRebuildsFilteredView()
    {
        var cache = new LogFilterCache();
        var entries = new List<LogEntry>
                      {
                          CreateEntry(LogLevel.Debug, "debug"),
                          CreateEntry(LogLevel.Warning, "warning")
                      };

        cache.Reset(entries, LogLevel.Warning, null);
        cache.ApplyFilter(entries, LogLevel.Trace, null);

        Assert.HasCount(2, cache.Filtered, "Lowering the minimum level should surface the previously excluded entry!");
    }

    /// <summary>
    /// Applying the same level and filter again does not change the cached instance
    /// </summary>
    [TestMethod]
    public void LogFilterCacheApplyFilterWithUnchangedValuesKeepsCachedInstance()
    {
        var cache = new LogFilterCache();
        var entries = new List<LogEntry>
                      {
                          CreateEntry(LogLevel.Information, "first")
                      };

        cache.Reset(entries, LogLevel.Trace, null);

        var before = cache.Filtered;

        cache.ApplyFilter(entries, LogLevel.Trace, null);

        Assert.AreSame(before, cache.Filtered, "Reapplying the same level and filter should not rebuild the cache!");
    }

    /// <summary>
    /// When a resync is pending, ApplyFilter rebuilds from the given live entries rather than the stale cached ones -
    /// otherwise a pause/unpause without an intervening entry would clear the pending resync using outdated data
    /// </summary>
    [TestMethod]
    public void LogFilterCacheApplyFilterWithPendingResyncUsesLiveEntries()
    {
        var cache = new LogFilterCache();
        var stale = CreateEntry(LogLevel.Information, "stale");

        cache.Reset(new List<LogEntry>
                    {
                        stale
                    },
                    LogLevel.Trace,
                    null);

        cache.MarkStale();

        var live = CreateEntry(LogLevel.Information, "live");
        var liveEntries = new List<LogEntry>
                          {
                              live
                          };

        cache.ApplyFilter(liveEntries, LogLevel.Trace, null);

        Assert.HasCount(1, cache.Filtered, "The rebuild should reflect the live entries, not the stale cached ones!");
        Assert.AreEqual("live", cache.Filtered[0].Message, "The entry added while stale should now be visible!");
    }

    /// <summary>
    /// After entries were skipped, the next append performs a full resync instead of an incorrect incremental update
    /// </summary>
    [TestMethod]
    public void LogFilterCacheAppendAfterMarkStaleResyncsFully()
    {
        var cache = new LogFilterCache();
        var first = CreateEntry(LogLevel.Information, "first");
        var second = CreateEntry(LogLevel.Information, "second");

        cache.Reset(new List<LogEntry>
                    {
                        first
                    },
                    LogLevel.Trace,
                    null);

        // Two entries are skipped while paused, evicting "first" from the live buffer.
        cache.MarkStale();
        cache.MarkStale();

        var third = CreateEntry(LogLevel.Information, "third");
        var updatedEntries = new List<LogEntry>
                             {
                                 second,
                                 third
                             };

        cache.Append(third, updatedEntries);

        Assert.HasCount(2, cache.Filtered, "The resync should reflect the current buffer, not an incremental guess!");
        Assert.IsFalse(cache.Filtered.Contains(first), "An entry evicted while stale should not survive the resync!");
        Assert.AreEqual("third", cache.Filtered[0].Message, "The newest entry should come first!");
        Assert.AreEqual("second", cache.Filtered[1].Message, "The entry that survived eviction should still be present!");
    }

    /// <summary>
    /// When several entries were added to the store before their handlers ran, the live entries hold more than just the
    /// one entry the append call names, so the cache falls back to a full rebuild instead of an incorrect incremental one
    /// </summary>
    [TestMethod]
    public void LogFilterCacheAppendWithMultipleUnseenAdditionsFallsBackToFullRebuild()
    {
        var cache = new LogFilterCache();
        var first = CreateEntry(LogLevel.Information, "first");

        cache.Reset(new List<LogEntry>
                    {
                        first
                    },
                    LogLevel.Trace,
                    null);

        var addedA = CreateEntry(LogLevel.Information, "a");
        var addedB = CreateEntry(LogLevel.Information, "b");

        // The store already holds both additions by the time the handler for "a" runs.
        var updatedEntries = new List<LogEntry>
                             {
                                 first,
                                 addedA,
                                 addedB
                             };

        cache.Append(addedA, updatedEntries);

        Assert.HasCount(3, cache.Filtered, "The full rebuild should include every live entry exactly once!");
        Assert.AreEqual("b", cache.Filtered[0].Message, "The newest entry should come first!");
    }

    /// <summary>
    /// A second handler for an entry a prior burst-triggered rebuild already included is a no-op, so it is not inserted twice
    /// </summary>
    [TestMethod]
    public void LogFilterCacheAppendWithAlreadySyncedEntryIsNoOp()
    {
        var cache = new LogFilterCache();
        var first = CreateEntry(LogLevel.Information, "first");
        var addedA = CreateEntry(LogLevel.Information, "a");
        var addedB = CreateEntry(LogLevel.Information, "b");

        cache.Reset(new List<LogEntry>
                    {
                        first
                    },
                    LogLevel.Trace,
                    null);

        var updatedEntries = new List<LogEntry>
                             {
                                 first,
                                 addedA,
                                 addedB
                             };

        cache.Append(addedA, updatedEntries);
        cache.Append(addedB, updatedEntries);

        Assert.HasCount(3, cache.Filtered, "The entry already included by the earlier rebuild should not be inserted again!");
    }

    /// <summary>
    /// Create a log entry with the given level and message
    /// </summary>
    /// <param name="level">Log level of the entry</param>
    /// <param name="message">Message of the entry</param>
    /// <returns>The log entry</returns>
    private static LogEntry CreateEntry(LogLevel level, string message)
    {
        return new LogEntry
               {
                   Timestamp = DateTimeOffset.UtcNow,
                   Level = level,
                   Category = "Test",
                   Message = message
               };
    }

    #endregion // Methods
}