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
        var logger = new InMemoryLogger(CreateStore(), "Test", CreateRedactor());

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
        var logger = new InMemoryLogger(store, "PlexToJellyfinSync.Service.SyncOrchestrator", CreateRedactor());

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Processed the {Kind} library", "movie");
        }

        var entries = store.GetEntries();

        Assert.HasCount(1, entries, "The message should have been captured!");
        Assert.AreEqual("PlexToJellyfinSync.Service.SyncOrchestrator", entries[0].Category, "The category should be captured!");
        Assert.AreEqual("Processed the movie library", entries[0].Message, "The formatted message should be captured!");
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
        var logger = new InMemoryLogger(store, "Test", CreateRedactor());

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
        var logger = new InMemoryLogger(store, "Test", CreateRedactor());

        if (logger.IsEnabled(LogLevel.Error))
        {
            logger.LogError(new InvalidOperationException("plex is down"), "Synchronization run failed");
        }

        var entries = store.GetEntries();

        Assert.HasCount(1, entries, "The message should have been captured!");
        Assert.AreEqual(LogLevel.Error, entries[0].Level, "The log level should be captured!");
        Assert.IsNotNull(entries[0].Exception, "The exception text should be captured!");
        Assert.IsTrue(entries[0].Exception!.Contains("plex is down", StringComparison.Ordinal), "The exception text should contain the message!");
    }

    /// <summary>
    /// The configured Plex token is masked out of a captured message
    /// </summary>
    [TestMethod]
    public void InMemoryLoggerMessageWithTokenIsRedacted()
    {
        var store = CreateStore();
        var logger = new InMemoryLogger(store, "Test", CreateRedactor("s3cr3t-token"));

        logger.LogInformation("Connecting with token {Token}", "s3cr3t-token");

        var entries = store.GetEntries();

        Assert.HasCount(1, entries, "The message should have been captured!");
        Assert.IsFalse(entries[0].Message.Contains("s3cr3t-token", StringComparison.Ordinal), "The token should not appear in the stored message!");
        Assert.IsTrue(entries[0].Message.Contains(SecretLogRedactor.Placeholder, StringComparison.Ordinal), "The message should carry the redaction placeholder!");
    }

    /// <summary>
    /// The configured Plex token is masked out of a captured exception
    /// </summary>
    [TestMethod]
    public void InMemoryLoggerExceptionWithTokenIsRedacted()
    {
        var store = CreateStore();
        var logger = new InMemoryLogger(store, "Test", CreateRedactor("s3cr3t-token"));

        if (logger.IsEnabled(LogLevel.Error))
        {
            logger.LogError(new InvalidOperationException("failed for token s3cr3t-token"), "Request failed");
        }

        var entries = store.GetEntries();

        Assert.HasCount(1, entries, "The message should have been captured!");
        Assert.IsFalse(entries[0].Exception!.Contains("s3cr3t-token", StringComparison.Ordinal), "The token should not appear in the stored exception text!");
        Assert.IsTrue(entries[0].Exception!.Contains(SecretLogRedactor.Placeholder, StringComparison.Ordinal), "The exception text should carry the redaction placeholder!");
    }

    /// <summary>
    /// Scopes are not supported and yield no disposable
    /// </summary>
    [TestMethod]
    public void InMemoryLoggerBeginScopeReturnsNull()
    {
        var logger = new InMemoryLogger(CreateStore(), "Test", CreateRedactor());

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

    /// <summary>
    /// Create a redactor configured with an optional Plex token
    /// </summary>
    /// <param name="plexToken">Plex token to redact, or <c>null</c> for none configured</param>
    /// <returns>The redactor</returns>
    private static SecretLogRedactor CreateRedactor(string? plexToken = null)
    {
        return new SecretLogRedactor(Options.Create(new PlexOptions
                                                    {
                                                        Token = plexToken ?? string.Empty
                                                    }),
                                     Options.Create(new DashboardOptions()));
    }

    #endregion // Methods
}