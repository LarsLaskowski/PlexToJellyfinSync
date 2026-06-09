using Microsoft.Extensions.Options;

using PlexToJellyfinSync.Core.Abstractions;
using PlexToJellyfinSync.Core.Options;

namespace PlexToJellyfinSync.Service;

/// <summary>
/// Translates Plex file paths into local (container) file paths using configured prefix mappings
/// </summary>
public sealed class PathMapper : IPathMapper
{
    #region Fields

    private readonly IReadOnlyList<PathMapping> _mappings;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="mappings">Configured path mappings</param>
    public PathMapper(IOptions<List<PathMapping>> mappings)
    {
        _mappings = mappings.Value ?? new List<PathMapping>();
    }

    #endregion // Constructors

    #region Static methods

    /// <summary>
    /// Normalize a path to forward slashes
    /// </summary>
    /// <param name="path">Path to normalize</param>
    /// <returns>Normalized path</returns>
    private static string Normalize(string path)
    {
        return path.Replace('\\', '/');
    }

    #endregion // Static methods

    #region IPathMapper

    /// <inheritdoc/>
    public string? MapToLocal(string plexPath)
    {
        if (string.IsNullOrWhiteSpace(plexPath))
        {
            return null;
        }

        var input = Normalize(plexPath);
        PathMapping? best = null;
        var bestLength = -1;

        foreach (var mapping in _mappings)
        {
            var plexPrefix = Normalize(mapping.Plex).TrimEnd('/');

            if (plexPrefix.Length == 0)
            {
                continue;
            }

            var matches = input.Equals(plexPrefix, StringComparison.Ordinal)
                          || input.StartsWith(plexPrefix + "/", StringComparison.Ordinal);

            if (matches && plexPrefix.Length > bestLength)
            {
                best = mapping;
                bestLength = plexPrefix.Length;
            }
        }

        if (best is null)
        {
            return null;
        }

        var localPrefix = Normalize(best.Local).TrimEnd('/');
        var remainder = input.Substring(Normalize(best.Plex).TrimEnd('/').Length);

        return localPrefix + remainder;
    }

    #endregion // IPathMapper
}