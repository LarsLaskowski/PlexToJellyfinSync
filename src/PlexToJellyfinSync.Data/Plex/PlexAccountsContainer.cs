using System.Text.Json.Serialization;

namespace PlexToJellyfinSync.Data.Plex;

/// <summary>
/// Container for the accounts response
/// </summary>
public sealed class PlexAccountsContainer
{
    #region Properties

    /// <summary>
    /// Accounts
    /// </summary>
    [JsonPropertyName("Account")]
    public List<PlexAccount>? Account { get; set; }

    #endregion // Properties
}