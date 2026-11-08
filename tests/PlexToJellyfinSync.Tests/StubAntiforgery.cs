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

    /// <inheritdoc/>
    public AntiforgeryTokenSet GetAndStoreTokens(HttpContext httpContext)
    {
        return new AntiforgeryTokenSet("request-token", "cookie-token", "__RequestVerificationToken", null);
    }

    /// <inheritdoc/>
    public AntiforgeryTokenSet GetTokens(HttpContext httpContext)
    {
        return GetAndStoreTokens(httpContext);
    }

    /// <inheritdoc/>
    public Task<bool> IsRequestValidAsync(HttpContext httpContext)
    {
        return Task.FromResult(_isValid);
    }

    /// <inheritdoc/>
    public void SetCookieTokenAndHeader(HttpContext httpContext)
    {
    }

    /// <inheritdoc/>
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