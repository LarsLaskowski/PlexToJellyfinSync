using System.Security.Cryptography;

using Microsoft.Extensions.Options;

using PlexToJellyfinSync.Core.Abstractions;
using PlexToJellyfinSync.Core.Models;
using PlexToJellyfinSync.Core.Options;

namespace PlexToJellyfinSync.Service.Security;

/// <summary>
/// Authenticates dashboard logins using a timing-safe token comparison and a per-client throttle
/// </summary>
public sealed class DashboardLoginService : IDashboardLoginService
{
    #region Constants

    /// <summary>
    /// Number of random bytes used to build a session identifier
    /// </summary>
    private const int SessionIdByteLength = 32;

    #endregion // Constants

    #region Fields

    private readonly ILoginThrottle _throttle;
    private readonly DashboardOptions _options;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="throttle">Throttle guarding against repeated failures</param>
    /// <param name="options">Dashboard options holding the configured token</param>
    public DashboardLoginService(ILoginThrottle throttle, IOptions<DashboardOptions> options)
    {
        _throttle = throttle;
        _options = options.Value;
    }

    #endregion // Constructors

    #region IDashboardLoginService

    /// <summary>
    /// Evaluate a login attempt for a client and the supplied token
    /// </summary>
    /// <param name="clientKey">Identifier for the client, e.g. its remote IP address</param>
    /// <param name="token">Token supplied by the client</param>
    /// <returns>The outcome of the attempt</returns>
    public LoginResult Authenticate(string clientKey, string token)
    {
        if (_throttle.IsLockedOut(clientKey, out var retryAfter))
        {
            return LoginResult.LockedOut(retryAfter);
        }

        if (TokenComparer.FixedTimeEquals(token, _options.Token))
        {
            _throttle.RegisterSuccess(clientKey);

            var sessionId = Convert.ToBase64String(RandomNumberGenerator.GetBytes(SessionIdByteLength));

            return LoginResult.Succeeded(sessionId);
        }

        _throttle.RegisterFailure(clientKey);

        return LoginResult.Failed();
    }

    #endregion // IDashboardLoginService
}