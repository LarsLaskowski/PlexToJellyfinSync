using System.Text.Json.Serialization;

namespace PlexToJellyfinSync.Data.Plex;

/// <summary>
/// Plex library section directory entry
/// </summary>
public sealed class PlexDirectory
{
    #region Properties

    /// <summary>
    /// Section key
    /// </summary>
    [JsonPropertyName("key")]
    public string? Key { get; set; }

    /// <summary>
    /// Section title
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>
    /// Section type, for example <c>movie</c> or <c>show</c>
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    #endregion // Properties
}