using System.Text.Json.Serialization;

namespace PlexToJellyfinSync.Data.Plex;

/// <summary>
/// Plex account entry
/// </summary>
public sealed class PlexAccount
{
    #region Properties

    /// <summary>
    /// Account id
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    /// Account name
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    #endregion // Properties
}