using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using PlexToJellyfinSync.Core.Options;
using PlexToJellyfinSync.Service.Logging;

namespace PlexToJellyfinSync.Tests;

/// <summary>
/// Tests for <see cref="InMemoryLogProvider"/>
/// </summary>
[TestClass]
public sealed class InMemoryLogProviderTests
{
    #region Methods

    /// <summary>
    /// The same category always yields the same logger instance
    /// </summary>
    [TestMethod]
    public void InMemoryLogProviderReusesLoggerPerCategory()
    {
        using var provider = new InMemoryLogProvider(CreateStore());

        var first = provider.CreateLogger("Category");
        var second = provider.CreateLogger("Category");
        var other = provider.CreateLogger("Other");

        Assert.AreSame(first, second, "The same category should reuse the logger!");
        Assert.AreNotSame(first, other, "A different category should get its own logger!");
    }

    /// <summary>
    /// Created loggers write into the shared store
    /// </summary>
    [TestMethod]
    public void InMemoryLogProviderLoggersWriteIntoStore()
    {
        var store = CreateStore();

        using var provider = new InMemoryLogProvider(store);

        provider.CreateLogger("First").LogInformation("one");
        provider.CreateLogger("Second").LogWarning("two");

        var entries = store.GetEntries();

        Assert.HasCount(2, entries, "Both messages should reach the shared store!");
        Assert.AreEqual("First", entries[0].Category, "The category of the first entry should be kept!");
        Assert.AreEqual("Second", entries[1].Category, "The category of the second entry should be kept!");
    }

    /// <summary>
    /// Disposing drops the cached loggers without breaking the provider
    /// </summary>
    [TestMethod]
    public void InMemoryLogProviderDisposeClearsCachedLoggers()
    {
        var provider = new InMemoryLogProvider(CreateStore());

        var before = provider.CreateLogger("Category");

        provider.Dispose();

        var after = provider.CreateLogger("Category");

        Assert.IsNotNull(after, "The provider should still create loggers after disposal!");
        Assert.AreNotSame(before, after, "Disposal should drop the cached loggers!");
    }

    /// <summary>
    /// Create a log store backing the provider under test
    /// </summary>
    /// <returns>The log store</returns>
    private static InMemoryLogStore CreateStore()
    {
        return new InMemoryLogStore(Options.Create(new DashboardOptions()));
    }

    #endregion // Methods
}