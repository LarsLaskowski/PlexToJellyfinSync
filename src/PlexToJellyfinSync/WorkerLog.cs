using Microsoft.Extensions.Logging;

namespace PlexToJellyfinSync;

/// <summary>
/// Source-generated log messages of <see cref="Worker"/>
/// </summary>
internal static partial class WorkerLog
{
    #region Methods

    /// <summary>
    /// Log that the full reconcile failed
    /// </summary>
    /// <param name="logger">Logging interface</param>
    /// <param name="exception">Exception that caused the failure</param>
    [LoggerMessage(EventId = 1, EventName = "ReconcileFailed", Level = LogLevel.Error, Message = "Full reconcile failed")]
    public static partial void ReconcileFailed(ILogger logger, Exception exception);

    /// <summary>
    /// Log that the incremental history sync failed
    /// </summary>
    /// <param name="logger">Logging interface</param>
    /// <param name="exception">Exception that caused the failure</param>
    [LoggerMessage(EventId = 2, EventName = "HistorySyncFailed", Level = LogLevel.Error, Message = "Incremental history sync failed")]
    public static partial void HistorySyncFailed(ILogger logger, Exception exception);

    /// <summary>
    /// Log that the worker started
    /// </summary>
    /// <param name="logger">Logging interface</param>
    /// <param name="poll">Poll interval</param>
    /// <param name="reconcile">Full reconcile interval</param>
    [LoggerMessage(EventId = 3, EventName = "Started", Level = LogLevel.Information, Message = "Worker started; poll every {Poll}, reconcile every {Reconcile}")]
    public static partial void Started(ILogger logger, TimeSpan poll, TimeSpan reconcile);

    /// <summary>
    /// Log that the worker is stopping
    /// </summary>
    /// <param name="logger">Logging interface</param>
    [LoggerMessage(EventId = 4, EventName = "Stopping", Level = LogLevel.Information, Message = "Worker stopping")]
    public static partial void Stopping(ILogger logger);

    #endregion // Methods
}