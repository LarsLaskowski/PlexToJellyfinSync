namespace PlexToJellyfinSync.Core.Models;

/// <summary>
/// Snapshot of the current synchronization status for display on the dashboard
/// </summary>
public sealed class SyncStatusViewData
{
    #region Properties

    /// <summary>
    /// Whether the Plex server was reachable during the last attempt
    /// </summary>
    public bool PlexConnected { get; set; }

    /// <summary>
    /// Timestamp of the last completed incremental poll
    /// </summary>
    public DateTimeOffset? LastPollAt { get; set; }

    /// <summary>
    /// Timestamp of the next scheduled incremental poll
    /// </summary>
    public DateTimeOffset? NextPollAt { get; set; }

    /// <summary>
    /// Timestamp of the last completed full reconcile
    /// </summary>
    public DateTimeOffset? LastReconcileAt { get; set; }

    /// <summary>
    /// Current high-water mark of the processed watch history
    /// </summary>
    public DateTimeOffset? HighWaterMark { get; set; }

    /// <summary>
    /// Number of items processed since application start
    /// </summary>
    public long ItemsProcessed { get; set; }

    /// <summary>
    /// Number of NFO files created since application start
    /// </summary>
    public long NfoCreated { get; set; }

    /// <summary>
    /// Number of NFO files updated since application start
    /// </summary>
    public long NfoUpdated { get; set; }

    /// <summary>
    /// Number of errors encountered since application start
    /// </summary>
    public long Errors { get; set; }

    /// <summary>
    /// Last error message, if any
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// Whether a synchronization run is currently in progress
    /// </summary>
    public bool IsRunning { get; set; }

    /// <summary>
    /// Timestamp at which the application started
    /// </summary>
    public DateTimeOffset StartedAt { get; set; }

    #endregion // Properties
}