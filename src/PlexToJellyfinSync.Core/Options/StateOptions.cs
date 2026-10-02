using System.ComponentModel.DataAnnotations;

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
    [Required(ErrorMessage = "State:Directory must not be empty (environment variable PLEXSYNC__State__Directory).")]
    public string Directory { get; set; } = "/config";

    #endregion // Properties
}