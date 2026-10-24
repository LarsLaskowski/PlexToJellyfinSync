using Microsoft.Extensions.Options;

using PlexToJellyfinSync.Core.Abstractions;
using PlexToJellyfinSync.Core.Options;

namespace PlexToJellyfinSync.Service;

/// <summary>
/// The collaborators and options a <see cref="SyncOrchestrator"/> works with
/// </summary>
public sealed class SyncOrchestratorDependencies
{
    #region Properties

    /// <summary>
    /// Plex client
    /// </summary>
    public required IPlexClient PlexClient { get; init; }

    /// <summary>
    /// Media item writer
    /// </summary>
    public required IMediaItemWriter ItemWriter { get; init; }

    /// <summary>
    /// Series aggregate writer
    /// </summary>
    public required ISeriesAggregateWriter AggregateWriter { get; init; }

    /// <summary>
    /// Library reconciler
    /// </summary>
    public required ILibraryReconciler LibraryReconciler { get; init; }

    /// <summary>
    /// State store
    /// </summary>
    public required IStateStore StateStore { get; init; }

    /// <summary>
    /// Status provider
    /// </summary>
    public required ISyncStatusProvider Status { get; init; }

    /// <summary>
    /// Plex options
    /// </summary>
    public required IOptions<PlexOptions> PlexOptions { get; init; }

    /// <summary>
    /// Sync options
    /// </summary>
    public required IOptions<SyncOptions> SyncOptions { get; init; }

    #endregion // Properties
}