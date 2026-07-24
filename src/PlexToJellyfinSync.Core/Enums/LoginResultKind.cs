namespace PlexToJellyfinSync.Core.Enums;

/// <summary>
/// Outcome of a dashboard login attempt
/// </summary>
public enum LoginResultKind
{
    /// <summary>
    /// The client is currently locked out and must wait before trying again
    /// </summary>
    LockedOut = 0,

    /// <summary>
    /// The supplied token did not match the configured token
    /// </summary>
    Failed,

    /// <summary>
    /// The supplied token matched and a session was created
    /// </summary>
    Succeeded
}