using Microsoft.Extensions.Logging;

using PlexToJellyfinSync.Service.State;

namespace PlexToJellyfinSync.Tests;

/// <summary>
/// Logger for <see cref="StateStore"/> that records level, message and exception of every entry
/// </summary>
internal sealed class RecordingStateStoreLogger : ILogger<StateStore>
{
    #region Properties

    /// <summary>
    /// Recorded entries in the order they were logged
    /// </summary>
    public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

    #endregion // Properties

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
        Entries.Add((logLevel, formatter(state, exception), exception));
    }

    #endregion // ILogger
}