using PlexToJellyfinSync.Core.Models;

namespace PlexToJellyfinSync.Core.Abstractions;

/// <summary>
/// Reconciles the complete watch state of a single Plex library
/// </summary>
public interface ILibraryReconciler
{
    #region Methods

    /// <summary>
    /// Reconcile all items of the given library; item-level failures are isolated and counted, failures outside an
    /// item propagate
    /// </summary>
    /// <param name="library">Library to reconcile</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task that completes when the library has been reconciled</returns>
    ValueTask ReconcileLibraryAsync(PlexLibrary library, CancellationToken cancellationToken);

    #endregion // Methods
}