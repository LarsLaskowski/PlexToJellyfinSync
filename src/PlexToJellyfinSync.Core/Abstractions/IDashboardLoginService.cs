using PlexToJellyfinSync.Core.Models;

namespace PlexToJellyfinSync.Core.Abstractions;

/// <summary>
/// Authenticates dashboard login attempts and coordinates throttling and session creation
/// </summary>
public interface IDashboardLoginService
{
    #region Methods

    /// <summary>
    /// Evaluate a login attempt for a client and the supplied token
    /// </summary>
    /// <param name="clientKey">Identifier for the client, e.g. its remote IP address</param>
    /// <param name="token">Token supplied by the client</param>
    /// <returns>The outcome of the attempt</returns>
    LoginResult Authenticate(string clientKey, string token);

    #endregion // Methods
}