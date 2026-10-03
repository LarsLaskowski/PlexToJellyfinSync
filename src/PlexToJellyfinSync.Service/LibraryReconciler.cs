using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using PlexToJellyfinSync.Core.Abstractions;
using PlexToJellyfinSync.Core.Models;
using PlexToJellyfinSync.Core.Options;

namespace PlexToJellyfinSync.Service;

/// <summary>
/// Reconciles the complete watch state of a single Plex library
/// </summary>
public sealed class LibraryReconciler : ILibraryReconciler
{
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="plexClient">Plex client</param>
    /// <param name="itemWriter">Media item writer</param>
    /// <param name="aggregateWriter">Series aggregate writer</param>
    /// <param name="status">Status provider</param>
    /// <param name="syncOptions">Sync options</param>
    /// <param name="logger">Logging interface</param>
    public LibraryReconciler(IPlexClient plexClient,
                             IMediaItemWriter itemWriter,
                             ISeriesAggregateWriter aggregateWriter,
                             ISyncStatusProvider status,
                             IOptions<SyncOptions> syncOptions,
                             ILogger<LibraryReconciler> logger)
    {
    }

    #endregion // Constructors

    #region ILibraryReconciler

    /// <inheritdoc />
    public ValueTask ReconcileLibraryAsync(PlexLibrary library, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    #endregion // ILibraryReconciler
}