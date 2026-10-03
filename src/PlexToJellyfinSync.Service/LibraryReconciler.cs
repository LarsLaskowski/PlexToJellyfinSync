using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using PlexToJellyfinSync.Core.Abstractions;
using PlexToJellyfinSync.Core.Enums;
using PlexToJellyfinSync.Core.Models;
using PlexToJellyfinSync.Core.Options;

namespace PlexToJellyfinSync.Service;

/// <summary>
/// Reconciles the complete watch state of a single Plex library
/// </summary>
public sealed class LibraryReconciler : ILibraryReconciler
{
    #region Fields

    private readonly IPlexClient _plexClient;
    private readonly IMediaItemWriter _itemWriter;
    private readonly ISeriesAggregateWriter _aggregateWriter;
    private readonly ISyncStatusProvider _status;
    private readonly SyncOptions _syncOptions;
    private readonly ILogger<LibraryReconciler> _logger;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="plexClient">Plex client</param>
    /// <param name="itemWriter">Media item writer</param>
    /// <param name="aggregateWriter">Series aggregate writer</param>
    /// <param name="status">Status provider</param>
    /// <param name="syncOptions">Sync options</param>
    /// <param name="logger">Logging interface</param>
    public LibraryReconciler(IPlexClient plexClient,
                             IMediaItemWriter itemWriter,
                             ISeriesAggregateWriter aggregateWriter,
                             ISyncStatusProvider status,
                             IOptions<SyncOptions> syncOptions,
                             ILogger<LibraryReconciler> logger)
    {
        _plexClient = plexClient;
        _itemWriter = itemWriter;
        _aggregateWriter = aggregateWriter;
        _status = status;
        _syncOptions = syncOptions.Value;
        _logger = logger;
    }

    #endregion // Constructors

    #region Methods

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
                await _itemWriter.WriteItemAsync(movie, cancellationToken).ConfigureAwait(false);
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
                    await _aggregateWriter.WriteAggregatesAsync(show.RatingKey, cancellationToken).ConfigureAwait(false);
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
                await _itemWriter.WriteItemAsync(episode, cancellationToken).ConfigureAwait(false);
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

    #endregion // Methods

    #region ILibraryReconciler

    /// <inheritdoc />
    public async ValueTask ReconcileLibraryAsync(PlexLibrary library, CancellationToken cancellationToken)
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

    #endregion // ILibraryReconciler
}