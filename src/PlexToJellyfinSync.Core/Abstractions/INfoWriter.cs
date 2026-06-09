using PlexToJellyfinSync.Core.Enums;
using PlexToJellyfinSync.Core.Models;

namespace PlexToJellyfinSync.Core.Abstractions;

/// <summary>
/// Creates or updates Jellyfin NFO files for media items
/// </summary>
public interface INfoWriter
{
    #region Methods

    /// <summary>
    /// Create or update the NFO file for the given item, writing the watch state
    /// </summary>
    /// <param name="item">Media item including its watch state</param>
    /// <param name="localPath">Local path of the media file (movie/episode) or the containing directory (season/series)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The outcome of the write operation</returns>
    Task<NfoWriteOutcome> WriteAsync(MediaItem item, string localPath, CancellationToken cancellationToken);

    #endregion // Methods
}