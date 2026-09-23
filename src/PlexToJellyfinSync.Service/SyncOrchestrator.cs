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
    /// Handle an error scoped to a single item by logging it and updating the error counter, without aborting the
    /// current cycle or marking Plex as disconnected
    /// </summary>
    /// <param name="ex">Exception</param>
    /// <param name="ratingKey">Rating key of the item that failed to process</param>
    private void HandleItemError(Exception ex, string ratingKey)
    {
        _logger.LogError(ex, "Processing item {RatingKey} failed, skipping it", ratingKey);
        _status.Update(s =>
                       {
                           s.Errors++;
                           s.LastError = ex.Message;
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

            var seasonOutcome = await _nfoWriter.WriteAsync(seasonItem, seasonDirectory, cancellationToken).ConfigureAwait(false);

            RecordOutcome(seasonOutcome);
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

        var seriesOutcome = await _nfoWriter.WriteAsync(seriesItem, showDirectory, cancellationToken).ConfigureAwait(false);

        RecordOutcome(seriesOutcome);
    }

    /// <summary>
    /// Process every history entry, writing the NFO for each affected item, and determine the newest viewed-at
    /// timestamp and the shows touched by an episode
    /// </summary>
    /// <param name="entries">History entries to process</param>
    /// <param name="since">Current high-water mark, used as the initial newest viewed-at timestamp</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The newest viewed-at timestamp seen and the rating keys of the shows touched by an episode</returns>
    private async Task<(DateTimeOffset MaxViewed, HashSet<string> AffectedShows)> ProcessHistoryEntriesAsync(IReadOnlyList<PlexHistoryEntry> entries,
                                                                                                             DateTimeOffset since,
                                                                                                             CancellationToken cancellationToken)
    {
        var maxViewed = since;
        var affectedShows = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var item = await TryProcessRatingKeyAsync(entry.RatingKey, cancellationToken).ConfigureAwait(false);

            if (item is not null && item.Kind == MediaKind.Episode && string.IsNullOrWhiteSpace(item.ShowRatingKey) == false)
            {
                affectedShows.Add(item.ShowRatingKey!);
            }

            if (entry.ViewedAt > maxViewed)
            {
                maxViewed = entry.ViewedAt;
            }
        }

        return (maxViewed, affectedShows);
    }

    /// <summary>
    /// Process a single item by its rating key, isolating a failure so it does not abort the caller's loop
    /// </summary>
    /// <param name="ratingKey">Rating key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The processed item, or <c>null</c> if it does not exist or failed to process</returns>
    private async Task<MediaItem?> TryProcessRatingKeyAsync(string ratingKey, CancellationToken cancellationToken)
    {
        try
        {
            return await ProcessRatingKeyAsync(ratingKey, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            HandleItemError(ex, ratingKey);

            return null;
        }
    }

    /// <summary>
    /// Update the aggregated season and series NFO files for every affected show, isolating a failure so it does
    /// not abort the remaining shows
    /// </summary>
    /// <param name="affectedShows">Rating keys of the shows to update</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task</returns>
    private async Task UpdateAffectedShowAggregatesAsync(IEnumerable<string> affectedShows, CancellationToken cancellationToken)
    {
        foreach (var show in affectedShows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await UpdateSeriesAggregatesAsync(show, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                HandleItemError(ex, show);
            }
        }
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

            var (maxViewed, affectedShows) = await ProcessHistoryEntriesAsync(entries, since.Value, cancellationToken).ConfigureAwait(false);

            if (_syncOptions.WriteSeriesSeasonAggregates)
            {
                await UpdateAffectedShowAggregatesAsync(affectedShows, cancellationToken).ConfigureAwait(false);
            }

            if (maxViewed > since.Value)
            {
                await _stateStore.SetHighWaterMarkAsync(maxViewed, cancellationToken).ConfigureAwait(false);
                _status.Update(s => s.HighWaterMark = maxViewed);
            }

            _status.Update(s => s.LastPollAt = DateTimeOffset.UtcNow);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
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
            var librariesToReconcile = libraries.Where(library => filter.Length == 0 || filter.Contains(library.Key))
                                                .ToList();

            var parallelOptions = new ParallelOptions
                                  {
                                      MaxDegreeOfParallelism = Math.Max(1, _syncOptions.LibraryReconcileParallelism),
                                      CancellationToken = cancellationToken
                                  };

            await Parallel.ForEachAsync(librariesToReconcile, parallelOptions, ReconcileLibraryAsync).ConfigureAwait(false);

            _status.Update(s => s.LastReconcileAt = DateTimeOffset.UtcNow);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
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
    /// Reconcile a single library according to its kind, isolating the dispatch so it can run concurrently with
    /// the other configured libraries
    /// </summary>
    /// <param name="library">Library to reconcile</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task</returns>
    private async ValueTask ReconcileLibraryAsync(PlexLibrary library, CancellationToken cancellationToken)
    {
        if (library.Kind == MediaKind.Movie)
        {
            await ReconcileMovieLibraryAsync(library.Key, cancellationToken).ConfigureAwait(false);
        }
        else if (library.Kind == MediaKind.Series)
        {
            await ReconcileSeriesLibraryAsync(library.Key, cancellationToken).ConfigureAwait(false);
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
        await foreach (var movie in _plexClient.GetLibraryItemsAsync(libraryKey, cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();

            _status.Update(s => s.ItemsProcessed++);

            try
            {
                await WriteItemAsync(movie, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                HandleItemError(ex, movie.RatingKey);
            }
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
        await foreach (var show in _plexClient.GetLibraryItemsAsync(libraryKey, cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await ReconcileShowEpisodesAsync(show.RatingKey, cancellationToken).ConfigureAwait(false);

                if (_syncOptions.WriteSeriesSeasonAggregates)
                {
                    await UpdateSeriesAggregatesAsync(show.RatingKey, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                HandleItemError(ex, show.RatingKey);
            }
        }
    }

    /// <summary>
    /// Write the NFO for every episode of a show, running unrelated episodes concurrently while episodes that
    /// share a file (a multi-episode file such as S01E01-E02.mkv) stay sequential within their own group so two
    /// writers never race over the same NFO target's temp file
    /// </summary>
    /// <param name="showRatingKey">Rating key of the show</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task</returns>
    private async Task ReconcileShowEpisodesAsync(string showRatingKey, CancellationToken cancellationToken)
    {
        var episodes = await _plexClient.GetEpisodesAsync(showRatingKey, cancellationToken).ToListAsync(cancellationToken).ConfigureAwait(false);
        var episodeGroups = episodes.GroupBy(episode => episode.FilePath, StringComparer.Ordinal).ToList();

        var parallelOptions = new ParallelOptions
                              {
                                  MaxDegreeOfParallelism = Math.Max(1, _syncOptions.EpisodeReconcileParallelism),
                                  CancellationToken = cancellationToken
                              };

        await Parallel.ForEachAsync(episodeGroups, parallelOptions, WriteEpisodeGroupAsync).ConfigureAwait(false);
    }

    /// <summary>
    /// Write every episode of a group (episodes that resolve to the same NFO target) one after another, isolating
    /// a failure so it does not abort the rest of the group or the other groups running concurrently
    /// </summary>
    /// <param name="episodeGroup">Episodes sharing one NFO target</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task</returns>
    private async ValueTask WriteEpisodeGroupAsync(IEnumerable<MediaItem> episodeGroup, CancellationToken cancellationToken)
    {
        foreach (var episode in episodeGroup)
        {
            _status.Update(s => s.ItemsProcessed++);

            try
            {
                await WriteItemAsync(episode, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                HandleItemError(ex, episode.RatingKey);
            }
        }
    }

    #endregion // ISyncOrchestrator
}