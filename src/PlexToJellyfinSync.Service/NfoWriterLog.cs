using Microsoft.Extensions.Logging;

namespace PlexToJellyfinSync.Service;

/// <summary>
/// Source-generated log messages of <see cref="NfoWriter"/>
/// </summary>
internal static partial class NfoWriterLog
{
    #region Methods

    /// <summary>
    /// Log that a resolved target path escapes every mapped local root
    /// </summary>
    /// <param name="logger">Logging interface</param>
    /// <param name="path">Local path of the media file</param>
    /// <param name="targetPath">Resolved NFO target path</param>
    [LoggerMessage(EventId = 1, EventName = "TargetOutsideMappedRoots", Level = LogLevel.Warning, Message = "Refusing to write NFO for {Path}: resolved target {TargetPath} escapes every mapped local root")]
    public static partial void TargetOutsideMappedRoots(ILogger logger, string path, string targetPath);

    /// <summary>
    /// Log that an existing NFO is too large to be read
    /// </summary>
    /// <param name="logger">Logging interface</param>
    /// <param name="path">NFO path</param>
    /// <param name="maxBytes">Largest file size that is read</param>
    [LoggerMessage(EventId = 2, EventName = "OversizedNfoSkipped", Level = LogLevel.Warning, Message = "Skipping oversized NFO {Path}: existing file is larger than {MaxBytes} bytes")]
    public static partial void OversizedNfoSkipped(ILogger logger, string path, int maxBytes);

    /// <summary>
    /// Log that an existing NFO could not be parsed as XML
    /// </summary>
    /// <param name="logger">Logging interface</param>
    /// <param name="exception">Parse failure</param>
    /// <param name="path">NFO path</param>
    [LoggerMessage(EventId = 3, EventName = "MalformedNfoSkipped", Level = LogLevel.Warning, Message = "Skipping malformed NFO {Path}: existing content could not be parsed as XML")]
    public static partial void MalformedNfoSkipped(ILogger logger, Exception exception, string path);

    /// <summary>
    /// Log that the watch state of an existing NFO was updated
    /// </summary>
    /// <param name="logger">Logging interface</param>
    /// <param name="path">NFO path</param>
    [LoggerMessage(EventId = 4, EventName = "WatchStateUpdated", Level = LogLevel.Information, Message = "Updated watch state in {Path}")]
    public static partial void WatchStateUpdated(ILogger logger, string path);

    /// <summary>
    /// Log that a new NFO was created
    /// </summary>
    /// <param name="logger">Logging interface</param>
    /// <param name="path">NFO path</param>
    [LoggerMessage(EventId = 5, EventName = "NfoCreated", Level = LogLevel.Information, Message = "Created NFO {Path}")]
    public static partial void NfoCreated(ILogger logger, string path);

    #endregion // Methods
}