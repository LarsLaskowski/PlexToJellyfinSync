using PlexToJellyfinSync.Core.Models;

namespace PlexToJellyfinSync.Core.Abstractions;

/// <summary>
/// Writes the watch state of a single media item into its NFO file and counts the outcome
/// </summary>
public interface IMediaItemWriter
{
    #region Methods

    /// <summary>
    /// Write a movie or episode: an item without a file path or without a path mapping is skipped, otherwise it is
    /// written at the mapped local path and the outcome is counted
    /// </summary>
    /// <param name="item">Media item including its watch state</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task that completes when the item has been handled</returns>
    Task WriteItemAsync(MediaItem item, CancellationToken cancellationToken);

    /// <summary>
    /// Write a season or series item to an already-local directory and count the outcome. The directory must come
    /// from an <see cref="IPathMapper.MapToLocal"/> result (it is not mapped again); the mapped-root check of the
    /// NFO writer is the backstop against a directory outside the mapped roots
    /// </summary>
    /// <param name="item">Season or series item including its watch state</param>
    /// <param name="localDirectory">Local directory that receives the NFO file, derived from a mapped local path</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task that completes when the item has been written</returns>
    Task WriteAggregateAsync(MediaItem item, string localDirectory, CancellationToken cancellationToken);

    #endregion // Methods
}