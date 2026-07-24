using System.Net;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

using PlexToJellyfinSync.Core.Models;
using PlexToJellyfinSync.Security;

namespace PlexToJellyfinSync.Tests;

/// <summary>
/// Tests for <see cref="LoginEndpoints"/>
/// </summary>
[TestClass]
public sealed class LoginEndpointsTests
{
    #region Methods

    /// <summary>
    /// A locked-out result yields a 429 response carrying a Retry-After header
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task LoginEndpointsLockedOutReturnsTooManyRequests()
    {
        var context = CreateContext("whatever");
        var service = new StubDashboardLoginService(LoginResult.LockedOut(TimeSpan.FromSeconds(4)));

        using var cache = new MemoryCache(new MemoryCacheOptions());

        await LoginEndpoints.HandleLoginAsync(context, cache, service);

        Assert.AreEqual(StatusCodes.Status429TooManyRequests, context.Response.StatusCode, "A locked-out client should receive 429!");
        Assert.AreEqual("4", context.Response.Headers.RetryAfter.ToString(), "The Retry-After header should reflect the remaining lockout!");
    }

    /// <summary>
    /// A successful result stores a session, sets the cookie and redirects to the dashboard
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task LoginEndpointsSucceededSetsSessionAndRedirects()
    {
        var context = CreateContext("s3cr3t");
        var service = new StubDashboardLoginService(LoginResult.Succeeded("session-123"));

        using var cache = new MemoryCache(new MemoryCacheOptions());

        await LoginEndpoints.HandleLoginAsync(context, cache, service);

        Assert.AreEqual(StatusCodes.Status302Found, context.Response.StatusCode, "A successful login should redirect!");
        Assert.AreEqual("/", context.Response.Headers.Location.ToString(), "A successful login should redirect to the dashboard!");
        Assert.IsTrue(cache.TryGetValue(TokenAuthMiddleware.SessionCachePrefix + "session-123", out _), "A successful login should store the session!");
        Assert.IsTrue(context.Response.Headers.SetCookie.ToString().Contains(TokenAuthMiddleware.CookieName, StringComparison.Ordinal), "A successful login should set the auth cookie!");
    }

    /// <summary>
    /// A failed result redirects back to the login page with an error flag
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task LoginEndpointsFailedRedirectsToLogin()
    {
        var context = CreateContext("wrong");
        var service = new StubDashboardLoginService(LoginResult.Failed());

        using var cache = new MemoryCache(new MemoryCacheOptions());

        await LoginEndpoints.HandleLoginAsync(context, cache, service);

        Assert.AreEqual(StatusCodes.Status302Found, context.Response.StatusCode, "A failed login should redirect!");
        Assert.AreEqual("/login?error=1", context.Response.Headers.Location.ToString(), "A failed login should redirect to the login page with an error flag!");
    }

    /// <summary>
    /// A request without a remote address still resolves and redirects on failure
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task LoginEndpointsWithoutRemoteAddressStillRedirects()
    {
        var context = CreateContext("wrong");

        context.Connection.RemoteIpAddress = null;

        var service = new StubDashboardLoginService(LoginResult.Failed());

        using var cache = new MemoryCache(new MemoryCacheOptions());

        await LoginEndpoints.HandleLoginAsync(context, cache, service);

        Assert.AreEqual(StatusCodes.Status302Found, context.Response.StatusCode, "A missing remote address should not break the login flow!");
    }

    #endregion // Methods

    #region Static methods

    /// <summary>
    /// Create an HTTP context carrying the given token in its form body
    /// </summary>
    /// <param name="token">Token to place in the form</param>
    /// <returns>The prepared HTTP context</returns>
    private static DefaultHttpContext CreateContext(string token)
    {
        var context = new DefaultHttpContext();

        context.Connection.RemoteIpAddress = IPAddress.Loopback;
        context.Request.Form = new FormCollection(new Dictionary<string, StringValues>
                                                  {
                                                      ["token"] = token
                                                  });

        return context;
    }

    #endregion // Static methods
}