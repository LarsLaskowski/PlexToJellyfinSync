using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

using PlexToJellyfinSync.Core.Options;
using PlexToJellyfinSync.Security;

namespace PlexToJellyfinSync.Tests;

/// <summary>
/// Tests for <see cref="TokenAuthMiddleware"/>
/// </summary>
[TestClass]
public sealed class TokenAuthMiddlewareTests
{
    #region Methods

    /// <summary>
    /// A request passes through unconditionally when no token is configured
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task TokenAuthMiddlewareNoTokenConfiguredPassesThrough()
    {
        var nextCalled = false;

        using var cache = new MemoryCache(new MemoryCacheOptions());

        var middleware = CreateMiddleware(string.Empty,
                                          cache,
                                          _ =>
                                          {
                                              nextCalled = true;

                                              return Task.CompletedTask;
                                          });
        var context = CreateContext("/dashboard");

        await middleware.InvokeAsync(context);

        Assert.IsTrue(nextCalled, "A request should pass through when no token is configured!");
    }

    /// <summary>
    /// The Blazor Server hub path is exempt from authentication
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task TokenAuthMiddlewareBlazorHubPathPassesThrough()
    {
        var nextCalled = false;

        using var cache = new MemoryCache(new MemoryCacheOptions());

        var middleware = CreateMiddleware("secret",
                                          cache,
                                          _ =>
                                          {
                                              nextCalled = true;

                                              return Task.CompletedTask;
                                          });
        var context = CreateContext("/_blazor");

        await middleware.InvokeAsync(context);

        Assert.IsTrue(nextCalled, "The Blazor hub path should be exempt from authentication!");
    }

    /// <summary>
    /// An unrecognized underscore-prefixed path now requires authentication instead of matching the old blanket prefix
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task TokenAuthMiddlewareUnknownUnderscorePathRedirectsToLogin()
    {
        var nextCalled = false;

        using var cache = new MemoryCache(new MemoryCacheOptions());

        var middleware = CreateMiddleware("secret",
                                          cache,
                                          _ =>
                                          {
                                              nextCalled = true;

                                              return Task.CompletedTask;
                                          });
        var context = CreateContext("/_future-endpoint");

        await middleware.InvokeAsync(context);

        Assert.IsFalse(nextCalled, "An unrecognized underscore-prefixed path should require authentication!");
        Assert.AreEqual("/login", context.Response.Headers.Location.ToString(), "An unauthenticated request should redirect to the login page!");
    }

    /// <summary>
    /// A request for a known static asset extension is exempt from authentication
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task TokenAuthMiddlewareKnownStaticAssetPassesThrough()
    {
        var nextCalled = false;

        using var cache = new MemoryCache(new MemoryCacheOptions());

        var middleware = CreateMiddleware("secret",
                                          cache,
                                          _ =>
                                          {
                                              nextCalled = true;

                                              return Task.CompletedTask;
                                          });
        var context = CreateContext("/app.css");

        await middleware.InvokeAsync(context);

        Assert.IsTrue(nextCalled, "A known static asset extension should be exempt from authentication!");
    }

    /// <summary>
    /// A path whose last segment merely contains a dot but is not a known static asset extension now requires authentication
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task TokenAuthMiddlewareUnknownExtensionRedirectsToLogin()
    {
        var nextCalled = false;

        using var cache = new MemoryCache(new MemoryCacheOptions());

        var middleware = CreateMiddleware("secret",
                                          cache,
                                          _ =>
                                          {
                                              nextCalled = true;

                                              return Task.CompletedTask;
                                          });
        var context = CreateContext("/logs/export.csv");

        await middleware.InvokeAsync(context);

        Assert.IsFalse(nextCalled, "An unrecognized extension should require authentication!");
    }

    /// <summary>
    /// A request carrying a cookie that resolves to a live session passes through
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task TokenAuthMiddlewareValidSessionCookiePassesThrough()
    {
        var nextCalled = false;

        using var cache = new MemoryCache(new MemoryCacheOptions());

        cache.Set(TokenAuthMiddleware.SessionCachePrefix + "session-1", true);

        var middleware = CreateMiddleware("secret",
                                          cache,
                                          _ =>
                                          {
                                              nextCalled = true;

                                              return Task.CompletedTask;
                                          });
        var context = CreateContext("/dashboard");

        context.Request.Headers.Append("Cookie", $"{TokenAuthMiddleware.CookieName}=session-1");

        await middleware.InvokeAsync(context);

        Assert.IsTrue(nextCalled, "A request carrying a valid session cookie should pass through!");
    }

    #endregion // Methods

    #region Static methods

    /// <summary>
    /// Create a middleware instance backed by the given configured token, cache and next delegate
    /// </summary>
    /// <param name="token">Configured dashboard token</param>
    /// <param name="cache">Memory cache backing session validation</param>
    /// <param name="next">Delegate invoked when the request is allowed through</param>
    /// <returns>The prepared middleware instance</returns>
    private static TokenAuthMiddleware CreateMiddleware(string token, IMemoryCache cache, RequestDelegate next)
    {
        var options = Options.Create(new DashboardOptions
                                     {
                                         Token = token
                                     });

        return new TokenAuthMiddleware(next, options, cache);
    }

    /// <summary>
    /// Create an HTTP context for the given request path
    /// </summary>
    /// <param name="path">Request path</param>
    /// <returns>The prepared HTTP context</returns>
    private static DefaultHttpContext CreateContext(string path)
    {
        var context = new DefaultHttpContext();

        context.Request.Path = path;

        return context;
    }

    #endregion // Static methods
}