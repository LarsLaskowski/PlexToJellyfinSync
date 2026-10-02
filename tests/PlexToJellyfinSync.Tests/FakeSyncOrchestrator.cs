using PlexToJellyfinSync.Core.Abstractions;

namespace PlexToJellyfinSync.Tests;

/// <summary>
/// Sync orchestrator fake with a configurable reconcile failure that signals when a reconcile was requested
/// </summary>
internal sealed class FakeSyncOrchestrator : ISyncOrchestrator
{
    #region Fields

    /// <summary>
    /// Completed when <see cref="ReconcileAsync"/> is called for the first time
    /// </summary>
    private readonly TaskCompletionSource _reconcileCalled = new(TaskCreationOptions.RunContinuationsAsynchronously);

    #endregion // Fields

    #region Properties

    /// <summary>
    /// Exception thrown by <see cref="ReconcileAsync"/>, or <c>null</c> to complete normally
    /// </summary>
    public Exception? ReconcileException { get; set; }

    /// <summary>
    /// When set, <see cref="ReconcileAsync"/> waits until its token is cancelled and then throws the cancellation
    /// </summary>
    public bool ReconcileWaitsForCancellation { get; set; }

    /// <summary>
    /// Task that completes once <see cref="ReconcileAsync"/> has been called
    /// </summary>
    public Task ReconcileCalled => _reconcileCalled.Task;

    #endregion // Properties

    #region ISyncOrchestrator

    /// <inheritdoc/>
    public Task ProcessHistoryAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task ReconcileAsync(CancellationToken cancellationToken)
    {
        _reconcileCalled.TrySetResult();

        if (ReconcileWaitsForCancellation)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
        }

        if (ReconcileException is not null)
        {
            throw ReconcileException;
        }
    }

    #endregion // ISyncOrchestrator
}