using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

using PlexToJellyfinSync.Core.Abstractions;

namespace PlexToJellyfinSync.Service.Logging;

/// <summary>
/// Logger provider that writes log entries into the in-memory log store
/// </summary>
[ProviderAlias("InMemory")]
public sealed class InMemoryLogProvider : ILoggerProvider
{
    #region Fields

    private readonly ILogStore _store;
    private readonly ConcurrentDictionary<string, InMemoryLogger> _loggers = new(StringComparer.Ordinal);

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="store">Log store</param>
    public InMemoryLogProvider(ILogStore store)
    {
        _store = store;
    }

    #endregion // Constructors

    #region ILoggerProvider

    /// <inheritdoc/>
    public ILogger CreateLogger(string categoryName)
    {
        return _loggers.GetOrAdd(categoryName, name => new InMemoryLogger(_store, name));
    }

    #endregion // ILoggerProvider

    #region IDisposable

    /// <inheritdoc/>
    public void Dispose()
    {
        _loggers.Clear();
    }

    #endregion // IDisposable
}