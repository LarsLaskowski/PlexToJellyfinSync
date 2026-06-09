using System.Text.Json.Serialization;

namespace PlexToJellyfinSync.Data.Plex;

/// <summary>
/// Container for metadata and history responses
/// </summary>
public sealed class PlexMetadataContainer
{
    #region Properties

    /// <summary>
    /// Metadata entries
    /// </summary>
    [JsonPropertyName("Metadata")]
    public List<PlexMetadata>? Metadata { get; set; }

    #endregion // Properties
}