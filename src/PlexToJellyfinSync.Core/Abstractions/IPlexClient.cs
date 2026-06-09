using PlexToJellyfinSync.Core.Models;

namespace PlexToJellyfinSync.Core.Abstractions;

/// <summary>
/// Client for reading data from the Plex media server
/// </summary>
public interface IPlexClient
{
    #region Methods

    /// <summary>
    /// Determine the owner account id of the Plex server
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The owner account id</returns>
    Task<int> GetOwnerAccountIdAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Get the available library sections
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of libraries</returns>
    Task<IReadOnlyList<PlexLibrary>> GetLibrariesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Get watch-history entries that occurred after the given timestamp for the given account
    /// </summary>
    /// <param name="since">Lower bound (exclusive) for the viewed-at timestamp</param>
    /// <param name="accountId">Account id to filter for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of history entries ordered ascending by viewed-at</returns>
    Task<IReadOnlyList<PlexHistoryEntry>> GetHistorySinceAsync(DateTimeOffset since, int accountId, CancellationToken cancellationToken);

    /// <summary>
    /// Get the full metadata of a single item
    /// </summary>
    /// <param name="ratingKey">Rating key of the item</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The media item, or <c>null</c> if not found</returns>
    Task<MediaItem?> GetMediaItemAsync(string ratingKey, CancellationToken cancellationToken);

    /// <summary>
    /// Get all episodes of a series
    /// </summary>
    /// <param name="showRatingKey">Rating key of the series</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of episodes</returns>
    Task<IReadOnlyList<MediaItem>> GetEpisodesAsync(string showRatingKey, CancellationToken cancellationToken);

    /// <summary>
    /// Get all items of a library section
    /// </summary>
    /// <param name="libraryKey">Section key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of items</returns>
    Task<IReadOnlyList<MediaItem>> GetLibraryItemsAsync(string libraryKey, CancellationToken cancellationToken);

    #endregion // Methods
}