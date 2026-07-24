using PlexToJellyfinSync.Core.Abstractions;

namespace PlexToJellyfinSync.Tests;

/// <summary>
/// Path mapper stub that maps every path onto itself except the explicitly unmapped ones
/// </summary>
internal sealed class StubPathMapper : IPathMapper
{
    #region Properties

    /// <summary>
    /// Plex paths for which no mapping exists
    /// </summary>
    public HashSet<string> Unmapped { get; } = new(StringComparer.Ordinal);

    #endregion // Properties

    #region IPathMapper

    /// <summary>
    /// Map the given Plex path onto itself unless it is marked as unmapped
    /// </summary>
    /// <param name="plexPath">Path as reported by Plex</param>
    /// <returns>The same path, or <c>null</c> if no mapping applies</returns>
    public string? MapToLocal(string plexPath)
    {
        if (string.IsNullOrWhiteSpace(plexPath) || Unmapped.Contains(plexPath))
        {
            return null;
        }

        return plexPath;
    }

    #endregion // IPathMapper
}