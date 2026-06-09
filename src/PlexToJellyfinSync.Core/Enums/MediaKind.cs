namespace PlexToJellyfinSync.Core.Enums;

/// <summary>
/// Kind of media item handled by the synchronization
/// </summary>
public enum MediaKind
{
    /// <summary>
    /// Unknown / not set
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// A single movie
    /// </summary>
    Movie,

    /// <summary>
    /// A single episode of a series
    /// </summary>
    Episode,

    /// <summary>
    /// A season of a series
    /// </summary>
    Season,

    /// <summary>
    /// A series / show
    /// </summary>
    Series
}