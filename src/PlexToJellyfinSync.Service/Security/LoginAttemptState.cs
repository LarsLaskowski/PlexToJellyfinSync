namespace PlexToJellyfinSync.Service.Security;

/// <summary>
/// Mutable per-client failure state tracked by the login throttle
/// </summary>
internal sealed class LoginAttemptState
{
    #region Properties

    /// <summary>
    /// Number of consecutive failed attempts within the retention window
    /// </summary>
    public int Failures { get; set; }

    /// <summary>
    /// Time until which further attempts are rejected
    /// </summary>
    public DateTimeOffset LockedUntil { get; set; }

    /// <summary>
    /// Time of the most recent recorded failure
    /// </summary>
    public DateTimeOffset LastActivity { get; set; }

    #endregion // Properties
}