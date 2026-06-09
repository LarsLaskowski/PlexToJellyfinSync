using System.Text.Json.Serialization;

namespace PlexToJellyfinSync.Data.Plex;

/// <summary>
/// Plex media part referencing the underlying file
/// </summary>
public sealed class PlexPart
{
    #region Properties

    /// <summary>
    /// Absolute file path as seen by the Plex server
    /// </summary>
    [JsonPropertyName("file")]
    public string? File { get; set; }

    #endregion // Properties
}