using System.Runtime.CompilerServices;

using PlexToJellyfinSync.Core.Abstractions;
using PlexToJellyfinSync.Core.Models;

namespace PlexToJellyfinSync.Tests;

/// <summary>
/// Plex client stub that serves preconfigured data and records the calls it received
/// </summary>
internal sealed class FakePlexClient : IPlexClient
{
    #region Fields

    private readonly Lock _lock = new();

    #endregion // Fields

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
    /// Exceptions thrown when a given library's items are requested, keyed by library section key
    /// </summary>
    public Dictionary<string, Exception> LibraryItemsExceptions { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Tasks awaited immediately before throwing the configured <see cref="LibraryItemsExceptions"/> entry for a
    /// given library, keyed by library section key, so a test can hold the failure in flight until a sibling
    /// library has observably reached some concurrent state first
    /// </summary>
    public Dictionary<string, Task> LibraryItemsGates { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Number of items to stream before throwing, and the exception to throw, keyed by library section key, so a
    /// test can prove that items streamed before a mid-page failure are still processed instead of the whole
    /// library being discarded
    /// </summary>
    public Dictionary<string, (int Count, Exception? Exception)> LibraryItemsFailAfter { get; } = new(StringComparer.Ordinal);

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
    /// Stream the configured episodes of the given series
    /// </summary>
    /// <param name="showRatingKey">Rating key of the series</param>
    /// <param name="cancellationToken">Ignored cancellation token</param>
    /// <returns>The configured episodes</returns>
    public async IAsyncEnumerable<MediaItem> GetEpisodesAsync(string showRatingKey, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.Yield();

        lock (_lock)
        {
            EpisodeRequests.Add(showRatingKey);
        }

        if (Episodes.TryGetValue(showRatingKey, out var episodes))
        {
            foreach (var episode in episodes)
            {
                yield return episode;
            }
        }
    }

    /// <summary>
    /// Stream the configured items of the given library section
    /// </summary>
    /// <param name="libraryKey">Section key</param>
    /// <param name="cancellationToken">Ignored cancellation token</param>
    /// <returns>The configured items</returns>
    public async IAsyncEnumerable<MediaItem> GetLibraryItemsAsync(string libraryKey, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            LibraryItemRequests.Add(libraryKey);
        }

        if (LibraryItemsExceptions.TryGetValue(libraryKey, out var exception))
        {
            if (LibraryItemsGates.TryGetValue(libraryKey, out var gate))
            {
                // A regression back to sequential reconciliation would leave the gate task waiting on a sibling
                // library that never starts; the timeout turns that into a failing assertion instead of a hang.
                await gate.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false);
            }

            throw exception;
        }

        if (LibraryItems.TryGetValue(libraryKey, out var items) == false)
        {
            yield break;
        }

        LibraryItemsFailAfter.TryGetValue(libraryKey, out var failAfter);

        var streamed = 0;

        foreach (var item in items)
        {
            yield return item;

            streamed++;

            if (failAfter.Exception is not null && streamed == failAfter.Count)
            {
                throw failAfter.Exception;
            }
        }
    }

    #endregion // IPlexClient
}