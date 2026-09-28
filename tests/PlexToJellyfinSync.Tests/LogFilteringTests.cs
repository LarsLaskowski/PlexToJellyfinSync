using Microsoft.Extensions.Logging;

using PlexToJellyfinSync.Components.Pages;
using PlexToJellyfinSync.Core.Models;

namespace PlexToJellyfinSync.Tests;

/// <summary>
/// Tests for <see cref="LogFiltering"/>
/// </summary>
[TestClass]
public sealed class LogFilteringTests
{
    #region Methods

    /// <summary>
    /// Entries below the minimum level are excluded
    /// </summary>
    [TestMethod]
    public void LogFilteringApplyBelowMinimumLevelExcludesEntry()
    {
        var entries = new List<LogEntry>
                      {
                          CreateEntry(LogLevel.Debug, "debug message"),
                          CreateEntry(LogLevel.Warning, "warning message")
                      };

        var result = LogFiltering.Apply(entries, LogLevel.Warning, null);

        Assert.HasCount(1, result, "Only the entry meeting the minimum level should be kept!");
        Assert.AreEqual("warning message", result[0].Message, "The surviving entry should be the warning one!");
    }

    /// <summary>
    /// Entries whose message does not contain the filter text are excluded
    /// </summary>
    [TestMethod]
    public void LogFilteringApplyMessageWithoutFilterTextExcludesEntry()
    {
        var entries = new List<LogEntry>
                      {
                          CreateEntry(LogLevel.Information, "sync started"),
                          CreateEntry(LogLevel.Information, "sync failed")
                      };

        var result = LogFiltering.Apply(entries, LogLevel.Trace, "failed");

        Assert.HasCount(1, result, "Only the entry containing the filter text should be kept!");
        Assert.AreEqual("sync failed", result[0].Message, "The surviving entry should be the one matching the filter!");
    }

    /// <summary>
    /// The filter text match is case-insensitive
    /// </summary>
    [TestMethod]
    public void LogFilteringApplyFilterTextIsCaseInsensitive()
    {
        var entries = new List<LogEntry>
                      {
                          CreateEntry(LogLevel.Information, "Sync FAILED")
                      };

        var result = LogFiltering.Apply(entries, LogLevel.Trace, "failed");

        Assert.HasCount(1, result, "The filter text should match regardless of case!");
    }

    /// <summary>
    /// An empty filter keeps every entry that meets the minimum level
    /// </summary>
    [TestMethod]
    public void LogFilteringApplyEmptyFilterKeepsAllMatchingLevel()
    {
        var entries = new List<LogEntry>
                      {
                          CreateEntry(LogLevel.Information, "first"),
                          CreateEntry(LogLevel.Information, "second")
                      };

        var result = LogFiltering.Apply(entries, LogLevel.Trace, string.Empty);

        Assert.HasCount(2, result, "An empty filter should not exclude any entry meeting the minimum level!");
    }

    /// <summary>
    /// The matching entries are returned newest first
    /// </summary>
    [TestMethod]
    public void LogFilteringApplyReturnsNewestFirst()
    {
        var entries = new List<LogEntry>
                      {
                          CreateEntry(LogLevel.Information, "first"),
                          CreateEntry(LogLevel.Information, "second"),
                          CreateEntry(LogLevel.Information, "third")
                      };

        var result = LogFiltering.Apply(entries, LogLevel.Trace, null);

        Assert.AreEqual("third", result[0].Message, "The newest entry should come first!");
        Assert.AreEqual("first", result[2].Message, "The oldest entry should come last!");
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