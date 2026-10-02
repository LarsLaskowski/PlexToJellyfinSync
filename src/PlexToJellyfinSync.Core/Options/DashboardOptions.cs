using System.ComponentModel.DataAnnotations;

namespace PlexToJellyfinSync.Core.Options;

/// <summary>
/// Configuration for the web dashboard (status and logs)
/// </summary>
public sealed class DashboardOptions
{
    #region Constants

    /// <summary>
    /// Configuration section name
    /// </summary>
    public const string SectionName = "Dashboard";

    #endregion // Constants

    #region Properties

    /// <summary>
    /// Whether the web dashboard is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Optional access token; when empty the dashboard is publicly reachable
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Number of log entries kept in the in-memory ring buffer
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "Dashboard:LogBufferSize must be at least 1 (environment variable PLEXSYNC__Dashboard__LogBufferSize).")]
    public int LogBufferSize { get; set; } = 1000;

    #endregion // Properties
}