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

    #endregion // Methods
}