using PlexToJellyfinSync.Core.Enums;

namespace PlexToJellyfinSync.Core.Models;

/// <summary>
/// Result of a dashboard login attempt produced by the login service
/// </summary>
public sealed class LoginResult
{
    #region Properties

    /// <summary>
    /// Kind of outcome
    /// </summary>
    public LoginResultKind Kind { get; set; }

    /// <summary>
    /// Session identifier issued when the attempt succeeded, otherwise empty
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// Remaining time until the next attempt is allowed when the client is locked out
    /// </summary>
    public TimeSpan RetryAfter { get; set; }

    #endregion // Properties

    #region Static methods

    /// <summary>
    /// Create a result describing a locked-out client
    /// </summary>
    /// <param name="retryAfter">Remaining time until the next attempt is allowed</param>
    /// <returns>A locked-out result</returns>
    public static LoginResult LockedOut(TimeSpan retryAfter)
    {
        return new LoginResult
               {
                   Kind = LoginResultKind.LockedOut,
                   RetryAfter = retryAfter
               };
    }

    /// <summary>
    /// Create a result describing a failed attempt
    /// </summary>
    /// <returns>A failed result</returns>
    public static LoginResult Failed()
    {
        return new LoginResult
               {
                   Kind = LoginResultKind.Failed
               };
    }

    /// <summary>
    /// Create a result describing a successful attempt
    /// </summary>
    /// <param name="sessionId">Session identifier issued for the new session</param>
    /// <returns>A successful result</returns>
    public static LoginResult Succeeded(string sessionId)
    {
        return new LoginResult
               {
                   Kind = LoginResultKind.Succeeded,
                   SessionId = sessionId
               };
    }

    #endregion // Static methods
}