using System.Collections.Concurrent;

using PlexToJellyfinSync.Core.Abstractions;

namespace PlexToJellyfinSync.Service.Security;

/// <summary>
/// In-memory per-client throttle that applies an exponential backoff lockout after repeated failures
/// </summary>
public sealed class LoginThrottle : ILoginThrottle
{
    #region Constants

    /// <summary>
    /// Number of failures tolerated before a lockout is applied
    /// </summary>
    private const int FreeAttempts = 5;

    /// <summary>
    /// Upper bound on the number of tracked clients before stale entries are pruned
    /// </summary>
    private const int MaxTrackedClients = 1024;

    #endregion // Constants

    #region Fields

    private static readonly TimeSpan _baseLockout = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan _maxLockout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan _retentionWindow = TimeSpan.FromMinutes(15);

    private readonly ConcurrentDictionary<string, LoginAttemptState> _attempts = new(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="timeProvider">Time provider used to measure lockout windows</param>
    public LoginThrottle(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    #endregion // Constructors

    #region Methods

    /// <summary>
    /// Remove entries that have been idle beyond the retention window once the tracked set grows large
    /// </summary>
    /// <param name="now">Current time</param>
    private void PruneStaleEntries(DateTimeOffset now)
    {
        if (_attempts.Count <= MaxTrackedClients)
        {
            return;
        }

        var stale = _attempts.Where(pair => now - pair.Value.LastActivity > _retentionWindow
                                            && pair.Value.LockedUntil <= now)
                             .Select(pair => pair.Key)
                             .ToList();

        foreach (var key in stale)
        {
            _attempts.TryRemove(key, out _);
        }
    }

    #endregion // Methods

    #region ILoginThrottle

    /// <summary>
    /// Determine whether a client is currently locked out from attempting a login
    /// </summary>
    /// <param name="clientKey">Identifier for the client, e.g. its remote IP address</param>
    /// <param name="retryAfter">When locked out, the remaining time until the next attempt is allowed</param>
    /// <returns>True when the client must wait before trying again</returns>
    public bool IsLockedOut(string clientKey, out TimeSpan retryAfter)
    {
        retryAfter = TimeSpan.Zero;

        if (_attempts.TryGetValue(clientKey, out var attempt) == false)
        {
            return false;
        }

        var now = _timeProvider.GetUtcNow();

        lock (attempt)
        {
            if (attempt.LockedUntil <= now)
            {
                return false;
            }

            retryAfter = attempt.LockedUntil - now;

            return true;
        }
    }

    /// <summary>
    /// Record a failed login attempt for a client and extend its backoff lockout
    /// </summary>
    /// <param name="clientKey">Identifier for the client, e.g. its remote IP address</param>
    public void RegisterFailure(string clientKey)
    {
        var now = _timeProvider.GetUtcNow();
        var attempt = _attempts.GetOrAdd(clientKey, _ => new LoginAttemptState());

        lock (attempt)
        {
            // A long idle period resets the counter so occasional typos do not accumulate forever.
            if (now - attempt.LastActivity > _retentionWindow)
            {
                attempt.Failures = 0;
            }

            attempt.Failures++;
            attempt.LastActivity = now;

            if (attempt.Failures > FreeAttempts)
            {
                var exponent = attempt.Failures - FreeAttempts - 1;
                var seconds = Math.Min(_baseLockout.TotalSeconds * Math.Pow(2, exponent), _maxLockout.TotalSeconds);

                attempt.LockedUntil = now.AddSeconds(seconds);
            }
        }

        PruneStaleEntries(now);
    }

    /// <summary>
    /// Clear the recorded failures for a client after a successful login
    /// </summary>
    /// <param name="clientKey">Identifier for the client, e.g. its remote IP address</param>
    public void RegisterSuccess(string clientKey)
    {
        _attempts.TryRemove(clientKey, out _);
    }

    #endregion // ILoginThrottle
}