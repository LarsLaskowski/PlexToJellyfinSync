using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using PlexToJellyfinSync.Core.Abstractions;
using PlexToJellyfinSync.Core.Enums;
using PlexToJellyfinSync.Core.Models;
using PlexToJellyfinSync.Core.Options;

namespace PlexToJellyfinSync.Service;

/// <summary>
/// Coordinates reading watch data from Plex and writing it into NFO files
/// </summary>
public sealed class SyncOrchestrator : ISyncOrchestrator
{
    #region Fields

    private readonly IPlexClient _plexClient;
    private readonly INfoWriter _nfoWriter;
    private readonly IPathMapper _pathMapper;
    private readonly WatchAggregator _aggregator;
    private readonly IStateStore _stateStore;
    private readonly ISyncStatusProvider _status;
    private readonly PlexOptions _plexOptions;
    private readonly SyncOptions _syncOptions;
    private readonly ILogger<SyncOrchestrator> _logger;

    private int? _ownerAccountId;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="plexClient">Plex client</param>
    /// <param name="nfoWriter">NFO writer</param>
    /// <param name="pathMapper">Path mapper</param>
    /// <param name="aggregator">Watch aggregator</param>
    /// <param name="stateStore">State store</param>
    /// <param name="status">Status provider</param>
    /// <param name="plexOptions">Plex options</param>
    /// <param name="syncOptions">Sync options</param>
    /// <param name="logger">Logging interface</param>
    public SyncOrchestrator(IPlexClient plexClient,
                            INfoWriter nfoWriter,
                            IPathMapper pathMapper,
                            WatchAggregator aggregator,
                            IStateStore stateStore,
                            ISyncStatusProvider status,
                            IOptions<PlexOptions> plexOptions,
                            IOptions<SyncOptions> syncOptions,
                            ILogger<SyncOrchestrator> logger)
    {
        _plexClient = plexClient;
        _nfoWriter = nfoWriter;
        _pathMapper = pathMapper;
        _aggregator = aggregator;
        _stateStore = stateStore;
        _status = status;
        _plexOptions = plexOptions.Value;
        _syncOptions = syncOptions.Value;
        _logger = logger;
    }

    #endregion // Constructors

    #region Methods

    /// <summary>
    /// Resolve and cache the Plex owner account id
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Owner account id</returns>
    private async Task<int> ResolveOwnerAsync(CancellationToken cancellationToken)
    {
        _ownerAccountId ??= await _plexClient.GetOwnerAccountIdAsync(cancellationToken).ConfigureAwait(false);

        return _ownerAccountId.Value;
    }

    /// <summary>
    /// Record an NFO write outcome in the status
    /// </summary>
    /// <param name="outcome">Write outcome</param>
    private void RecordOutcome(NfoWriteOutcome outcome)
    {
        if (outcome == NfoWriteOutcome.Created)
        {
            _status.Update(s => s.NfoCreated++);
        }
        else if (outcome == NfoWriteOutcome.Updated)
        {
            _status.Update(s => s.NfoUpdated++);
        }
    }

    /// <summary>
    /// Handle an error by logging it and updating the status
    /// </summary>
    /// <param name="ex">Exception</param>
    private void HandleError(Exception ex)
    {
        _logger.LogError(ex, "Synchronization run failed");
        _status.Update(s =>
                       {
                           s.Errors++;
                           s.LastError = ex.Message;
                           s.PlexConnected = false;
                       });
    }

    /// <summary>
    /// Write the NFO file for a movie or episode
    /// </summary>
    /// <param name="item">Media item</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task</returns>
    private async Task WriteItemAsync(MediaItem item, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(item.FilePath))
        {
            _logger.LogWarning("Item {RatingKey} ({Title}) has no file path, skipping", item.RatingKey, item.Title);

            return;
        }

        var localPath = _pathMapper.MapToLocal(item.FilePath);

        if (localPath is null)
        {
            _logger.LogWarning("No path mapping for {FilePath}, skipping", item.FilePath);

            return;
        }

        var outcome = await _nfoWriter.WriteAsync(item, localPath, cancellationToken).ConfigureAwait(false);

        RecordOutcome(outcome);
    }

    /// <summary>
    /// Process a single item by its rating key
    /// </summary>
    /// <param name="ratingKey">Rating key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The processed item, or <c>null</c></returns>
    private async Task<MediaItem?> ProcessRatingKeyAsync(string ratingKey, CancellationToken cancellationToken)
    {
        var item = await _plexClient.GetMediaItemAsync(ratingKey, cancellationToken).ConfigureAwait(false);

        if (item is null)
        {
            return null;
        }

        _status.Update(s => s.ItemsProcessed++);

        await WriteItemAsync(item, cancellationToken).ConfigureAwait(false);

        return item;
    }

    /// <summary>
    /// Update the aggregated season and series NFO files for a show
    /// </summary>
    /// <param name="showRatingKey">Rating key of the show</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task</returns>
    private async Task UpdateSeriesAggregatesAsync(string showRatingKey, CancellationToken cancellationToken)
    {
        var episodes = await _plexClient.GetEpisodesAsync(showRatingKey, cancellationToken).ConfigureAwait(false);

        var mapped = episodes.Select(e => new
                                          {
                                              Episode = e,
                                              Local = string.IsNullOrWhiteSpace(e.FilePath)
                                                          ? null
                                                          : _pathMapper.MapToLocal(e.FilePath)
                                          })
                             .Where(x => x.Local is not null)
                             .ToList();

        if (mapped.Count == 0)
        {
            return;
        }

        foreach (var season in mapped.GroupBy(x => x.Episode.SeasonNumber))
        {
            var seasonDirectory = Path.GetDirectoryName(season.First().Local!);

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

            var seasonOutcome = await _nfoWriter.WriteAsync(seasonItem, seasonDirectory, cancellationToken).ConfigureAwait(false);

            RecordOutcome(seasonOutcome);
        }

        var anySeasonDirectory = Path.GetDirectoryName(mapped[0].Local!);
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

        var seriesOutcome = await _nfoWriter.WriteAsync(seriesItem, showDirectory, cancellationToken).ConfigureAwait(false);

        RecordOutcome(seriesOutcome);
    }

    #endregion // Methods

    #region ISyncOrchestrator

    /// <inheritdoc/>
    public async Task ProcessHistoryAsync(CancellationToken cancellationToken)
    {
        _status.Update(s => s.IsRunning = true);

        try
        {
            var ownerId = await ResolveOwnerAsync(cancellationToken).ConfigureAwait(false);
            var since = await _stateStore.GetHighWaterMarkAsync(cancellationToken).ConfigureAwait(false);

            if (since is null)
            {
                var now = DateTimeOffset.UtcNow;

                await _stateStore.SetHighWaterMarkAsync(now, cancellationToken).ConfigureAwait(false);
                _status.Update(s =>
                               {
                                   s.HighWaterMark = now;
                                   s.LastPollAt = now;
                                   s.PlexConnected = true;
                               });

                return;
            }

            var entries = await _plexClient.GetHistorySinceAsync(since.Value, ownerId, cancellationToken).ConfigureAwait(false);

            _status.Update(s => s.PlexConnected = true);

            var maxViewed = since.Value;
            var affectedShows = new HashSet<string>(StringComparer.Ordinal);

            foreach (var entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var item = await ProcessRatingKeyAsync(entry.RatingKey, cancellationToken).ConfigureAwait(false);

                if (item is not null && item.Kind == MediaKind.Episode && string.IsNullOrWhiteSpace(item.ShowRatingKey) == false)
                {
                    affectedShows.Add(item.ShowRatingKey!);
                }

                if (entry.ViewedAt > maxViewed)
                {
                    maxViewed = entry.ViewedAt;
                }
            }

            if (_syncOptions.WriteSeriesSeasonAggregates)
            {
                foreach (var show in affectedShows)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    await UpdateSeriesAggregatesAsync(show, cancellationToken).ConfigureAwait(false);
                }
            }

            if (maxViewed > since.Value)
            {
                await _stateStore.SetHighWaterMarkAsync(maxViewed, cancellationToken).ConfigureAwait(false);
                _status.Update(s => s.HighWaterMark = maxViewed);
            }

            _status.Update(s => s.LastPollAt = DateTimeOffset.UtcNow);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            HandleError(ex);
        }
        finally
        {
            _status.Update(s => s.IsRunning = false);
        }
    }

    /// <inheritdoc/>
    public async Task ReconcileAsync(CancellationToken cancellationToken)
    {
        _status.Update(s => s.IsRunning = true);

        try
        {
            var libraries = await _plexClient.GetLibrariesAsync(cancellationToken).ConfigureAwait(false);

            _status.Update(s => s.PlexConnected = true);

            var filter = _plexOptions.Libraries;

            foreach (var library in libraries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (filter.Length > 0 && filter.Contains(library.Key) == false)
                {
                    continue;
                }

                if (library.Kind == MediaKind.Movie)
                {
                    await ReconcileMovieLibraryAsync(library.Key, cancellationToken).ConfigureAwait(false);
                }
                else if (library.Kind == MediaKind.Series)
                {
                    await ReconcileSeriesLibraryAsync(library.Key, cancellationToken).ConfigureAwait(false);
                }
            }

            _status.Update(s => s.LastReconcileAt = DateTimeOffset.UtcNow);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            HandleError(ex);
        }
        finally
        {
            _status.Update(s => s.IsRunning = false);
        }
    }

    /// <summary>
    /// Reconcile all movies of a library
    /// </summary>
    /// <param name="libraryKey">Library key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task</returns>
    private async Task ReconcileMovieLibraryAsync(string libraryKey, CancellationToken cancellationToken)
    {
        var movies = await _plexClient.GetLibraryItemsAsync(libraryKey, cancellationToken).ConfigureAwait(false);

        foreach (var movie in movies)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _status.Update(s => s.ItemsProcessed++);

            await WriteItemAsync(movie, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Reconcile all series of a library including episodes and aggregates
    /// </summary>
    /// <param name="libraryKey">Library key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task</returns>
    private async Task ReconcileSeriesLibraryAsync(string libraryKey, CancellationToken cancellationToken)
    {
        var shows = await _plexClient.GetLibraryItemsAsync(libraryKey, cancellationToken).ConfigureAwait(false);

        foreach (var show in shows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var episodes = await _plexClient.GetEpisodesAsync(show.RatingKey, cancellationToken).ConfigureAwait(false);

            foreach (var episode in episodes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                _status.Update(s => s.ItemsProcessed++);

                await WriteItemAsync(episode, cancellationToken).ConfigureAwait(false);
            }

            if (_syncOptions.WriteSeriesSeasonAggregates)
            {
                await UpdateSeriesAggregatesAsync(show.RatingKey, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    #endregion // ISyncOrchestrator
}