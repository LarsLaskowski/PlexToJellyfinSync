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
    public int PollIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Interval in hours between full reconcile runs over all libraries
    /// </summary>
    public int FullReconcileIntervalHours { get; set; } = 24;

    /// <summary>
    /// Whether a complete NFO file is created when none exists yet
    /// </summary>
    public bool CreateMissingNfo { get; set; } = true;

    /// <summary>
    /// Whether aggregated watch state is written to <c>season.nfo</c> and <c>tvshow.nfo</c>
    /// </summary>
    public bool WriteSeriesSeasonAggregates { get; set; } = true;

    #endregion // Properties
}