using Microsoft.Extensions.Logging;

using PlexToJellyfinSync.Service;

namespace PlexToJellyfinSync.Tests;

/// <summary>
/// Logger for <see cref="SyncStatusService"/> that records the exceptions of all warning entries
/// </summary>
internal sealed class RecordingSyncStatusLogger : ILogger<SyncStatusService>
{
    #region Properties

    /// <summary>
    /// Exceptions attached to the recorded warnings
    /// </summary>
    public List<Exception?> Warnings { get; } = [];

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
            Warnings.Add(exception);
        }
    }

    #endregion // ILogger
}