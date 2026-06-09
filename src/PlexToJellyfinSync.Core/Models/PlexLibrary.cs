using PlexToJellyfinSync.Core.Enums;

namespace PlexToJellyfinSync.Core.Models;

/// <summary>
/// A Plex library section
/// </summary>
public sealed class PlexLibrary
{
    #region Properties

    /// <summary>
    /// Section key
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Section title
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Kind of media contained in the section
    /// </summary>
    public MediaKind Kind { get; set; }

    #endregion // Properties
}