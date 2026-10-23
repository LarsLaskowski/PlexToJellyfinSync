using Microsoft.AspNetCore.HttpOverrides;

using PlexToJellyfinSync.Core.Options;

namespace PlexToJellyfinSync.Security;

/// <summary>
/// Pure startup decisions for the dashboard, kept separate from <c>Program.cs</c> so they can be unit tested in isolation
/// </summary>
public static class DashboardStartup
{
    #region Static methods

    /// <summary>
    /// Determine whether a startup warning should be logged because the dashboard is reachable without a configured access token
    /// </summary>
    /// <param name="options">Dashboard options</param>
    /// <returns>True when the dashboard is enabled with no access token configured</returns>
    public static bool ShouldWarnAboutMissingToken(DashboardOptions options)
    {
        return options.Enabled && string.IsNullOrWhiteSpace(options.Token);
    }

    /// <summary>
    /// Build the forwarded-headers options used so a TLS-terminating reverse proxy's scheme is honored
    /// </summary>
    /// <param name="options">Dashboard options holding the trusted proxies</param>
    /// <returns>Forwarded-headers options trusting loopback and the configured proxies</returns>
    public static ForwardedHeadersOptions CreateForwardedHeadersOptions(DashboardOptions options)
    {
        var forwardedOptions = new ForwardedHeadersOptions
                               {
                                   ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
                                   ForwardLimit = 1
                               };

        foreach (var entry in options.TrustedProxies)
        {
            // An entry the parser rejects is skipped, never treated as "trust all"
            if (TrustedProxyParser.TryParse(entry, out var network))
            {
                forwardedOptions.KnownIPNetworks.Add(network);
            }
        }

        return forwardedOptions;
    }

    #endregion // Static methods
}