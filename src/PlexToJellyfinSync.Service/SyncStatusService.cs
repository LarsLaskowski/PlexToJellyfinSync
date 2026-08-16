using PlexToJellyfinSync.Core.Abstractions;
using PlexToJellyfinSync.Core.Models;

namespace PlexToJellyfinSync.Service;

/// <summary>
/// Thread-safe holder of the current synchronization status
/// </summary>
public sealed class SyncStatusService : ISyncStatusProvider
{
    #region Fields

    private readonly Lock _lock = new();
    private readonly SyncStatusViewData _status;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    public SyncStatusService()
    {
        _status = new SyncStatusViewData
                  {
                      StartedAt = DateTimeOffset.UtcNow
                  };
    }

    #endregion // Constructors

    #region ISyncStatusProvider

    #region Events

    /// <inheritdoc/>
    public event Action? Changed;

    #endregion // Events

    /// <inheritdoc/>
    public SyncStatusViewData GetSnapshot()
    {
        lock (_lock)
        {
            return new SyncStatusViewData
                   {
                       PlexConnected = _status.PlexConnected,
                       LastPollAt = _status.LastPollAt,
                       NextPollAt = _status.NextPollAt,
                       LastReconcileAt = _status.LastReconcileAt,
                       HighWaterMark = _status.HighWaterMark,
                       ItemsProcessed = _status.ItemsProcessed,
                       NfoCreated = _status.NfoCreated,
                       NfoUpdated = _status.NfoUpdated,
                       Errors = _status.Errors,
                       LastError = _status.LastError,
                       IsRunning = _status.IsRunning,
                       StartedAt = _status.StartedAt
                   };
        }
    }

    /// <inheritdoc/>
    public void Update(Action<SyncStatusViewData> mutate)
    {
        lock (_lock)
        {
            mutate(_status);
        }

        Changed?.Invoke();
    }

    #endregion // ISyncStatusProvider
}