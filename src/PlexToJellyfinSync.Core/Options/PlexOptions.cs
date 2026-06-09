namespace PlexToJellyfinSync.Core.Options;

/// <summary>
/// Configuration for connecting to the Plex media server
/// </summary>
public sealed class PlexOptions
{
    #region Constants

    /// <summary>
    /// Configuration section name
    /// </summary>
    public const string SectionName = "Plex";

    #endregion // Constants

    #region Properties

    /// <summary>
    /// Base URL of the Plex server (for example <c>http://plex:32400</c>)
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Plex authentication token sent as <c>X-Plex-Token</c>
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Optional explicit owner account id; when <c>null</c> it is auto-detected
    /// </summary>
    public int? OwnerAccountId { get; set; }

    /// <summary>
    /// Optional list of library section keys to restrict synchronization to; empty means all libraries
    /// </summary>
    public string[] Libraries { get; set; } = [];

    #endregion // Properties
}