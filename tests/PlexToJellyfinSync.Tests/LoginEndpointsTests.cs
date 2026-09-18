using System.Net;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
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
    /// The GET login handler renders the antiforgery field and omits the error message for a plain request
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task LoginEndpointsHandleGetLoginRendersPageWithoutError()
    {
        var context = new DefaultHttpContext
                      {
                          RequestServices = CreateRequestServices()
                      };

        using var body = new MemoryStream();

        context.Response.Body = body;

        var result = LoginEndpoints.HandleGetLogin(context, new StubAntiforgery());

        await result.ExecuteAsync(context);

        var html = ReadBody(body);

        Assert.IsTrue(html.Contains("__RequestVerificationToken", StringComparison.Ordinal), "The rendered login page should embed the antiforgery field!");
        Assert.IsFalse(html.Contains("Invalid access token", StringComparison.Ordinal), "A plain request should not show the error message!");
    }

    /// <summary>
    /// The GET login handler shows the error message when the query carries ?error=1
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task LoginEndpointsHandleGetLoginWithErrorQueryShowsErrorMessage()
    {
        var context = new DefaultHttpContext
                      {
                          RequestServices = CreateRequestServices()
                      };

        using var body = new MemoryStream();

        context.Request.QueryString = new QueryString("?error=1");
        context.Response.Body = body;

        var result = LoginEndpoints.HandleGetLogin(context, new StubAntiforgery());

        await result.ExecuteAsync(context);

        var html = ReadBody(body);

        Assert.IsTrue(html.Contains("Invalid access token", StringComparison.Ordinal), "A request with ?error=1 should show the error message!");
    }

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
    /// Create a minimal service provider satisfying what <c>ContentHttpResult.ExecuteAsync</c> resolves from <see cref="HttpContext.RequestServices"/>
    /// </summary>
    /// <returns>A service provider carrying logging services</returns>
    private static ServiceProvider CreateRequestServices()
    {
        return new ServiceCollection().AddLogging()
                                      .BuildServiceProvider();
    }

    /// <summary>
    /// Read the full text content written to a response body stream
    /// </summary>
    /// <param name="body">Response body stream, already written to</param>
    /// <returns>The text content written to the stream</returns>
    private static string ReadBody(MemoryStream body)
    {
        body.Seek(0, SeekOrigin.Begin);

        using var reader = new StreamReader(body, leaveOpen: true);

        return reader.ReadToEnd();
    }

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