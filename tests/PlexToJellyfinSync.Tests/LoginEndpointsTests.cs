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

        await LoginEndpoints.HandleLoginAsync(context, cache, service, new StubAntiforgery());

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

        await LoginEndpoints.HandleLoginAsync(context, cache, service, new StubAntiforgery());

        Assert.AreEqual(StatusCodes.Status302Found, context.Response.StatusCode, "A successful login should redirect!");
        Assert.AreEqual("/", context.Response.Headers.Location.ToString(), "A successful login should redirect to the dashboard!");
        Assert.IsTrue(cache.TryGetValue(TokenAuthMiddleware.SessionCachePrefix + "session-123", out _), "A successful login should store the session!");
        Assert.IsTrue(context.Response.Headers.SetCookie.ToString().Contains(TokenAuthMiddleware.CookieName, StringComparison.Ordinal), "A successful login should set the auth cookie!");
    }

    /// <summary>
    /// A successful login over plain HTTP does not mark the cookie Secure, since browsers would otherwise drop it
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task LoginEndpointsSucceededOverHttpOmitsSecureCookieFlag()
    {
        var context = CreateContext("s3cr3t");
        var service = new StubDashboardLoginService(LoginResult.Succeeded("session-123"));

        using var cache = new MemoryCache(new MemoryCacheOptions());

        await LoginEndpoints.HandleLoginAsync(context, cache, service, new StubAntiforgery());

        Assert.IsFalse(context.Response.Headers.SetCookie.ToString().Contains("secure", StringComparison.OrdinalIgnoreCase), "A login served over plain HTTP should not mark the cookie Secure!");
    }

    /// <summary>
    /// A successful login over HTTPS marks the cookie Secure
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task LoginEndpointsSucceededOverHttpsSetsSecureCookieFlag()
    {
        var context = CreateContext("s3cr3t");

        context.Request.Scheme = "https";

        var service = new StubDashboardLoginService(LoginResult.Succeeded("session-123"));

        using var cache = new MemoryCache(new MemoryCacheOptions());

        await LoginEndpoints.HandleLoginAsync(context, cache, service, new StubAntiforgery());

        Assert.IsTrue(context.Response.Headers.SetCookie.ToString().Contains("secure", StringComparison.OrdinalIgnoreCase), "A login served over HTTPS should mark the cookie Secure!");
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

        await LoginEndpoints.HandleLoginAsync(context, cache, service, new StubAntiforgery());

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

        await LoginEndpoints.HandleLoginAsync(context, cache, service, new StubAntiforgery());

        Assert.AreEqual(StatusCodes.Status302Found, context.Response.StatusCode, "A missing remote address should not break the login flow!");
    }

    /// <summary>
    /// A login request carrying an invalid antiforgery token is rejected before the token is even evaluated
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task LoginEndpointsInvalidAntiforgeryReturnsBadRequest()
    {
        var context = CreateContext("s3cr3t");
        var service = new StubDashboardLoginService(LoginResult.Succeeded("session-123"));

        using var cache = new MemoryCache(new MemoryCacheOptions());

        await LoginEndpoints.HandleLoginAsync(context, cache, service, new StubAntiforgery(isValid: false));

        Assert.AreEqual(StatusCodes.Status400BadRequest, context.Response.StatusCode, "A request without a valid antiforgery token should be rejected!");
        Assert.IsFalse(cache.TryGetValue(TokenAuthMiddleware.SessionCachePrefix + "session-123", out _), "A rejected request should never evaluate the login attempt!");
    }

    /// <summary>
    /// A logout request revokes the session and clears the auth cookie
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task LoginEndpointsLogoutRevokesSessionAndClearsCookie()
    {
        var context = new DefaultHttpContext();

        context.Request.Headers.Append("Cookie", $"{TokenAuthMiddleware.CookieName}=session-123");

        using var cache = new MemoryCache(new MemoryCacheOptions());

        cache.Set(TokenAuthMiddleware.SessionCachePrefix + "session-123", true);

        await LoginEndpoints.HandleLogout(context, cache, new StubAntiforgery());

        Assert.IsFalse(cache.TryGetValue(TokenAuthMiddleware.SessionCachePrefix + "session-123", out _), "A logout should revoke the stored session!");
        Assert.AreEqual("/login", context.Response.Headers.Location.ToString(), "A logout should redirect to the login page!");
        Assert.IsTrue(context.Response.Headers.SetCookie.ToString().Contains(TokenAuthMiddleware.CookieName, StringComparison.Ordinal), "A logout should clear the auth cookie!");
    }

    /// <summary>
    /// A logout request without an existing session cookie still redirects without error
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task LoginEndpointsLogoutWithoutSessionCookieStillRedirects()
    {
        var context = new DefaultHttpContext();

        using var cache = new MemoryCache(new MemoryCacheOptions());

        await LoginEndpoints.HandleLogout(context, cache, new StubAntiforgery());

        Assert.AreEqual("/login", context.Response.Headers.Location.ToString(), "A logout without an existing session should still redirect to the login page!");
    }

    /// <summary>
    /// A logout request carrying an invalid antiforgery token is rejected and never touches the session
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task LoginEndpointsLogoutInvalidAntiforgeryReturnsBadRequest()
    {
        var context = new DefaultHttpContext();

        context.Request.Headers.Append("Cookie", $"{TokenAuthMiddleware.CookieName}=session-123");

        using var cache = new MemoryCache(new MemoryCacheOptions());

        cache.Set(TokenAuthMiddleware.SessionCachePrefix + "session-123", true);

        await LoginEndpoints.HandleLogout(context, cache, new StubAntiforgery(isValid: false));

        Assert.AreEqual(StatusCodes.Status400BadRequest, context.Response.StatusCode, "A logout without a valid antiforgery token should be rejected!");
        Assert.IsTrue(cache.TryGetValue(TokenAuthMiddleware.SessionCachePrefix + "session-123", out _), "A rejected logout should never revoke the session!");
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