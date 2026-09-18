using System.Globalization;

using Microsoft.AspNetCore.Antiforgery;
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
    /// Handle a GET request for the login page, rendering the antiforgery field and any error message
    /// </summary>
    /// <param name="context">HTTP context</param>
    /// <param name="antiforgery">Antiforgery service issuing the request token</param>
    /// <returns>The rendered login page as an HTML content result</returns>
    public static IResult HandleGetLogin(HttpContext context, IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(context);
        var antiforgeryField = LoginPage.BuildAntiforgeryField(tokens.FormFieldName, tokens.RequestToken);
        var showError = LoginPage.ShouldShowError(context.Request.Query["error"]);

        return Results.Content(LoginPage.Render(antiforgeryField, showError), "text/html");
    }

    /// <summary>
    /// Handle a login form submission, applying throttling and issuing a session cookie on success
    /// </summary>
    /// <param name="context">HTTP context</param>
    /// <param name="cache">Memory cache holding valid session identifiers</param>
    /// <param name="loginService">Service that evaluates the login attempt</param>
    /// <param name="antiforgery">Antiforgery service validating the submitted request token</param>
    /// <returns>Task</returns>
    public static async Task HandleLoginAsync(HttpContext context, IMemoryCache cache, IDashboardLoginService loginService, IAntiforgery antiforgery)
    {
        // The form is read manually rather than through parameter binding, so ASP.NET Core cannot infer
        // that this endpoint needs antiforgery validation on its own; validate it explicitly instead.
        if (await IsAntiforgeryValidAsync(context, antiforgery).ConfigureAwait(false) == false)
        {
            await RejectAsync(context).ConfigureAwait(false);

            return;
        }

        var clientKey = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var form = await context.Request.ReadFormAsync(context.RequestAborted).ConfigureAwait(false);
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

            // Secure is intentionally conditional below (fixes GitHub issue #74 – a hardcoded Secure = true
            // silently drops the cookie over plain HTTP, making the token-protected dashboard unusable behind
            // the project's own documented HTTP quick start). Sonar's static S2092 check cannot see that this
            // is a deliberate, reviewed trade-off, so it is disabled for this cookie only.
#pragma warning disable S2092
            context.Response.Cookies.Append(TokenAuthMiddleware.CookieName,
                                            result.SessionId,
                                            new CookieOptions
                                            {
                                                HttpOnly = true,
                                                Secure = context.Request.IsHttps,
                                                SameSite = SameSiteMode.Strict,
                                                MaxAge = _sessionLifetime
                                            });
#pragma warning restore S2092
            context.Response.Redirect("/");

            return;
        }

        context.Response.Redirect("/login?error=1");
    }

    /// <summary>
    /// Handle a logout request, revoking the session and clearing the auth cookie
    /// </summary>
    /// <param name="context">HTTP context</param>
    /// <param name="cache">Memory cache holding valid session identifiers</param>
    /// <param name="antiforgery">Antiforgery service validating the submitted request token</param>
    /// <returns>Task</returns>
    public static async Task HandleLogout(HttpContext context, IMemoryCache cache, IAntiforgery antiforgery)
    {
        if (await IsAntiforgeryValidAsync(context, antiforgery).ConfigureAwait(false) == false)
        {
            await RejectAsync(context).ConfigureAwait(false);

            return;
        }

        if (context.Request.Cookies.TryGetValue(TokenAuthMiddleware.CookieName, out var sessionId))
        {
            cache.Remove(TokenAuthMiddleware.SessionCachePrefix + sessionId);
        }

        context.Response.Cookies.Delete(TokenAuthMiddleware.CookieName);
        context.Response.Redirect("/login");
    }

    /// <summary>
    /// Write a 400 response with a body, so <c>UseStatusCodePagesWithReExecute</c> does not
    /// re-execute the request (and have <see cref="TokenAuthMiddleware"/> turn it into a
    /// redirect to <c>/login</c>, masking the rejection) once a body has already been written
    /// </summary>
    /// <param name="context">HTTP context</param>
    /// <returns>Task</returns>
    private static async Task RejectAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;

        await context.Response.WriteAsync("Invalid or missing antiforgery token.", context.RequestAborted).ConfigureAwait(false);
    }

    /// <summary>
    /// Validate the antiforgery token carried by a request without throwing on failure
    /// </summary>
    /// <param name="context">HTTP context</param>
    /// <param name="antiforgery">Antiforgery service validating the submitted request token</param>
    /// <returns>True when the request carries a valid antiforgery token</returns>
    private static async Task<bool> IsAntiforgeryValidAsync(HttpContext context, IAntiforgery antiforgery)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context).ConfigureAwait(false);

            return true;
        }
        catch (AntiforgeryValidationException)
        {
            return false;
        }
    }

    #endregion // Static methods
}