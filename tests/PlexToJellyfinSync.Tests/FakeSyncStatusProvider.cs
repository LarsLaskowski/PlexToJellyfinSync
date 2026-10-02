using PlexToJellyfinSync.Core.Abstractions;
using PlexToJellyfinSync.Core.Models;

namespace PlexToJellyfinSync.Tests;

/// <summary>
/// Sync status provider fake that serves a settable snapshot, raises <see cref="Changed"/> on demand and
/// records on which thread context every snapshot was requested
/// </summary>
internal sealed class FakeSyncStatusProvider : ISyncStatusProvider
{
    #region Fields

    /// <summary>
    /// Synchronizes access to the recorded values
    /// </summary>
    private readonly object _lock = new();

    /// <summary>
    /// Results of <see cref="DispatcherCheck"/> for every snapshot request
    /// </summary>
    private readonly List<bool> _dispatcherChecks = [];

    #endregion // Fields

    #region Properties

    /// <summary>
    /// Snapshot returned by <see cref="GetSnapshot"/>
    /// </summary>
    public SyncStatusViewData Snapshot { get; set; } = new();

    /// <summary>
    /// Optional callback that tells whether the current call is made on the renderer's dispatcher
    /// </summary>
    public Func<bool>? DispatcherCheck { get; set; }

    /// <summary>
    /// Number of snapshot requests so far
    /// </summary>
    public int SnapshotCalls
    {
        get
        {
            lock (_lock)
            {
                return _dispatcherChecks.Count;
            }
        }
    }

    /// <summary>
    /// Number of subscribers currently attached to <see cref="Changed"/>
    /// </summary>
    public int SubscriberCount => Changed?.GetInvocationList().Length ?? 0;

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Raise <see cref="Changed"/> on the calling thread
    /// </summary>
    public void RaiseChanged()
    {
        Changed?.Invoke();
    }

    /// <summary>
    /// Get the recorded dispatcher check results of all snapshot requests so far
    /// </summary>
    /// <returns>Copy of the recorded results, in call order</returns>
    public IReadOnlyList<bool> GetDispatcherChecks()
    {
        lock (_lock)
        {
            return [.. _dispatcherChecks];
        }
    }

    /// <summary>
    /// Forget all recorded snapshot requests
    /// </summary>
    public void ResetCalls()
    {
        lock (_lock)
        {
            _dispatcherChecks.Clear();
        }
    }

    #endregion // Methods

    #region ISyncStatusProvider

    /// <inheritdoc/>
    public event Action? Changed;

    /// <inheritdoc/>
    public SyncStatusViewData GetSnapshot()
    {
        var onDispatcher = DispatcherCheck?.Invoke() ?? false;

        lock (_lock)
        {
            _dispatcherChecks.Add(onDispatcher);
        }

        return Snapshot;
    }

    /// <inheritdoc/>
    public void Update(Action<SyncStatusViewData> mutate)
    {
        mutate(Snapshot);
        RaiseChanged();
    }

    #endregion // ISyncStatusProvider
}