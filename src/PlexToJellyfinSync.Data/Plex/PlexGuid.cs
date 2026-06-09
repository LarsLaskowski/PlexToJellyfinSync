using System.Text.Json.Serialization;

namespace PlexToJellyfinSync.Data.Plex;

/// <summary>
/// Plex GUID element holding an external identifier such as <c>imdb://tt1234567</c>
/// </summary>
public sealed class PlexGuid
{
    #region Properties

    /// <summary>
    /// Identifier string in the form <c>scheme://value</c>
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    #endregion // Properties
}