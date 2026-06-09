namespace PlexToJellyfinSync.Core.Abstractions;

/// <summary>
/// Coordinates reading watch data from Plex and writing it into NFO files
/// </summary>
public interface ISyncOrchestrator
{
    #region Methods

    /// <summary>
    /// Process the incremental watch history since the persisted high-water mark
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task</returns>
    Task ProcessHistoryAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Reconcile all items of the configured libraries with their NFO files
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task</returns>
    Task ReconcileAsync(CancellationToken cancellationToken);

    #endregion // Methods
}