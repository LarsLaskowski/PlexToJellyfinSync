namespace PlexToJellyfinSync.Service.State;

/// <summary>
/// Serializable representation of the persisted synchronization state
/// </summary>
public sealed class SyncStateFile
{
    #region Constants

    /// <summary>
    /// Schema version written by this release
    /// </summary>
    public const int CurrentSchemaVersion = 1;

    #endregion // Constants

    #region Properties

    /// <summary>
    /// Schema version of the file; <c>null</c> for a file of a release before the version was introduced
    /// </summary>
    public int? SchemaVersion { get; set; }

    /// <summary>
    /// High-water mark of the processed watch history
    /// </summary>
    public DateTimeOffset? HighWaterMark { get; set; }

    #endregion // Properties
}