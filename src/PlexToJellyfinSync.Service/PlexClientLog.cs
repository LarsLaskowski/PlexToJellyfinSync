using Microsoft.Extensions.Logging;

namespace PlexToJellyfinSync.Service;

/// <summary>
/// Source-generated log messages of <see cref="PlexClient"/>
/// </summary>
internal static partial class PlexClientLog
{
    #region Methods

    /// <summary>
    /// Log that a history entry with an invalid rating key was skipped
    /// </summary>
    /// <param name="logger">Logging interface</param>
    [LoggerMessage(EventId = 1, EventName = "InvalidHistoryRatingKey", Level = LogLevel.Warning, Message = "Skipping a Plex history entry with an invalid rating key")]
    public static partial void InvalidHistoryRatingKey(ILogger logger);

    /// <summary>
    /// Log that an item reported an invalid show rating key
    /// </summary>
    /// <param name="logger">Logging interface</param>
    /// <param name="ratingKey">Rating key of the item</param>
    [LoggerMessage(EventId = 2, EventName = "InvalidShowRatingKey", Level = LogLevel.Warning, Message = "Item {RatingKey} reported an invalid show rating key, dropping it")]
    public static partial void InvalidShowRatingKey(ILogger logger, string? ratingKey);

    /// <summary>
    /// Log that a library with an invalid key was skipped
    /// </summary>
    /// <param name="logger">Logging interface</param>
    [LoggerMessage(EventId = 3, EventName = "InvalidLibraryKey", Level = LogLevel.Warning, Message = "Skipping a Plex library with an invalid key")]
    public static partial void InvalidLibraryKey(ILogger logger);

    /// <summary>
    /// Log that an item with a missing or invalid rating key was skipped
    /// </summary>
    /// <param name="logger">Logging interface</param>
    [LoggerMessage(EventId = 4, EventName = "InvalidItemRatingKey", Level = LogLevel.Warning, Message = "Skipping a Plex item with a missing or invalid rating key")]
    public static partial void InvalidItemRatingKey(ILogger logger);

    /// <summary>
    /// Log that an item reported an invalid file path
    /// </summary>
    /// <param name="logger">Logging interface</param>
    /// <param name="ratingKey">Rating key of the item</param>
    [LoggerMessage(EventId = 5, EventName = "InvalidFilePath", Level = LogLevel.Warning, Message = "Item {RatingKey} reported an invalid file path, treating it as missing")]
    public static partial void InvalidFilePath(ILogger logger, string? ratingKey);

    /// <summary>
    /// Log that a library page did not advance
    /// </summary>
    /// <param name="logger">Logging interface</param>
    /// <param name="baseUrl">Relative URL of the paged request</param>
    [LoggerMessage(EventId = 6, EventName = "NonAdvancingPage", Level = LogLevel.Warning, Message = "Plex server returned a non-advancing page for {BaseUrl}, stopping pagination")]
    public static partial void NonAdvancingPage(ILogger logger, string baseUrl);

    /// <summary>
    /// Log that the account list holds no account with a positive id
    /// </summary>
    /// <param name="logger">Logging interface</param>
    [LoggerMessage(EventId = 7, EventName = "NoPositiveOwnerId", Level = LogLevel.Warning, Message = "Could not auto-detect the Plex owner account id: the account list contains no account with a positive id, defaulting to 1 (set Plex:OwnerAccountId to choose the owner)")]
    public static partial void NoPositiveOwnerId(ILogger logger);

    /// <summary>
    /// Log that the owner account id could not be detected
    /// </summary>
    /// <param name="logger">Logging interface</param>
    /// <param name="exception">Request or parse failure</param>
    [LoggerMessage(EventId = 8, EventName = "OwnerDetectionFailed", Level = LogLevel.Warning, Message = "Could not auto-detect the Plex owner account id, defaulting to 1")]
    public static partial void OwnerDetectionFailed(ILogger logger, Exception exception);

    /// <summary>
    /// Log that a history page did not advance
    /// </summary>
    /// <param name="logger">Logging interface</param>
    [LoggerMessage(EventId = 9, EventName = "NonAdvancingHistoryPage", Level = LogLevel.Warning, Message = "Plex server returned a non-advancing history page, stopping pagination")]
    public static partial void NonAdvancingHistoryPage(ILogger logger);

    #endregion // Methods
}