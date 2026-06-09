using Microsoft.Extensions.Logging;

using PlexToJellyfinSync.Core.Abstractions;
using PlexToJellyfinSync.Core.Models;

namespace PlexToJellyfinSync.Service.Logging;

/// <summary>
/// Logger that forwards entries to the in-memory log store
/// </summary>
public sealed class InMemoryLogger : ILogger
{
    #region Fields

    private readonly ILogStore _store;
    private readonly string _category;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="store">Log store</param>
    /// <param name="category">Logger category</param>
    public InMemoryLogger(ILogStore store, string category)
    {
        _store = store;
        _category = category;
    }

    #endregion // Constructors

    #region ILogger

    /// <inheritdoc/>
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        return null;
    }

    /// <inheritdoc/>
    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel >= LogLevel.Information;
    }

    /// <inheritdoc/>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (IsEnabled(logLevel) == false)
        {
            return;
        }

        var entry = new LogEntry
                    {
                        Timestamp = DateTimeOffset.Now,
                        Level = logLevel,
                        Category = _category,
                        Message = formatter(state, exception),
                        Exception = exception?.ToString()
                    };

        _store.Add(entry);
    }

    #endregion // ILogger
}