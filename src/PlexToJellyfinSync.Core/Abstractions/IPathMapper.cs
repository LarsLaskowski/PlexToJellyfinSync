namespace PlexToJellyfinSync.Core.Abstractions;

/// <summary>
/// Translates Plex file paths into local (container) file paths
/// </summary>
public interface IPathMapper
{
    #region Methods

    /// <summary>
    /// Map a Plex path to the corresponding local path
    /// </summary>
    /// <param name="plexPath">Path as reported by Plex</param>
    /// <returns>The mapped local path, or <c>null</c> if no mapping applies</returns>
    string? MapToLocal(string plexPath);

    #endregion // Methods
}