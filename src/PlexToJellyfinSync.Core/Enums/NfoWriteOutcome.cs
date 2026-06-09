namespace PlexToJellyfinSync.Core.Enums;

/// <summary>
/// Result of an NFO write operation
/// </summary>
public enum NfoWriteOutcome
{
    /// <summary>
    /// Nothing was written (file missing and creation disabled, or no change required)
    /// </summary>
    Skipped = 0,

    /// <summary>
    /// A new NFO file was created
    /// </summary>
    Created,

    /// <summary>
    /// An existing NFO file was updated
    /// </summary>
    Updated
}