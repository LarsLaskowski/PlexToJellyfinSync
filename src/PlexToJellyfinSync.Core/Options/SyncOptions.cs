using System.ComponentModel.DataAnnotations;

namespace PlexToJellyfinSync.Core.Options;

/// <summary>
/// Configuration for the synchronization behaviour
/// </summary>
public sealed class SyncOptions
{
    #region Constants

    /// <summary>
    /// Configuration section name
    /// </summary>
    public const string SectionName = "Sync";

    #endregion // Constants

    #region Properties

    /// <summary>
    /// Interval in seconds between incremental history polls
    /// </summary>
    [Range(5, 86400, ErrorMessage = "Sync:PollIntervalSeconds must be between 5 and 86400 (environment variable PLEXSYNC__Sync__PollIntervalSeconds).")]
    public int PollIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Interval in hours between full reconcile runs over all libraries
    /// </summary>
    [Range(1, 8760, ErrorMessage = "Sync:FullReconcileIntervalHours must be between 1 and 8760 (environment variable PLEXSYNC__Sync__FullReconcileIntervalHours).")]
    public int FullReconcileIntervalHours { get; set; } = 24;

    /// <summary>
    /// Whether a complete NFO file is created when none exists yet
    /// </summary>
    public bool CreateMissingNfo { get; set; } = true;

    /// <summary>
    /// Whether aggregated watch state is written to <c>season.nfo</c> and <c>tvshow.nfo</c>
    /// </summary>
    public bool WriteSeriesSeasonAggregates { get; set; } = true;

    /// <summary>
    /// Maximum number of episode NFO writes processed concurrently while reconciling a series library
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "Sync:EpisodeReconcileParallelism must be at least 1 (environment variable PLEXSYNC__Sync__EpisodeReconcileParallelism).")]
    public int EpisodeReconcileParallelism { get; set; } = 4;

    /// <summary>
    /// Maximum number of Plex libraries reconciled concurrently during a full reconcile run
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "Sync:LibraryReconcileParallelism must be at least 1 (environment variable PLEXSYNC__Sync__LibraryReconcileParallelism).")]
    public int LibraryReconcileParallelism { get; set; } = 2;

    #endregion // Properties
}