using Microsoft.Extensions.Logging;

namespace PlexToJellyfinSync.Service;

/// <summary>
/// Source-generated log messages of <see cref="SyncOrchestrator"/>
/// </summary>
internal static partial class SyncOrchestratorLog
{
    #region Methods

    /// <summary>
    /// Log that an item is neither a movie nor an episode
    /// </summary>
    /// <param name="logger">Logging interface</param>
    /// <param name="ratingKey">Plex rating key of the item</param>
    [LoggerMessage(EventId = 1, EventName = "ItemKindSkipped", Level = LogLevel.Debug, Message = "Item {RatingKey} is neither a movie nor an episode, skipping")]
    public static partial void ItemKindSkipped(ILogger logger, string ratingKey);

    /// <summary>
    /// Log that a history entry belongs to a library that is not listed
    /// </summary>
    /// <param name="logger">Logging interface</param>
    /// <param name="ratingKey">Plex rating key of the history entry</param>
    /// <param name="librarySectionKey">Key of the library section</param>
    [LoggerMessage(EventId = 2, EventName = "LibrarySkipped", Level = LogLevel.Debug, Message = "History entry {RatingKey} belongs to library {LibrarySectionKey}, which Plex:Libraries does not list, skipping")]
    public static partial void LibrarySkipped(ILogger logger, string ratingKey, string? librarySectionKey);

    /// <summary>
    /// Log that a synchronization run failed
    /// </summary>
    /// <param name="logger">Logging interface</param>
    /// <param name="exception">Exception that caused the failure</param>
    [LoggerMessage(EventId = 3, EventName = "RunFailed", Level = LogLevel.Error, Message = "Synchronization run failed")]
    public static partial void RunFailed(ILogger logger, Exception exception);

    /// <summary>
    /// Log that processing an item failed
    /// </summary>
    /// <param name="logger">Logging interface</param>
    /// <param name="exception">Exception that caused the failure</param>
    /// <param name="ratingKey">Plex rating key of the item</param>
    [LoggerMessage(EventId = 4, EventName = "ItemFailed", Level = LogLevel.Error, Message = "Processing item {RatingKey} failed, skipping it")]
    public static partial void ItemFailed(ILogger logger, Exception exception, string ratingKey);

    #endregion // Methods
}