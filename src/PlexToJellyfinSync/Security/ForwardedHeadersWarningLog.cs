using System.Net;

using Microsoft.Extensions.Logging;

namespace PlexToJellyfinSync.Security;

/// <summary>
/// Source-generated log messages of <see cref="ForwardedHeadersWarning"/>
/// </summary>
internal static partial class ForwardedHeadersWarningLog
{
    #region Methods

    /// <summary>
    /// Log that forwarded headers of an untrusted sender were not applied
    /// </summary>
    /// <param name="logger">Logging interface</param>
    /// <param name="sender">Address of the sender</param>
    [LoggerMessage(EventId = 1, EventName = "ForwardedHeadersNotApplied", Level = LogLevel.Warning, Message = "Forwarded headers from {Sender} were not applied: the sender is not a trusted proxy or the header is malformed. List the reverse proxy in Dashboard:TrustedProxies (environment variable PLEXSYNC__Dashboard__TrustedProxies__0). This warning is logged once.")]
    public static partial void ForwardedHeadersNotApplied(ILogger logger, IPAddress? sender);

    #endregion // Methods
}