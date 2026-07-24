using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using PlexToJellyfinSync.Core.Options;
using PlexToJellyfinSync.Service.Logging;

namespace PlexToJellyfinSync.Tests;

/// <summary>
/// Tests for <see cref="InMemoryLogger"/>
/// </summary>
[TestClass]
public sealed class InMemoryLoggerTests
{
    #region Methods

    /// <summary>
    /// Levels below information are not enabled
    /// </summary>
    [TestMethod]
    public void InMemoryLoggerEnablesInformationAndAbove()
    {
        var logger = new InMemoryLogger(CreateStore(), "Test");

        Assert.IsFalse(logger.IsEnabled(LogLevel.Trace), "Trace should not be enabled!");
        Assert.IsFalse(logger.IsEnabled(LogLevel.Debug), "Debug should not be enabled!");
        Assert.IsTrue(logger.IsEnabled(LogLevel.Information), "Information should be enabled!");
        Assert.IsTrue(logger.IsEnabled(LogLevel.Error), "Error should be enabled!");
    }

    /// <summary>
    /// An informational message is captured with its category and text
    /// </summary>
    [TestMethod]
    public void InMemoryLoggerInformationIsCaptured()
    {
        var store = CreateStore();
        var logger = new InMemoryLogger(store, "PlexToJellyfinSync.Service.SyncOrchestrator");

        logger.LogInformation("Processed {Count} items", 3);

        var entries = store.GetEntries();

        Assert.HasCount(1, entries, "The message should have been captured!");
        Assert.AreEqual("PlexToJellyfinSync.Service.SyncOrchestrator", entries[0].Category, "The category should be captured!");
        Assert.AreEqual("Processed 3 items", entries[0].Message, "The formatted message should be captured!");
        Assert.AreEqual(LogLevel.Information, entries[0].Level, "The log level should be captured!");
        Assert.IsNull(entries[0].Exception, "A message without exception should not carry exception text!");
    }

    /// <summary>
    /// A message below information is discarded
    /// </summary>
    [TestMethod]
    public void InMemoryLoggerDebugIsDiscarded()
    {
        var store = CreateStore();
        var logger = new InMemoryLogger(store, "Test");

        logger.LogDebug("noise");
        logger.LogTrace("more noise");

        Assert.IsEmpty(store.GetEntries(), "Messages below information should be discarded!");
    }

    /// <summary>
    /// An exception is captured alongside the message
    /// </summary>
    [TestMethod]
    public void InMemoryLoggerExceptionIsCaptured()
    {
        var store = CreateStore();
        var logger = new InMemoryLogger(store, "Test");

        logger.LogError(new InvalidOperationException("plex is down"), "Synchronization run failed");

        var entries = store.GetEntries();

        Assert.HasCount(1, entries, "The message should have been captured!");
        Assert.AreEqual(LogLevel.Error, entries[0].Level, "The log level should be captured!");
        Assert.IsNotNull(entries[0].Exception, "The exception text should be captured!");
        Assert.IsTrue(entries[0].Exception!.Contains("plex is down", StringComparison.Ordinal), "The exception text should contain the message!");
    }

    /// <summary>
    /// Scopes are not supported and yield no disposable
    /// </summary>
    [TestMethod]
    public void InMemoryLoggerBeginScopeReturnsNull()
    {
        var logger = new InMemoryLogger(CreateStore(), "Test");

        Assert.IsNull(logger.BeginScope("scope"), "Scopes should not be supported!");
    }

    /// <summary>
    /// Create a log store backing the logger under test
    /// </summary>
    /// <returns>The log store</returns>
    private static InMemoryLogStore CreateStore()
    {
        return new InMemoryLogStore(Options.Create(new DashboardOptions()));
    }

    #endregion // Methods
}