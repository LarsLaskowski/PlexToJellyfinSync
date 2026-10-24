using Microsoft.Extensions.Logging;

using PlexToJellyfinSync.Service;

namespace PlexToJellyfinSync.Tests;

/// <summary>
/// Logger for <see cref="PlexClient"/> that records the message and the exception of all warning entries
/// </summary>
internal sealed class RecordingPlexClientLogger : ILogger<PlexClient>
{
    #region Properties

    /// <summary>
    /// Formatted messages and exceptions of the recorded warnings
    /// </summary>
    public List<(string Message, Exception? Exception)> Warnings { get; } = [];

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
        if (logLevel == LogLevel.Warning)
        {
            Warnings.Add((formatter(state, exception), exception));
        }
    }

    #endregion // ILogger
}