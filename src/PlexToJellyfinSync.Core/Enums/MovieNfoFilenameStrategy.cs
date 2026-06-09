namespace PlexToJellyfinSync.Core.Enums;

/// <summary>
/// Strategy for resolving the NFO file name of a movie
/// </summary>
public enum MovieNfoFilenameStrategy
{
    /// <summary>
    /// Prefer an existing <c>movie.nfo</c>; otherwise use the video file name with the <c>.nfo</c> extension
    /// </summary>
    PreferExistingMovieNfo = 0,

    /// <summary>
    /// Always use the video file name with the <c>.nfo</c> extension
    /// </summary>
    VideoFileName,

    /// <summary>
    /// Always use <c>movie.nfo</c>
    /// </summary>
    MovieNfo
}