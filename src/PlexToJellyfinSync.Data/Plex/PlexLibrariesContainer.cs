using System.Text.Json.Serialization;

namespace PlexToJellyfinSync.Data.Plex;

/// <summary>
/// Container for the library sections response
/// </summary>
public sealed class PlexLibrariesContainer
{
    #region Properties

    /// <summary>
    /// Library section directories
    /// </summary>
    [JsonPropertyName("Directory")]
    public List<PlexDirectory>? Directory { get; set; }

    #endregion // Properties
}