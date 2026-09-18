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
    /// <returns>Forwarded-headers options with no proxy network known upfront</returns>
    public static ForwardedHeadersOptions CreateForwardedHeadersOptions()
    {
        var options = new ForwardedHeadersOptions
                      {
                          ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
                      };

        // No reverse proxy address is known upfront in this single-container deployment; trust
        // whatever proxy the operator places in front (see README for the reverse-proxy/TLS guidance).
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();

        return options;
    }

    #endregion // Static methods
}