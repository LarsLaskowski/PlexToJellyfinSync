using System.Text.Json.Serialization;

namespace PlexToJellyfinSync.Data.Plex;

/// <summary>
/// Root response for metadata and history endpoints
/// </summary>
public sealed class PlexMetadataResponse
{
    #region Properties

    /// <summary>
    /// Media container
    /// </summary>
    [JsonPropertyName("MediaContainer")]
    public PlexMetadataContainer? MediaContainer { get; set; }

    #endregion // Properties
}