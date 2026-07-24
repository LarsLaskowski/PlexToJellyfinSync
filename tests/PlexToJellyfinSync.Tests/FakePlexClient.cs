using PlexToJellyfinSync.Core.Abstractions;
using PlexToJellyfinSync.Core.Models;

namespace PlexToJellyfinSync.Tests;

/// <summary>
/// Plex client stub that serves preconfigured data and records the calls it received
/// </summary>
internal sealed class FakePlexClient : IPlexClient
{
    #region Properties

    /// <summary>
    /// Owner account id returned to the caller
    /// </summary>
    public int OwnerAccountId { get; set; } = 1;

    /// <summary>
    /// Number of times the owner account id has been requested
    /// </summary>
    public int OwnerAccountIdCalls { get; private set; }

    /// <summary>
    /// Libraries served to the caller
    /// </summary>
    public List<PlexLibrary> Libraries { get; } = [];

    /// <summary>
    /// History entries served to the caller
    /// </summary>
    public List<PlexHistoryEntry> History { get; } = [];

    /// <summary>
    /// Media items keyed by rating key
    /// </summary>
    public Dictionary<string, MediaItem> Items { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Episodes keyed by the rating key of their series
    /// </summary>
    public Dictionary<string, List<MediaItem>> Episodes { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Library items keyed by library section key
    /// </summary>
    public Dictionary<string, List<MediaItem>> LibraryItems { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Exception thrown when the history is requested, if any
    /// </summary>
    public Exception? HistoryException { get; set; }

    /// <summary>
    /// Exception thrown when the libraries are requested, if any
    /// </summary>
    public Exception? LibrariesException { get; set; }

    /// <summary>
    /// Lower bound passed to the last history request, if any
    /// </summary>
    public DateTimeOffset? LastHistorySince { get; private set; }

    /// <summary>
    /// Account id passed to the last history request, if any
    /// </summary>
    public int? LastHistoryAccountId { get; private set; }

    /// <summary>
    /// Rating keys for which episodes have been requested
    /// </summary>
    public List<string> EpisodeRequests { get; } = [];

    /// <summary>
    /// Library keys for which items have been requested
    /// </summary>
    public List<string> LibraryItemRequests { get; } = [];

    #endregion // Properties

    #region IPlexClient

    /// <summary>
    /// Return the configured owner account id
    /// </summary>
    /// <param name="cancellationToken">Ignored cancellation token</param>
    /// <returns>The configured owner account id</returns>
    public Task<int> GetOwnerAccountIdAsync(CancellationToken cancellationToken)
    {
        OwnerAccountIdCalls++;

        return Task.FromResult(OwnerAccountId);
    }

    /// <summary>
    /// Return the configured libraries
    /// </summary>
    /// <param name="cancellationToken">Ignored cancellation token</param>
    /// <returns>The configured libraries</returns>
    public Task<IReadOnlyList<PlexLibrary>> GetLibrariesAsync(CancellationToken cancellationToken)
    {
        if (LibrariesException is not null)
        {
            throw LibrariesException;
        }

        return Task.FromResult<IReadOnlyList<PlexLibrary>>(Libraries);
    }

    /// <summary>
    /// Return the configured history entries that occurred after the given timestamp
    /// </summary>
    /// <param name="since">Lower bound for the viewed-at timestamp</param>
    /// <param name="accountId">Account id to filter for</param>
    /// <param name="cancellationToken">Ignored cancellation token</param>
    /// <returns>The matching history entries ordered ascending by viewed-at</returns>
    public Task<IReadOnlyList<PlexHistoryEntry>> GetHistorySinceAsync(DateTimeOffset since, int accountId, CancellationToken cancellationToken)
    {
        LastHistorySince = since;
        LastHistoryAccountId = accountId;

        if (HistoryException is not null)
        {
            throw HistoryException;
        }

        var entries = History.Where(entry => entry.ViewedAt > since)
                             .OrderBy(entry => entry.ViewedAt)
                             .ToList();

        return Task.FromResult<IReadOnlyList<PlexHistoryEntry>>(entries);
    }

    /// <summary>
    /// Return the configured media item for the given rating key
    /// </summary>
    /// <param name="ratingKey">Rating key of the item</param>
    /// <param name="cancellationToken">Ignored cancellation token</param>
    /// <returns>The configured media item, or <c>null</c></returns>
    public Task<MediaItem?> GetMediaItemAsync(string ratingKey, CancellationToken cancellationToken)
    {
        Items.TryGetValue(ratingKey, out var item);

        return Task.FromResult(item);
    }

    /// <summary>
    /// Return the configured episodes of the given series
    /// </summary>
    /// <param name="showRatingKey">Rating key of the series</param>
    /// <param name="cancellationToken">Ignored cancellation token</param>
    /// <returns>The configured episodes</returns>
    public Task<IReadOnlyList<MediaItem>> GetEpisodesAsync(string showRatingKey, CancellationToken cancellationToken)
    {
        EpisodeRequests.Add(showRatingKey);

        if (Episodes.TryGetValue(showRatingKey, out var episodes) == false)
        {
            return Task.FromResult<IReadOnlyList<MediaItem>>([]);
        }

        return Task.FromResult<IReadOnlyList<MediaItem>>(episodes);
    }

    /// <summary>
    /// Return the configured items of the given library section
    /// </summary>
    /// <param name="libraryKey">Section key</param>
    /// <param name="cancellationToken">Ignored cancellation token</param>
    /// <returns>The configured items</returns>
    public Task<IReadOnlyList<MediaItem>> GetLibraryItemsAsync(string libraryKey, CancellationToken cancellationToken)
    {
        LibraryItemRequests.Add(libraryKey);

        if (LibraryItems.TryGetValue(libraryKey, out var items) == false)
        {
            return Task.FromResult<IReadOnlyList<MediaItem>>([]);
        }

        return Task.FromResult<IReadOnlyList<MediaItem>>(items);
    }

    #endregion // IPlexClient
}