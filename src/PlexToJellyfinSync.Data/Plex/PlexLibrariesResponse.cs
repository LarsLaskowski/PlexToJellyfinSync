using System.Text.Json.Serialization;

namespace PlexToJellyfinSync.Data.Plex;

/// <summary>
/// Root response for the library sections endpoint
/// </summary>
public sealed class PlexLibrariesResponse
{
    #region Properties

    /// <summary>
    /// Media container
    /// </summary>
    [JsonPropertyName("MediaContainer")]
    public PlexLibrariesContainer? MediaContainer { get; set; }

    #endregion // Properties
}