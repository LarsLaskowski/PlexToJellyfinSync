namespace PlexToJellyfinSync.Core.Models;

/// <summary>
/// A single Plex watch-history entry
/// </summary>
public sealed class PlexHistoryEntry
{
    #region Properties

    /// <summary>
    /// Rating key of the watched item
    /// </summary>
    public string RatingKey { get; set; } = string.Empty;

    /// <summary>
    /// Account id that watched the item
    /// </summary>
    public int AccountId { get; set; }

    /// <summary>
    /// Timestamp at which the item was viewed
    /// </summary>
    public DateTimeOffset ViewedAt { get; set; }

    /// <summary>
    /// Key of the library section the watched item belongs to, or <c>null</c> if Plex reported none
    /// </summary>
    public string? LibrarySectionKey { get; set; }

    #endregion // Properties
}