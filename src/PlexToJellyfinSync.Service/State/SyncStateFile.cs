namespace PlexToJellyfinSync.Service.State;

/// <summary>
/// Serializable representation of the persisted synchronization state
/// </summary>
public sealed class SyncStateFile
{
    #region Properties

    /// <summary>
    /// High-water mark of the processed watch history
    /// </summary>
    public DateTimeOffset? HighWaterMark { get; set; }

    #endregion // Properties
}