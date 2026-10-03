using PlexToJellyfinSync.Core.Abstractions;
using PlexToJellyfinSync.Core.Enums;
using PlexToJellyfinSync.Core.Models;

namespace PlexToJellyfinSync.Service;

/// <summary>
/// Computes and writes the season and series watch state of a show
/// </summary>
public sealed class SeriesAggregateWriter : ISeriesAggregateWriter
{
    #region Fields

    private readonly IPlexClient _plexClient;
    private readonly IPathMapper _pathMapper;
    private readonly IMediaItemWriter _itemWriter;
    private readonly WatchAggregator _aggregator;

    #endregion // Fields

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
        _plexClient = plexClient;
        _pathMapper = pathMapper;
        _itemWriter = itemWriter;
        _aggregator = aggregator;
    }

    #endregion // Constructors

    #region ISeriesAggregateWriter

    /// <inheritdoc />
    public async Task WriteAggregatesAsync(string showRatingKey, CancellationToken cancellationToken)
    {
        var mapped = new List<(MediaItem Episode, string Local)>();

        await foreach (var episode in _plexClient.GetEpisodesAsync(showRatingKey, cancellationToken).ConfigureAwait(false))
        {
            if (string.IsNullOrWhiteSpace(episode.FilePath))
            {
                continue;
            }

            var local = _pathMapper.MapToLocal(episode.FilePath);

            if (local is not null)
            {
                mapped.Add((episode, local));
            }
        }

        if (mapped.Count == 0)
        {
            return;
        }

        foreach (var season in mapped.GroupBy(x => x.Episode.SeasonNumber))
        {
            var seasonDirectory = Path.GetDirectoryName(season.First().Local);

            if (string.IsNullOrEmpty(seasonDirectory))
            {
                continue;
            }

            var seasonItem = new MediaItem
                             {
                                 Kind = MediaKind.Season,
                                 SeasonNumber = season.Key,
                                 Title = season.Key.HasValue ? $"Season {season.Key.Value}" : "Season",
                                 Watch = _aggregator.Aggregate(season.Select(x => x.Episode.Watch).ToList())
                             };

            await _itemWriter.WriteAggregateAsync(seasonItem, seasonDirectory, cancellationToken).ConfigureAwait(false);
        }

        var anySeasonDirectory = Path.GetDirectoryName(mapped[0].Local);
        var showDirectory = string.IsNullOrEmpty(anySeasonDirectory) ? null : Path.GetDirectoryName(anySeasonDirectory);

        if (string.IsNullOrEmpty(showDirectory))
        {
            return;
        }

        var seriesItem = await _plexClient.GetMediaItemAsync(showRatingKey, cancellationToken).ConfigureAwait(false)
                             ?? new MediaItem
                                {
                                    Title = mapped[0].Episode.ShowTitle ?? string.Empty
                                };

        seriesItem.Kind = MediaKind.Series;
        seriesItem.Watch = _aggregator.Aggregate(mapped.Select(x => x.Episode.Watch).ToList());

        await _itemWriter.WriteAggregateAsync(seriesItem, showDirectory, cancellationToken).ConfigureAwait(false);
    }

    #endregion // ISeriesAggregateWriter
}