using System.Net;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

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
    /// The forwarded-headers options read X-Forwarded-For and X-Forwarded-Proto only
    /// </summary>
    [TestMethod]
    public void DashboardStartupCreateForwardedHeadersOptionsReadsForwardedForAndProto()
    {
        var options = DashboardStartup.CreateForwardedHeadersOptions(new DashboardOptions());

        Assert.AreEqual(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto, options.ForwardedHeaders, "Only the client address and the scheme should be forwarded!");
        Assert.AreEqual(1, options.ForwardLimit, "One proxy hop should be processed!");
    }

    /// <summary>
    /// Forwarded headers from a sender that is not a trusted proxy are ignored
    /// </summary>
    [TestMethod]
    public void DashboardStartupForwardedHeadersFromUntrustedSenderAreIgnored()
    {
        var context = Apply(new DashboardOptions(), "203.0.113.7", "198.51.100.1", "https");

        Assert.AreEqual(IPAddress.Parse("203.0.113.7"), context.Connection.RemoteIpAddress, "The client address of an untrusted sender should stay the socket address!");
        Assert.IsFalse(context.Request.IsHttps, "The scheme of an untrusted sender should not be applied!");
    }

    /// <summary>
    /// Forwarded headers from loopback are applied without any configured proxy
    /// </summary>
    /// <param name="sender">Sender address</param>
    [TestMethod]
    [DataRow("127.0.0.1")]
    [DataRow("::1")]
    public void DashboardStartupForwardedHeadersFromLoopbackAreApplied(string sender)
    {
        var context = Apply(new DashboardOptions(), sender, "198.51.100.1", "https");

        Assert.AreEqual(IPAddress.Parse("198.51.100.1"), context.Connection.RemoteIpAddress, "The forwarded client address should be applied for loopback!");
        Assert.IsTrue(context.Request.IsHttps, "The forwarded scheme should be applied for loopback!");
    }

    /// <summary>
    /// Forwarded headers from a sender inside a trusted network are applied, the rightmost entry wins
    /// </summary>
    /// <param name="sender">Sender address</param>
    [TestMethod]
    [DataRow("172.18.0.5")]
    [DataRow("::ffff:172.18.0.5")]
    public void DashboardStartupForwardedHeadersFromTrustedNetworkAreApplied(string sender)
    {
        var options = new DashboardOptions
                      {
                          TrustedProxies = ["172.18.0.0/16"]
                      };

        var context = Apply(options, sender, "203.0.113.9, 198.51.100.1", "https");

        Assert.AreEqual(IPAddress.Parse("198.51.100.1"), context.Connection.RemoteIpAddress, "The rightmost forwarded address should be applied!");
        Assert.IsTrue(context.Request.IsHttps, "The forwarded scheme should be applied!");
    }

    /// <summary>
    /// Forwarded headers from a single trusted address are applied
    /// </summary>
    [TestMethod]
    public void DashboardStartupForwardedHeadersFromTrustedAddressAreApplied()
    {
        var options = new DashboardOptions
                      {
                          TrustedProxies = ["192.0.2.10"]
                      };

        var context = Apply(options, "192.0.2.10", "203.0.113.9, 198.51.100.1", "https");

        Assert.AreEqual(IPAddress.Parse("198.51.100.1"), context.Connection.RemoteIpAddress, "The rightmost forwarded address should be applied!");
        Assert.IsTrue(context.Request.IsHttps, "The forwarded scheme should be applied!");
    }

    /// <summary>
    /// Forwarded headers from a sender outside the trusted network are ignored
    /// </summary>
    [TestMethod]
    public void DashboardStartupForwardedHeadersFromOutsideTrustedNetworkAreIgnored()
    {
        var options = new DashboardOptions
                      {
                          TrustedProxies = ["172.18.0.0/16"]
                      };

        var context = Apply(options, "172.19.0.5", "198.51.100.1", "https");

        Assert.AreEqual(IPAddress.Parse("172.19.0.5"), context.Connection.RemoteIpAddress, "The client address should stay the socket address!");
        Assert.IsFalse(context.Request.IsHttps, "The scheme should not be applied!");
    }

    /// <summary>
    /// Forwarded headers from a sender inside a trusted IPv6 network are applied and from outside it ignored
    /// </summary>
    /// <param name="sender">Sender address</param>
    /// <param name="expectedAddress">Expected client address afterwards</param>
    /// <param name="applied">Whether the headers are expected to be applied</param>
    [TestMethod]
    [DataRow("2001:db8::5", "198.51.100.1", true)]
    [DataRow("2001:db9::5", "2001:db9::5", false)]
    public void DashboardStartupForwardedHeadersFromTrustedIPv6NetworkAreApplied(string sender, string expectedAddress, bool applied)
    {
        var options = new DashboardOptions
                      {
                          TrustedProxies = ["2001:db8::/32"]
                      };

        var context = Apply(options, sender, "198.51.100.1", "https");

        Assert.AreEqual(IPAddress.Parse(expectedAddress), context.Connection.RemoteIpAddress, "The client address should follow the trust decision for the IPv6 network!");
        Assert.AreEqual(applied, context.Request.IsHttps, "The scheme should follow the trust decision for the IPv6 network!");
    }

    /// <summary>
    /// An entry the parser rejects never widens the trust
    /// </summary>
    [TestMethod]
    public void DashboardStartupInvalidTrustedProxyIsNotTrusted()
    {
        var options = new DashboardOptions
                      {
                          TrustedProxies = ["10.0.0.0/33"]
                      };

        var forwardedOptions = DashboardStartup.CreateForwardedHeadersOptions(options);
        var context = Apply(options, "203.0.113.7", "198.51.100.1", "https");

        Assert.AreEqual(IPAddress.Parse("203.0.113.7"), context.Connection.RemoteIpAddress, "The client address should stay the socket address!");
        Assert.IsFalse(context.Request.IsHttps, "The scheme should not be applied!");
        Assert.HasCount(1, forwardedOptions.KnownIPNetworks, "Only the framework default network should be trusted!");
    }

    /// <summary>
    /// Run a request through the real forwarded-headers middleware with the options built from the dashboard options
    /// </summary>
    /// <param name="options">Dashboard options</param>
    /// <param name="sender">Socket address of the sender</param>
    /// <param name="forwardedFor">Value of the X-Forwarded-For header</param>
    /// <param name="forwardedProto">Value of the X-Forwarded-Proto header</param>
    /// <returns>The context after the middleware</returns>
    private static DefaultHttpContext Apply(DashboardOptions options, string sender, string forwardedFor, string forwardedProto)
    {
        var middleware = new ForwardedHeadersMiddleware(_ => Task.CompletedTask, NullLoggerFactory.Instance, Options.Create(DashboardStartup.CreateForwardedHeadersOptions(options)));
        var context = new DefaultHttpContext();

        context.Request.Scheme = "http";
        context.Connection.RemoteIpAddress = IPAddress.Parse(sender);
        context.Request.Headers["X-Forwarded-For"] = forwardedFor;
        context.Request.Headers["X-Forwarded-Proto"] = forwardedProto;

        middleware.ApplyForwarders(context);

        return context;
    }

    #endregion // Methods
}