using PlexToJellyfinSync.Core.Models;

namespace PlexToJellyfinSync.Core.Abstractions;

/// <summary>
/// Holds the current synchronization status and notifies subscribers about changes
/// </summary>
public interface ISyncStatusProvider
{
    #region Events

    /// <summary>
    /// Raised whenever the status changes
    /// </summary>
    event Action? Changed;

    #endregion // Events

    #region Methods

    /// <summary>
    /// Get an immutable snapshot of the current status
    /// </summary>
    /// <returns>A copy of the current status</returns>
    SyncStatusViewData GetSnapshot();

    /// <summary>
    /// Apply a mutation to the status and notify subscribers
    /// </summary>
    /// <param name="mutate">Action that mutates the status</param>
    void Update(Action<SyncStatusViewData> mutate);

    #endregion // Methods
}