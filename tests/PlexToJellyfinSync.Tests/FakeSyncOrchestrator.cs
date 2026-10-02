using PlexToJellyfinSync.Core.Abstractions;

namespace PlexToJellyfinSync.Tests;

/// <summary>
/// Sync orchestrator fake with a configurable failures that signals when a history sync or reconcile was requested
/// </summary>
internal sealed class FakeSyncOrchestrator : ISyncOrchestrator
{
    #region Fields

    /// <summary>
    /// Completed when <see cref="ReconcileAsync"/> is called for the first time
    /// </summary>
    private readonly TaskCompletionSource _reconcileCalled = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Completed when <see cref="ProcessHistoryAsync"/> is called for the first time
    /// </summary>
    private readonly TaskCompletionSource _processHistoryCalled = new(TaskCreationOptions.RunContinuationsAsynchronously);

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

    /// <summary>
    /// Exception thrown by <see cref="ProcessHistoryAsync"/>, or <c>null</c> to complete normally
    /// </summary>
    public Exception? ProcessHistoryException { get; set; }

    /// <summary>
    /// When set, <see cref="ProcessHistoryAsync"/> waits until its token is cancelled and then throws the cancellation
    /// </summary>
    public bool ProcessHistoryWaitsForCancellation { get; set; }

    /// <summary>
    /// Task that completes once <see cref="ProcessHistoryAsync"/> has been called
    /// </summary>
    public Task ProcessHistoryCalled => _processHistoryCalled.Task;

    #endregion // Properties

    #region ISyncOrchestrator

    /// <inheritdoc/>
    public async Task ProcessHistoryAsync(CancellationToken cancellationToken)
    {
        _processHistoryCalled.TrySetResult();

        if (ProcessHistoryWaitsForCancellation)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
        }

        if (ProcessHistoryException is not null)
        {
            throw ProcessHistoryException;
        }
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