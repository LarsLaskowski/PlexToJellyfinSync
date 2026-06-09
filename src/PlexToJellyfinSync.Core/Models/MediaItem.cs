using PlexToJellyfinSync.Core.Enums;

namespace PlexToJellyfinSync.Core.Models;

/// <summary>
/// Resolved media item with the metadata required to create or update an NFO file
/// </summary>
public sealed class MediaItem
{
    #region Properties

    /// <summary>
    /// Plex rating key (unique id within the Plex server)
    /// </summary>
    public string RatingKey { get; set; } = string.Empty;

    /// <summary>
    /// Kind of media item
    /// </summary>
    public MediaKind Kind { get; set; }

    /// <summary>
    /// Display title
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Original title, if available
    /// </summary>
    public string? OriginalTitle { get; set; }

    /// <summary>
    /// Sort title, if available
    /// </summary>
    public string? SortTitle { get; set; }

    /// <summary>
    /// Release year, if available
    /// </summary>
    public int? Year { get; set; }

    /// <summary>
    /// Plot / summary, if available
    /// </summary>
    public string? Plot { get; set; }

    /// <summary>
    /// Runtime in minutes, if available
    /// </summary>
    public int? RuntimeMinutes { get; set; }

    /// <summary>
    /// Genres
    /// </summary>
    public IReadOnlyList<string> Genres { get; set; } = [];

    /// <summary>
    /// Studio, if available
    /// </summary>
    public string? Studio { get; set; }

    /// <summary>
    /// Premiere / air date, if available
    /// </summary>
    public DateTimeOffset? Premiered { get; set; }

    /// <summary>
    /// Date the item was added to the library, if available
    /// </summary>
    public DateTimeOffset? DateAdded { get; set; }

    /// <summary>
    /// External unique identifiers
    /// </summary>
    public IReadOnlyList<UniqueId> UniqueIds { get; set; } = [];

    /// <summary>
    /// Plex file path of the underlying media file, if available
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// Season number for episodes and seasons, if applicable
    /// </summary>
    public int? SeasonNumber { get; set; }

    /// <summary>
    /// Episode number for episodes, if applicable
    /// </summary>
    public int? EpisodeNumber { get; set; }

    /// <summary>
    /// Title of the owning series for episodes and seasons, if applicable
    /// </summary>
    public string? ShowTitle { get; set; }

    /// <summary>
    /// Rating key of the owning series for episodes and seasons, if applicable
    /// </summary>
    public string? ShowRatingKey { get; set; }

    /// <summary>
    /// Watch state of the item
    /// </summary>
    public WatchInfo Watch { get; set; } = new();

    #endregion // Properties
}