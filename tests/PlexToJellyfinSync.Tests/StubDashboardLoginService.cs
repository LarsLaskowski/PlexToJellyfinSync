using PlexToJellyfinSync.Core.Abstractions;
using PlexToJellyfinSync.Core.Models;

namespace PlexToJellyfinSync.Tests;

/// <summary>
/// Login service stub that returns a preconfigured result
/// </summary>
internal sealed class StubDashboardLoginService : IDashboardLoginService
{
    #region Fields

    private readonly LoginResult _result;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="result">Result to return from every call</param>
    public StubDashboardLoginService(LoginResult result)
    {
        _result = result;
    }

    #endregion // Constructors

    #region IDashboardLoginService

    /// <inheritdoc/>
    public LoginResult Authenticate(string clientKey, string token)
    {
        return _result;
    }

    #endregion // IDashboardLoginService
}