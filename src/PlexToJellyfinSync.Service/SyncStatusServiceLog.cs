using Microsoft.Extensions.Logging;

namespace PlexToJellyfinSync.Service;

/// <summary>
/// Source-generated log messages of <see cref="SyncStatusService"/>
/// </summary>
internal static partial class SyncStatusServiceLog
{
    #region Methods

    /// <summary>
    /// Log that a status change subscriber threw an exception
    /// </summary>
    /// <param name="logger">Logging interface</param>
    /// <param name="exception">Exception thrown by the subscriber</param>
    [LoggerMessage(EventId = 1, EventName = "SubscriberFailed", Level = LogLevel.Warning, Message = "A status change subscriber threw an exception and was skipped")]
    public static partial void SubscriberFailed(ILogger logger, Exception exception);

    #endregion // Methods
}