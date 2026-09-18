using Microsoft.AspNetCore.HttpOverrides;

using PlexToJellyfinSync.Core.Options;
using PlexToJellyfinSync.Security;

namespace PlexToJellyfinSync.Tests;

/// <summary>
/// Tests for <see cref="DashboardStartup"/>
/// </summary>
[TestClass]
public sealed class DashboardStartupTests
{
    #region Methods

    /// <summary>
    /// An enabled dashboard with no token configured should warn
    /// </summary>
    [TestMethod]
    public void DashboardStartupShouldWarnAboutMissingTokenEnabledWithEmptyTokenReturnsTrue()
    {
        var options = new DashboardOptions
                      {
                          Enabled = true,
                          Token = string.Empty
                      };

        var result = DashboardStartup.ShouldWarnAboutMissingToken(options);

        Assert.IsTrue(result, "An enabled dashboard with no configured token should warn!");
    }

    /// <summary>
    /// An enabled dashboard with a configured token should not warn
    /// </summary>
    [TestMethod]
    public void DashboardStartupShouldWarnAboutMissingTokenEnabledWithTokenReturnsFalse()
    {
        var options = new DashboardOptions
                      {
                          Enabled = true,
                          Token = "secret"
                      };

        var result = DashboardStartup.ShouldWarnAboutMissingToken(options);

        Assert.IsFalse(result, "An enabled dashboard with a configured token should not warn!");
    }

    /// <summary>
    /// A disabled dashboard should never warn, regardless of the configured token
    /// </summary>
    [TestMethod]
    public void DashboardStartupShouldWarnAboutMissingTokenDisabledReturnsFalse()
    {
        var options = new DashboardOptions
                      {
                          Enabled = false,
                          Token = string.Empty
                      };

        var result = DashboardStartup.ShouldWarnAboutMissingToken(options);

        Assert.IsFalse(result, "A disabled dashboard has no surface to secure and should never warn!");
    }

    /// <summary>
    /// The forwarded-headers options honor X-Forwarded-For and X-Forwarded-Proto with no proxy network preconfigured
    /// </summary>
    [TestMethod]
    public void DashboardStartupCreateForwardedHeadersOptionsHonorsForwardedForAndProto()
    {
        var options = DashboardStartup.CreateForwardedHeadersOptions();

        Assert.IsTrue(options.ForwardedHeaders.HasFlag(ForwardedHeaders.XForwardedFor), "X-Forwarded-For should be honored!");
        Assert.IsTrue(options.ForwardedHeaders.HasFlag(ForwardedHeaders.XForwardedProto), "X-Forwarded-Proto should be honored so Request.IsHttps reflects the proxy!");
        Assert.IsEmpty(options.KnownIPNetworks, "No reverse proxy network is known upfront in this single-container deployment!");
        Assert.IsEmpty(options.KnownProxies, "No reverse proxy address is known upfront in this single-container deployment!");
    }

    #endregion // Methods
}