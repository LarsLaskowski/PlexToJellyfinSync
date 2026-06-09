namespace PlexToJellyfinSync.Core.Models;

/// <summary>
/// Watch state of a media item as it should be written to an NFO file
/// </summary>
public sealed class WatchInfo
{
    #region Properties

    /// <summary>
    /// Whether the item is considered watched
    /// </summary>
    public bool Watched { get; set; }

    /// <summary>
    /// Number of times the item has been played
    /// </summary>
    public int PlayCount { get; set; }

    /// <summary>
    /// Timestamp of the last playback, or <c>null</c> if never played
    /// </summary>
    public DateTimeOffset? LastPlayed { get; set; }

    #endregion // Properties
}