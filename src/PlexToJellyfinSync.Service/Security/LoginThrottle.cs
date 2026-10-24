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
    /// Upper bound on the number of per-client records; beyond it untracked clients share one record. A new client
    /// meeting a full set scans all records under the one lock, which is cheap at this size, so do not raise the
    /// bound without revisiting that cost
    /// </summary>
    private const int MaxTrackedClients = 1024;

    #endregion // Constants

    #region Fields

    private static readonly TimeSpan _baseLockout = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan _maxLockout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan _retentionWindow = TimeSpan.FromMinutes(15);

    private readonly Dictionary<string, LoginAttemptState> _attempts = new(StringComparer.Ordinal);
    private readonly Lock _lock = new();
    private readonly LoginAttemptState _sharedRecord = new();
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

    #region Properties

    /// <summary>
    /// Number of per-client records currently held
    /// </summary>
    public int TrackedClientCount
    {
        get
        {
            lock (_lock)
            {
                return _attempts.Count;
            }
        }
    }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Find the record that judges a client; the caller holds the lock
    /// </summary>
    /// <param name="clientKey">Client identifier</param>
    /// <param name="now">Current time</param>
    /// <param name="create">Whether a new record is created for an untracked client when there is room</param>
    /// <returns>The client's record, the shared record when the set is full, or null for an untracked client without creation</returns>
    private LoginAttemptState? ResolveRecord(string clientKey, DateTimeOffset now, bool create)
    {
        if (_attempts.TryGetValue(clientKey, out var tracked))
        {
            return tracked;
        }

        if (_attempts.Count >= MaxTrackedClients)
        {
            var stale = _attempts.Where(pair => now - pair.Value.LastActivity > _retentionWindow
                                                && pair.Value.LockedUntil <= now)
                                 .Select(pair => pair.Key)
                                 .ToList();

            foreach (var key in stale)
            {
                _attempts.Remove(key);
            }
        }

        if (_attempts.Count >= MaxTrackedClients)
        {
            return _sharedRecord;
        }

        if (create == false)
        {
            return null;
        }

        var added = new LoginAttemptState();

        _attempts[clientKey] = added;

        return added;
    }

    #endregion // Methods

    #region ILoginThrottle

    /// <inheritdoc/>
    public bool IsLockedOut(string clientKey, out TimeSpan retryAfter)
    {
        retryAfter = TimeSpan.Zero;

        var now = _timeProvider.GetUtcNow();

        lock (_lock)
        {
            var attempt = ResolveRecord(clientKey, now, false);

            if (attempt is null || attempt.LockedUntil <= now)
            {
                return false;
            }

            retryAfter = attempt.LockedUntil - now;

            return true;
        }
    }

    /// <inheritdoc/>
    public void RegisterFailure(string clientKey)
    {
        var now = _timeProvider.GetUtcNow();

        lock (_lock)
        {
            var attempt = ResolveRecord(clientKey, now, true)!;

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
    }

    /// <inheritdoc/>
    public void RegisterSuccess(string clientKey)
    {
        lock (_lock)
        {
            _attempts.Remove(clientKey);
        }
    }

    #endregion // ILoginThrottle
}