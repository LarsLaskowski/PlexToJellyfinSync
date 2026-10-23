using Microsoft.Extensions.Logging;

using PlexToJellyfinSync.Security;

namespace PlexToJellyfinSync.Tests;

/// <summary>
/// Logger fake for <see cref="ForwardedHeadersWarning"/> that records level and message of every entry
/// </summary>
internal sealed class RecordingForwardedHeadersWarningLogger : ILogger<ForwardedHeadersWarning>
{
    #region Fields

    /// <summary>
    /// Synchronizes access to the recorded entries
    /// </summary>
    private readonly object _lock = new();

    /// <summary>
    /// Recorded entries
    /// </summary>
    private readonly List<(LogLevel Level, string Message)> _entries = [];

    #endregion // Fields

    #region Methods

    /// <summary>
    /// Get a copy of the recorded entries
    /// </summary>
    /// <returns>Entries in logging order</returns>
    public IReadOnlyList<(LogLevel Level, string Message)> GetEntries()
    {
        lock (_lock)
        {
            return [.. _entries];
        }
    }

    #endregion // Methods

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
        return true;
    }

    /// <inheritdoc/>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        lock (_lock)
        {
            _entries.Add((logLevel, formatter(state, exception)));
        }
    }

    #endregion // ILogger
}