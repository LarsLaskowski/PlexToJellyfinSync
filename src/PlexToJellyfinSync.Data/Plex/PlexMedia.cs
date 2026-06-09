using System.Text.Json.Serialization;

namespace PlexToJellyfinSync.Data.Plex;

/// <summary>
/// Plex media element containing one or more parts
/// </summary>
public sealed class PlexMedia
{
    #region Properties

    /// <summary>
    /// Media parts
    /// </summary>
    [JsonPropertyName("Part")]
    public List<PlexPart>? Part { get; set; }

    #endregion // Properties
}