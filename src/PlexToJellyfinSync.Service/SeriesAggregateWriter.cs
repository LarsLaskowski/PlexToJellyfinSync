using PlexToJellyfinSync.Core.Abstractions;

namespace PlexToJellyfinSync.Service;

/// <summary>
/// Computes and writes the season and series watch state of a show
/// </summary>
public sealed class SeriesAggregateWriter : ISeriesAggregateWriter
{
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="plexClient">Plex client</param>
    /// <param name="pathMapper">Path mapper</param>
    /// <param name="itemWriter">Media item writer</param>
    /// <param name="aggregator">Watch aggregator</param>
    public SeriesAggregateWriter(IPlexClient plexClient,
                                 IPathMapper pathMapper,
                                 IMediaItemWriter itemWriter,
                                 WatchAggregator aggregator)
    {
    }

    #endregion // Constructors

    #region ISeriesAggregateWriter

    /// <inheritdoc />
    public Task WriteAggregatesAsync(string showRatingKey, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    #endregion // ISeriesAggregateWriter
}