using System.Net;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using PlexToJellyfinSync.Core.Options;
using PlexToJellyfinSync.Security;

namespace PlexToJellyfinSync.Tests;

/// <summary>
/// Tests for <see cref="ForwardedHeadersWarning"/>
/// </summary>
[TestClass]
public sealed class ForwardedHeadersWarningTests
{
    #region Methods

    /// <summary>
    /// Untrusted forwarded headers log exactly one warning per instance naming only the socket address
    /// </summary>
    [TestMethod]
    public void ForwardedHeadersWarningUntrustedForwardedForLogsOnce()
    {
        var logger = new RecordingForwardedHeadersWarningLogger();
        var warning = new ForwardedHeadersWarning(logger);
        var first = Process("203.0.113.7", "198.51.100.1", null);
        var second = Process("203.0.113.8", "198.51.100.2", null);

        warning.Inspect(first);
        warning.Inspect(second);

        var entries = logger.GetEntries();

        Assert.HasCount(1, entries, "The warning should be logged once per instance!");
        Assert.AreEqual(LogLevel.Warning, entries[0].Level, "The entry should be a warning!");
        Assert.AreEqual("Forwarded headers from 203.0.113.7 were not applied: the sender is not a trusted proxy or the header is malformed. List the reverse proxy in Dashboard:TrustedProxies (environment variable PLEXSYNC__Dashboard__TrustedProxies__0). This warning is logged once.",
                        entries[0].Message,
                        "The warning should name the socket address and the setting to use!");
    }

    /// <summary>
    /// An untrusted X-Forwarded-Proto alone logs the warning
    /// </summary>
    [TestMethod]
    public void ForwardedHeadersWarningUntrustedForwardedProtoLogs()
    {
        var logger = new RecordingForwardedHeadersWarningLogger();
        var warning = new ForwardedHeadersWarning(logger);

        warning.Inspect(Process("203.0.113.7", null, "https"));

        Assert.HasCount(1, logger.GetEntries(), "An ignored X-Forwarded-Proto should log the warning!");
    }

    /// <summary>
    /// Headers applied for a trusted sender log nothing
    /// </summary>
    [TestMethod]
    public void ForwardedHeadersWarningAppliedHeadersLogNothing()
    {
        var logger = new RecordingForwardedHeadersWarningLogger();
        var warning = new ForwardedHeadersWarning(logger);

        warning.Inspect(Process("127.0.0.1", "198.51.100.1", "https"));

        Assert.IsEmpty(logger.GetEntries(), "Applied forwarded headers should not log!");
    }

    /// <summary>
    /// A request without forwarded headers logs nothing
    /// </summary>
    [TestMethod]
    public void ForwardedHeadersWarningWithoutForwardedHeadersLogsNothing()
    {
        var logger = new RecordingForwardedHeadersWarningLogger();
        var warning = new ForwardedHeadersWarning(logger);

        warning.Inspect(Process("203.0.113.7", null, null));

        Assert.IsEmpty(logger.GetEntries(), "A request without forwarded headers should not log!");
    }

    /// <summary>
    /// Run a request through the real forwarded-headers middleware with the default dashboard options
    /// </summary>
    /// <param name="sender">Socket address of the sender</param>
    /// <param name="forwardedFor">Value of the X-Forwarded-For header or null</param>
    /// <param name="forwardedProto">Value of the X-Forwarded-Proto header or null</param>
    /// <returns>The context after the middleware</returns>
    private static DefaultHttpContext Process(string sender, string? forwardedFor, string? forwardedProto)
    {
        var middleware = new ForwardedHeadersMiddleware(_ => Task.CompletedTask, NullLoggerFactory.Instance, Options.Create(DashboardStartup.CreateForwardedHeadersOptions(new DashboardOptions())));
        var context = new DefaultHttpContext();

        context.Request.Scheme = "http";
        context.Connection.RemoteIpAddress = IPAddress.Parse(sender);

        if (forwardedFor is not null)
        {
            context.Request.Headers["X-Forwarded-For"] = forwardedFor;
        }

        if (forwardedProto is not null)
        {
            context.Request.Headers["X-Forwarded-Proto"] = forwardedProto;
        }

        middleware.ApplyForwarders(context);

        return context;
    }

    #endregion // Methods
}