using System.Text.Json.Serialization;

namespace PlexToJellyfinSync.Data.Plex;

/// <summary>
/// Root response for the accounts endpoint
/// </summary>
public sealed class PlexAccountsResponse
{
    #region Properties

    /// <summary>
    /// Media container
    /// </summary>
    [JsonPropertyName("MediaContainer")]
    public PlexAccountsContainer? MediaContainer { get; set; }

    #endregion // Properties
}