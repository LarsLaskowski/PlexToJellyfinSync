using Microsoft.Extensions.Logging;

namespace PlexToJellyfinSync;

/// <summary>
/// Source-generated log messages of <see cref="Program"/>
/// </summary>
internal static partial class ProgramLog
{
    #region Methods

    /// <summary>
    /// Log that the dashboard is enabled without an access token
    /// </summary>
    /// <param name="logger">Logging interface</param>
    [LoggerMessage(EventId = 1, EventName = "DashboardWithoutToken", Level = LogLevel.Warning, Message = "Dashboard is enabled with no access token configured (Dashboard:Token); it is reachable by anyone who can reach this host. Set Dashboard:Token to require authentication.")]
    public static partial void DashboardWithoutToken(ILogger logger);

    #endregion // Methods
}