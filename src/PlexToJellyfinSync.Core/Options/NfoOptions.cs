using PlexToJellyfinSync.Core.Enums;

namespace PlexToJellyfinSync.Core.Options;

/// <summary>
/// Configuration for NFO file generation and updating
/// </summary>
public sealed class NfoOptions
{
    #region Constants

    /// <summary>
    /// Configuration section name
    /// </summary>
    public const string SectionName = "Nfo";

    #endregion // Constants

    #region Properties

    /// <summary>
    /// Date/time format used for the <c>lastplayed</c> element
    /// </summary>
    public string DateTimeFormat { get; set; } = "yyyy-MM-dd HH:mm:ss";

    /// <summary>
    /// Strategy for resolving the NFO file name of a movie
    /// </summary>
    public MovieNfoFilenameStrategy MovieFilenameStrategy { get; set; } = MovieNfoFilenameStrategy.PreferExistingMovieNfo;

    #endregion // Properties
}