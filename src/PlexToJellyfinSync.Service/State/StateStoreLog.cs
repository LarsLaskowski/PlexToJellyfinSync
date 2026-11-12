using Microsoft.Extensions.Logging;

namespace PlexToJellyfinSync.Service.State;

/// <summary>
/// Source-generated log messages of <see cref="StateStore"/>
/// </summary>
internal static partial class StateStoreLog
{
    #region Methods

    /// <summary>
    /// Log that a corrupt state file was preserved
    /// </summary>
    /// <param name="logger">Logging interface</param>
    /// <param name="exception">Parse failure</param>
    /// <param name="path">State file path</param>
    /// <param name="corruptPath">Path the file was preserved at</param>
    [LoggerMessage(EventId = 1, EventName = "CorruptFilePreserved", Level = LogLevel.Error, Message = "State file {Path} is corrupt, preserved as {CorruptPath}; starting fresh with no high-water mark")]
    public static partial void CorruptFilePreserved(ILogger logger, Exception exception, string path, string corruptPath);

    /// <summary>
    /// Log that a corrupt state file could not be preserved
    /// </summary>
    /// <param name="logger">Logging interface</param>
    /// <param name="exception">Parse failure</param>
    /// <param name="path">State file path</param>
    [LoggerMessage(EventId = 2, EventName = "CorruptFileNotPreserved", Level = LogLevel.Error, Message = "State file {Path} is corrupt and could not be preserved; starting fresh with no high-water mark")]
    public static partial void CorruptFileNotPreserved(ILogger logger, Exception exception, string path);

    /// <summary>
    /// Log that the state file could not be read
    /// </summary>
    /// <param name="logger">Logging interface</param>
    /// <param name="exception">Read failure</param>
    /// <param name="path">State file path</param>
    [LoggerMessage(EventId = 3, EventName = "ReadFailed", Level = LogLevel.Warning, Message = "Could not read state file {Path}, starting fresh")]
    public static partial void ReadFailed(ILogger logger, Exception exception, string path);

    /// <summary>
    /// Log that a state file of an unsupported schema version was preserved
    /// </summary>
    /// <param name="logger">Logging interface</param>
    /// <param name="path">State file path</param>
    /// <param name="schemaVersion">Schema version read from the file</param>
    /// <param name="preservedPath">Path the file was preserved at</param>
    [LoggerMessage(EventId = 4, EventName = "UnsupportedFilePreserved", Level = LogLevel.Error, Message = "State file {Path} has unsupported schema version {SchemaVersion}, preserved as {PreservedPath}; starting fresh with no high-water mark")]
    public static partial void UnsupportedFilePreserved(ILogger logger, string path, int schemaVersion, string preservedPath);

    /// <summary>
    /// Log that a state file of an unsupported schema version could not be preserved
    /// </summary>
    /// <param name="logger">Logging interface</param>
    /// <param name="path">State file path</param>
    /// <param name="schemaVersion">Schema version read from the file</param>
    [LoggerMessage(EventId = 5, EventName = "UnsupportedFileNotPreserved", Level = LogLevel.Error, Message = "State file {Path} has unsupported schema version {SchemaVersion} and could not be preserved; starting fresh with no high-water mark")]
    public static partial void UnsupportedFileNotPreserved(ILogger logger, string path, int schemaVersion);

    #endregion // Methods
}