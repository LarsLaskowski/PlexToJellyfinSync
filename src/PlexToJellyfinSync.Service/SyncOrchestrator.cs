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
    private readonly IMediaItemWriter _itemWriter;
    private readonly ISeriesAggregateWriter _aggregateWriter;
    private readonly ILibraryReconciler _libraryReconciler;
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
    /// <param name="itemWriter">Media item writer</param>
    /// <param name="aggregateWriter">Series aggregate writer</param>
    /// <param name="libraryReconciler">Library reconciler</param>
    /// <param name="stateStore">State store</param>
    /// <param name="status">Status provider</param>
    /// <param name="plexOptions">Plex options</param>
    /// <param name="syncOptions">Sync options</param>
    /// <param name="logger">Logging interface</param>
    public SyncOrchestrator(IPlexClient plexClient,
                            IMediaItemWriter itemWriter,
                            ISeriesAggregateWriter aggregateWriter,
                            ILibraryReconciler libraryReconciler,
                            IStateStore stateStore,
                            ISyncStatusProvider status,
                            IOptions<PlexOptions> plexOptions,
                            IOptions<SyncOptions> syncOptions,
                            ILogger<SyncOrchestrator> logger)
    {
        _plexClient = plexClient;
        _itemWriter = itemWriter;
        _aggregateWriter = aggregateWriter;
        _libraryReconciler = libraryReconciler;
        _stateStore = stateStore;
        _status = status;
        _plexOptions = plexOptions.Value;
        _syncOptions = syncOptions.Value;
        _logger = logger;
    }

    #endregion // Constructors

    #region Methods

    /// <summary>
    /// Resolve and cache the Plex owner account id; the cache is cleared when a cycle fails, so a fallback id
    /// resolved during that cycle is retried on the next poll instead of pinning a bad guess indefinitely. A
    /// fallback resolved during a cycle that then succeeds is still cached until the next failed cycle
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Owner account id</returns>
    private async Task<int> ResolveOwnerAsync(CancellationToken cancellationToken)
    {
        _ownerAccountId ??= await _plexClient.GetOwnerAccountIdAsync(cancellationToken).ConfigureAwait(false);

        return _ownerAccountId.Value;
    }

    /// <summary>
    /// Handle an error by logging it, updating the status and clearing the cached owner account id so a possibly
    /// wrong id resolved during this cycle (for example a transient-failure fallback) is not pinned for the
    /// remaining lifetime of the process; the next cycle re-resolves it
    /// </summary>
    /// <param name="ex">Exception</param>
    private void HandleError(Exception ex)
    {
        _logger.LogError(ex, "Synchronization run failed");
        _ownerAccountId = null;
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

        await _itemWriter.WriteItemAsync(item, cancellationToken).ConfigureAwait(false);

        return item;
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
                affectedShows.Add(item.ShowRatingKey);
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
                await _aggregateWriter.WriteAggregatesAsync(show, cancellationToken).ConfigureAwait(false);
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

            var ownerId = await ResolveOwnerAsync(cancellationToken).ConfigureAwait(false);
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

            await Parallel.ForEachAsync(librariesToReconcile, parallelOptions, _libraryReconciler.ReconcileLibraryAsync).ConfigureAwait(false);

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

    #endregion // ISyncOrchestrator
}