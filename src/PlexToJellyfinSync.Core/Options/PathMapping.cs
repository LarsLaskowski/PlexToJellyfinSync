namespace PlexToJellyfinSync.Core.Options;

/// <summary>
/// Single path mapping from a Plex path prefix to a local (container) path prefix
/// </summary>
public sealed class PathMapping
{
    #region Properties

    /// <summary>
    /// Path prefix as reported by Plex (for example <c>/data/Movies</c>)
    /// </summary>
    public string Plex { get; set; } = string.Empty;

    /// <summary>
    /// Local path prefix as seen by this application (for example <c>/media/Movies</c>)
    /// </summary>
    public string Local { get; set; } = string.Empty;

    #endregion // Properties
}