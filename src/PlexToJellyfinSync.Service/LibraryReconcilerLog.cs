using Microsoft.Extensions.Logging;

namespace PlexToJellyfinSync.Service;

/// <summary>
/// Source-generated log messages of <see cref="LibraryReconciler"/>
/// </summary>
internal static partial class LibraryReconcilerLog
{
    #region Methods

    /// <summary>
    /// Log that processing an item failed
    /// </summary>
    /// <param name="logger">Logging interface</param>
    /// <param name="exception">Exception that caused the failure</param>
    /// <param name="ratingKey">Plex rating key of the item</param>
    [LoggerMessage(EventId = 1, EventName = "ItemFailed", Level = LogLevel.Error, Message = "Processing item {RatingKey} failed, skipping it")]
    public static partial void ItemFailed(ILogger logger, Exception exception, string ratingKey);

    #endregion // Methods
}