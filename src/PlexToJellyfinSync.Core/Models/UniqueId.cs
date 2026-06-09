namespace PlexToJellyfinSync.Core.Models;

/// <summary>
/// External unique identifier of a media item (for example imdb, tmdb, tvdb)
/// </summary>
public sealed class UniqueId
{
    #region Properties

    /// <summary>
    /// Identifier type, for example <c>imdb</c>, <c>tmdb</c> or <c>tvdb</c>
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Identifier value
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Whether this identifier is the default one
    /// </summary>
    public bool IsDefault { get; set; }

    #endregion // Properties
}