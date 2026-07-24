using PlexToJellyfinSync.Core.Models;

namespace PlexToJellyfinSync.Tests;

/// <summary>
/// A single NFO write captured by <see cref="RecordingNfoWriter"/>
/// </summary>
internal sealed class NfoWriteRecord
{
    #region Properties

    /// <summary>
    /// Media item that was written
    /// </summary>
    public MediaItem Item { get; set; } = new();

    /// <summary>
    /// Local path the item was written to
    /// </summary>
    public string LocalPath { get; set; } = string.Empty;

    #endregion // Properties
}