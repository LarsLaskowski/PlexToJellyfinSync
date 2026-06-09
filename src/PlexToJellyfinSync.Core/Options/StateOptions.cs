namespace PlexToJellyfinSync.Core.Options;

/// <summary>
/// Configuration for the persisted synchronization state
/// </summary>
public sealed class StateOptions
{
    #region Constants

    /// <summary>
    /// Configuration section name
    /// </summary>
    public const string SectionName = "State";

    #endregion // Constants

    #region Properties

    /// <summary>
    /// Directory in which the <c>state.json</c> file is stored
    /// </summary>
    public string Directory { get; set; } = "/config";

    #endregion // Properties
}