namespace PlexToJellyfinSync.Core.Abstractions;

/// <summary>
/// Tracks failed dashboard login attempts per client and applies a backoff lockout
/// </summary>
public interface ILoginThrottle
{
    #region Methods

    /// <summary>
    /// Determine whether a client is currently locked out from attempting a login
    /// </summary>
    /// <param name="clientKey">Identifier for the client, e.g. its remote IP address</param>
    /// <param name="retryAfter">When locked out, the remaining time until the next attempt is allowed</param>
    /// <returns>True when the client must wait before trying again</returns>
    bool IsLockedOut(string clientKey, out TimeSpan retryAfter);

    /// <summary>
    /// Record a failed login attempt for a client and extend its backoff lockout
    /// </summary>
    /// <param name="clientKey">Identifier for the client, e.g. its remote IP address</param>
    void RegisterFailure(string clientKey);

    /// <summary>
    /// Clear the recorded failures for a client after a successful login
    /// </summary>
    /// <param name="clientKey">Identifier for the client, e.g. its remote IP address</param>
    void RegisterSuccess(string clientKey);

    #endregion // Methods
}