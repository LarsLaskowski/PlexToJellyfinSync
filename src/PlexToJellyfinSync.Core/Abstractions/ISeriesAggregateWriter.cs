using PlexToJellyfinSync.Core.Models;

namespace PlexToJellyfinSync.Core.Abstractions;

/// <summary>
/// Computes and writes the season and series watch state of a show
/// </summary>
public interface ISeriesAggregateWriter
{
    #region Methods

    /// <summary>
    /// Re-fetch the episodes of a show, aggregate the mapped ones per season and for the series and write the
    /// season and series NFO files
    /// </summary>
    /// <param name="showRatingKey">Plex rating key of the show</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task that completes when the aggregates have been written</returns>
    Task WriteAggregatesAsync(string showRatingKey, CancellationToken cancellationToken);

    /// <summary>
    /// Aggregate the given, already fetched episodes of a show per season and for the series and write the
    /// season and series NFO files without fetching the episodes again
    /// </summary>
    /// <param name="showRatingKey">Plex rating key of the show</param>
    /// <param name="episodes">Episodes of the show</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task that completes when the aggregates have been written</returns>
    Task WriteAggregatesAsync(string showRatingKey, IReadOnlyList<MediaItem> episodes, CancellationToken cancellationToken);

    #endregion // Methods
}