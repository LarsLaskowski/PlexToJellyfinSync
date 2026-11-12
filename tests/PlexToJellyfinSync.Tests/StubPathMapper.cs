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
    public HashSet<string> Unmapped { get; } = [with(StringComparer.Ordinal)];

    #endregion // Properties

    #region IPathMapper

    /// <inheritdoc/>
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