using Microsoft.Extensions.Logging;

namespace PlexToJellyfinSync.Service;

/// <summary>
/// Source-generated log messages of <see cref="MediaItemWriter"/>
/// </summary>
internal static partial class MediaItemWriterLog
{
    #region Methods

    /// <summary>
    /// Log that an item has no file path
    /// </summary>
    /// <param name="logger">Logging interface</param>
    /// <param name="ratingKey">Plex rating key of the item</param>
    /// <param name="title">Title of the item</param>
    [LoggerMessage(EventId = 1, EventName = "ItemWithoutFilePath", Level = LogLevel.Warning, Message = "Item {RatingKey} ({Title}) has no file path, skipping")]
    public static partial void ItemWithoutFilePath(ILogger logger, string ratingKey, string title);

    /// <summary>
    /// Log that no path mapping covers the file path
    /// </summary>
    /// <param name="logger">Logging interface</param>
    /// <param name="filePath">Plex file path</param>
    [LoggerMessage(EventId = 2, EventName = "NoPathMapping", Level = LogLevel.Warning, Message = "No path mapping for {FilePath}, skipping")]
    public static partial void NoPathMapping(ILogger logger, string? filePath);

    #endregion // Methods
}