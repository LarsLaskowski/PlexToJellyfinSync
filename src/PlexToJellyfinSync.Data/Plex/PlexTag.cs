using System.Text.Json.Serialization;

namespace PlexToJellyfinSync.Data.Plex;

/// <summary>
/// Plex tag element (used for genres and similar)
/// </summary>
public sealed class PlexTag
{
    #region Properties

    /// <summary>
    /// Tag value
    /// </summary>
    [JsonPropertyName("tag")]
    public string? Tag { get; set; }

    #endregion // Properties
}