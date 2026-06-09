namespace PlexToJellyfinSync.Core.Abstractions;

/// <summary>
/// Persists the synchronization state across restarts
/// </summary>
public interface IStateStore
{
    #region Methods

    /// <summary>
    /// Get the persisted high-water mark of the processed watch history
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The high-water mark, or <c>null</c> if none has been persisted yet</returns>
    Task<DateTimeOffset?> GetHighWaterMarkAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Persist the high-water mark of the processed watch history
    /// </summary>
    /// <param name="value">New high-water mark</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task</returns>
    Task SetHighWaterMarkAsync(DateTimeOffset value, CancellationToken cancellationToken);

    #endregion // Methods
}