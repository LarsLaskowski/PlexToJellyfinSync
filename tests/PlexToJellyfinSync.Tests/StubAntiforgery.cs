using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;

namespace PlexToJellyfinSync.Tests;

/// <summary>
/// Antiforgery stub that validates every request as either always valid or always invalid
/// </summary>
internal sealed class StubAntiforgery : IAntiforgery
{
    #region Fields

    private readonly bool _isValid;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="isValid">Whether <see cref="ValidateRequestAsync"/> should succeed</param>
    public StubAntiforgery(bool isValid = true)
    {
        _isValid = isValid;
    }

    #endregion // Constructors

    #region IAntiforgery

    /// <summary>
    /// Return a fixed, unused token set
    /// </summary>
    /// <param name="httpContext">Ignored HTTP context</param>
    /// <returns>A fixed token set</returns>
    public AntiforgeryTokenSet GetAndStoreTokens(HttpContext httpContext)
    {
        return new AntiforgeryTokenSet("request-token", "cookie-token", "__RequestVerificationToken", null);
    }

    /// <summary>
    /// Return a fixed, unused token set
    /// </summary>
    /// <param name="httpContext">Ignored HTTP context</param>
    /// <returns>A fixed token set</returns>
    public AntiforgeryTokenSet GetTokens(HttpContext httpContext)
    {
        return GetAndStoreTokens(httpContext);
    }

    /// <summary>
    /// Return the preconfigured validity without inspecting the request
    /// </summary>
    /// <param name="httpContext">Ignored HTTP context</param>
    /// <returns>The preconfigured validity</returns>
    public Task<bool> IsRequestValidAsync(HttpContext httpContext)
    {
        return Task.FromResult(_isValid);
    }

    /// <summary>
    /// No-op; no cookie or header is set by this stub
    /// </summary>
    /// <param name="httpContext">Ignored HTTP context</param>
    public void SetCookieTokenAndHeader(HttpContext httpContext)
    {
    }

    /// <summary>
    /// Succeed or throw depending on the preconfigured validity, without inspecting the request
    /// </summary>
    /// <param name="httpContext">Ignored HTTP context</param>
    /// <returns>Task</returns>
    public Task ValidateRequestAsync(HttpContext httpContext)
    {
        if (_isValid == false)
        {
            throw new AntiforgeryValidationException("Stubbed antiforgery validation failure.");
        }

        return Task.CompletedTask;
    }

    #endregion // IAntiforgery
}