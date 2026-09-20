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
    private readonly ILogRedactor _redactor;
    private readonly ConcurrentDictionary<string, InMemoryLogger> _loggers = new(StringComparer.Ordinal);

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="store">Log store</param>
    /// <param name="redactor">Redactor masking known secrets out of captured text</param>
    public InMemoryLogProvider(ILogStore store, ILogRedactor redactor)
    {
        _store = store;
        _redactor = redactor;
    }

    #endregion // Constructors

    #region ILoggerProvider

    /// <inheritdoc/>
    public ILogger CreateLogger(string categoryName)
    {
        return _loggers.GetOrAdd(categoryName, name => new InMemoryLogger(_store, name, _redactor));
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