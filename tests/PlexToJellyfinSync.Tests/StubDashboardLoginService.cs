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

    /// <summary>
    /// Return the preconfigured result
    /// </summary>
    /// <param name="clientKey">Ignored client identifier</param>
    /// <param name="token">Ignored token</param>
    /// <returns>The preconfigured result</returns>
    public LoginResult Authenticate(string clientKey, string token)
    {
        return _result;
    }

    #endregion // IDashboardLoginService
}