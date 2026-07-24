using System.Globalization;

using Microsoft.Extensions.Caching.Memory;

using PlexToJellyfinSync.Core.Abstractions;
using PlexToJellyfinSync.Core.Enums;

namespace PlexToJellyfinSync.Security;

/// <summary>
/// Request handlers for the dashboard login endpoints
/// </summary>
public static class LoginEndpoints
{
    #region Constants

    /// <summary>
    /// Lifetime of an authenticated dashboard session
    /// </summary>
    private static readonly TimeSpan _sessionLifetime = TimeSpan.FromHours(8);

    #endregion // Constants

    #region Static methods

    /// <summary>
    /// Handle a login form submission, applying throttling and issuing a session cookie on success
    /// </summary>
    /// <param name="context">HTTP context</param>
    /// <param name="cache">Memory cache holding valid session identifiers</param>
    /// <param name="loginService">Service that evaluates the login attempt</param>
    /// <returns>Task</returns>
    public static async Task HandleLoginAsync(HttpContext context, IMemoryCache cache, IDashboardLoginService loginService)
    {
        var clientKey = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var form = await context.Request.ReadFormAsync().ConfigureAwait(false);
        var token = form["token"].ToString();

        var result = loginService.Authenticate(clientKey, token);

        if (result.Kind == LoginResultKind.LockedOut)
        {
            context.Response.Headers.RetryAfter = ((int)Math.Ceiling(result.RetryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;

            return;
        }

        if (result.Kind == LoginResultKind.Succeeded)
        {
            cache.Set(TokenAuthMiddleware.SessionCachePrefix + result.SessionId, true, _sessionLifetime);

            context.Response.Cookies.Append(TokenAuthMiddleware.CookieName,
                                            result.SessionId,
                                            new CookieOptions
                                            {
                                                HttpOnly = true,
                                                Secure = true,
                                                SameSite = SameSiteMode.Strict,
                                                MaxAge = _sessionLifetime
                                            });
            context.Response.Redirect("/");

            return;
        }

        context.Response.Redirect("/login?error=1");
    }

    #endregion // Static methods
}