using Microsoft.Extensions.Logging;

namespace PlexToJellyfinSync.Core.Models;

/// <summary>
/// A single captured log entry shown on the dashboard
/// </summary>
public sealed class LogEntry
{
    #region Properties

    /// <summary>
    /// Timestamp at which the entry was created
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// Log level
    /// </summary>
    public LogLevel Level { get; set; }

    /// <summary>
    /// Logger category
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Formatted message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Optional exception text
    /// </summary>
    public string? Exception { get; set; }

    #endregion // Properties
}