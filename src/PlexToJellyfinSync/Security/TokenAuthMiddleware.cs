using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

using PlexToJellyfinSync.Core.Options;

namespace PlexToJellyfinSync.Security;

/// <summary>
/// Optional middleware that protects the dashboard with a configured access token
/// </summary>
public sealed class TokenAuthMiddleware
{
    #region Constants

    /// <summary>
    /// Name of the authentication cookie
    /// </summary>
    public const string CookieName = "pjf_auth";

    /// <summary>
    /// Cache key prefix for session identifiers
    /// </summary>
    public const string SessionCachePrefix = "pjf_session:";

    #endregion // Constants

    #region Fields

    private readonly RequestDelegate _next;
    private readonly DashboardOptions _options;
    private readonly IMemoryCache _cache;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="next">Next middleware in the pipeline</param>
    /// <param name="options">Dashboard options</param>
    /// <param name="cache">Memory cache for server-side session validation</param>
    public TokenAuthMiddleware(RequestDelegate next, IOptions<DashboardOptions> options, IMemoryCache cache)
    {
        _next = next;
        _options = options.Value;
        _cache = cache;
    }

    #endregion // Constructors

    #region Static methods

    /// <summary>
    /// Determine whether a path is always accessible without authentication
    /// </summary>
    /// <param name="path">Request path</param>
    /// <returns>True if the path is allowed</returns>
    private static bool IsAllowedPath(string path)
    {
        if (string.Equals(path, "/health", StringComparison.OrdinalIgnoreCase)
            || string.Equals(path, "/login", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (path.StartsWith("/_", StringComparison.Ordinal))
        {
            return true;
        }

        var lastSegment = path.AsSpan(path.LastIndexOf('/') + 1);

        return lastSegment.Contains('.');
    }

    #endregion // Static methods

    #region Methods

    /// <summary>
    /// Process an HTTP request
    /// </summary>
    /// <param name="context">HTTP context</param>
    /// <returns>Task</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        if (string.IsNullOrWhiteSpace(_options.Token))
        {
            await _next(context).ConfigureAwait(false);

            return;
        }

        var path = context.Request.Path.Value ?? "/";

        if (IsAllowedPath(path))
        {
            await _next(context).ConfigureAwait(false);

            return;
        }

        if (context.Request.Cookies.TryGetValue(CookieName, out var sessionId)
            && sessionId is not null
            && _cache.TryGetValue(SessionCachePrefix + sessionId, out _))
        {
            await _next(context).ConfigureAwait(false);

            return;
        }

        context.Response.Redirect("/login");
    }

    #endregion // Methods
}