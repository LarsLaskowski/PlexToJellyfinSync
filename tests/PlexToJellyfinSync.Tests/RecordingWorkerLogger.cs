using Microsoft.Extensions.Logging;

namespace PlexToJellyfinSync.Tests;

/// <summary>
/// Logger fake for <see cref="Worker"/> that records level, message and exception of every entry
/// </summary>
internal sealed class RecordingWorkerLogger : ILogger<Worker>
{
    #region Fields

    /// <summary>
    /// Synchronizes access to the recorded entries
    /// </summary>
    private readonly object _lock = new();

    /// <summary>
    /// Recorded entries
    /// </summary>
    private readonly List<(LogLevel Level, string Message, Exception? Exception)> _entries = [];

    /// <summary>
    /// Completed when an entry at <see cref="LogLevel.Error"/> is logged
    /// </summary>
    private readonly TaskCompletionSource _errorLogged = new(TaskCreationOptions.RunContinuationsAsynchronously);

    #endregion // Fields

    #region Properties

    /// <summary>
    /// Task that completes once an error entry was logged
    /// </summary>
    public Task ErrorLogged => _errorLogged.Task;

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Get a copy of the recorded entries
    /// </summary>
    /// <returns>Entries in logging order</returns>
    public IReadOnlyList<(LogLevel Level, string Message, Exception? Exception)> GetEntries()
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
            _entries.Add((logLevel, formatter(state, exception), exception));
        }

        if (logLevel == LogLevel.Error)
        {
            _errorLogged.TrySetResult();
        }
    }

    #endregion // ILogger
}