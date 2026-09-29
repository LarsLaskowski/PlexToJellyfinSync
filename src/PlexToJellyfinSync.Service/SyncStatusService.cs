using Microsoft.Extensions.Logging;

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
    private readonly ILogger<SyncStatusService> _logger;
    private readonly SyncStatusViewData _status;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="logger">Logger</param>
    public SyncStatusService(ILogger<SyncStatusService> logger)
    {
        _logger = logger;
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

        var handlers = Changed;

        if (handlers is null)
        {
            return;
        }

        // A faulty subscriber (a dashboard component) must not disturb the caller, usually the sync cycle
        foreach (var handler in handlers.GetInvocationList().Cast<Action>())
        {
            try
            {
                handler();
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "A status change subscriber threw an exception and was skipped");
            }
        }
    }

    #endregion // ISyncStatusProvider
}