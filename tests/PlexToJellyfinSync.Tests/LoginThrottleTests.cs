using PlexToJellyfinSync.Service.Security;

namespace PlexToJellyfinSync.Tests;

/// <summary>
/// Tests for <see cref="LoginThrottle"/>
/// </summary>
[TestClass]
public sealed class LoginThrottleTests
{
    #region Methods

    /// <summary>
    /// The first tolerated failures do not lock the client out
    /// </summary>
    [TestMethod]
    public void LoginThrottleWithinFreeAttemptsIsNotLockedOut()
    {
        var time = new TestTimeProvider(DateTimeOffset.UnixEpoch);
        var throttle = new LoginThrottle(time);

        for (var index = 0; index < 5; index++)
        {
            throttle.RegisterFailure("10.0.0.1");
        }

        var lockedOut = throttle.IsLockedOut("10.0.0.1", out _);

        Assert.IsFalse(lockedOut, "The client should not be locked out within the free attempts!");
    }

    /// <summary>
    /// Exceeding the free attempts locks the client out with a positive backoff
    /// </summary>
    [TestMethod]
    public void LoginThrottleBeyondFreeAttemptsIsLockedOut()
    {
        var time = new TestTimeProvider(DateTimeOffset.UnixEpoch);
        var throttle = new LoginThrottle(time);

        for (var index = 0; index < 6; index++)
        {
            throttle.RegisterFailure("10.0.0.1");
        }

        var lockedOut = throttle.IsLockedOut("10.0.0.1", out var retryAfter);

        Assert.IsTrue(lockedOut, "The client should be locked out after exceeding the free attempts!");
        Assert.IsGreaterThan(TimeSpan.Zero, retryAfter, "The retry-after should be positive while locked out!");
    }

    /// <summary>
    /// A successful login clears the recorded failures
    /// </summary>
    [TestMethod]
    public void LoginThrottleSuccessClearsLockout()
    {
        var time = new TestTimeProvider(DateTimeOffset.UnixEpoch);
        var throttle = new LoginThrottle(time);

        for (var index = 0; index < 6; index++)
        {
            throttle.RegisterFailure("10.0.0.1");
        }

        throttle.RegisterSuccess("10.0.0.1");

        var lockedOut = throttle.IsLockedOut("10.0.0.1", out _);

        Assert.IsFalse(lockedOut, "A successful login should clear the lockout!");
    }

    /// <summary>
    /// Each additional failure extends the backoff duration
    /// </summary>
    [TestMethod]
    public void LoginThrottleAdditionalFailuresIncreaseBackoff()
    {
        var time = new TestTimeProvider(DateTimeOffset.UnixEpoch);
        var throttle = new LoginThrottle(time);

        for (var index = 0; index < 6; index++)
        {
            throttle.RegisterFailure("10.0.0.1");
        }

        throttle.IsLockedOut("10.0.0.1", out var firstRetryAfter);

        time.Advance(firstRetryAfter);
        throttle.RegisterFailure("10.0.0.1");

        var lockedOut = throttle.IsLockedOut("10.0.0.1", out var secondRetryAfter);

        Assert.IsTrue(lockedOut, "The client should remain locked out after another failure!");
        Assert.IsGreaterThan(firstRetryAfter, secondRetryAfter, "The backoff should grow with additional failures!");
    }

    /// <summary>
    /// The lockout is lifted once its duration has elapsed
    /// </summary>
    [TestMethod]
    public void LoginThrottleLockoutExpiresAfterDuration()
    {
        var time = new TestTimeProvider(DateTimeOffset.UnixEpoch);
        var throttle = new LoginThrottle(time);

        for (var index = 0; index < 6; index++)
        {
            throttle.RegisterFailure("10.0.0.1");
        }

        time.Advance(TimeSpan.FromMinutes(10));

        var lockedOut = throttle.IsLockedOut("10.0.0.1", out _);

        Assert.IsFalse(lockedOut, "The lockout should expire once its duration has elapsed!");
    }

    /// <summary>
    /// Failures of one client do not affect another client
    /// </summary>
    [TestMethod]
    public void LoginThrottleClientsAreIndependent()
    {
        var time = new TestTimeProvider(DateTimeOffset.UnixEpoch);
        var throttle = new LoginThrottle(time);

        for (var index = 0; index < 6; index++)
        {
            throttle.RegisterFailure("10.0.0.1");
        }

        var otherLockedOut = throttle.IsLockedOut("10.0.0.2", out _);

        Assert.IsFalse(otherLockedOut, "A different client should not be locked out!");
    }

    /// <summary>
    /// Idle entries are pruned once the tracked set grows past its bound
    /// </summary>
    [TestMethod]
    public void LoginThrottlePrunesStaleEntries()
    {
        var time = new TestTimeProvider(DateTimeOffset.UnixEpoch);
        var throttle = new LoginThrottle(time);

        for (var index = 0; index < 1100; index++)
        {
            throttle.RegisterFailure($"10.1.{index / 256}.{index % 256}");
        }

        time.Advance(TimeSpan.FromMinutes(20));
        throttle.RegisterFailure("10.9.9.9");

        var lockedOut = throttle.IsLockedOut("10.1.0.0", out _);

        Assert.IsFalse(lockedOut, "A pruned idle client should no longer be tracked!");
    }

    /// <summary>
    /// The number of per-client records never exceeds the bound
    /// </summary>
    [TestMethod]
    public void LoginThrottleTrackedClientsStayWithinBound()
    {
        var time = new TestTimeProvider(DateTimeOffset.UnixEpoch);
        var throttle = new LoginThrottle(time);

        FillMap(throttle, 2000);

        Assert.AreEqual(1024, throttle.TrackedClientCount, "No more than 1024 records should be held!");
    }

    /// <summary>
    /// A new client meeting a full map first frees idle records and then gets its own record
    /// </summary>
    [TestMethod]
    public void LoginThrottleFullMapPrunesIdleRecordsBeforeSharing()
    {
        var time = new TestTimeProvider(DateTimeOffset.UnixEpoch);
        var throttle = new LoginThrottle(time);

        FillMap(throttle, 1024);
        time.Advance(TimeSpan.FromMinutes(20));
        throttle.RegisterFailure("10.9.9.9");

        Assert.AreEqual(1, throttle.TrackedClientCount, "The idle records should be removed and the new client tracked!");
    }

    /// <summary>
    /// Untracked clients share one record while the map is full
    /// </summary>
    [TestMethod]
    public void LoginThrottleFullMapCountsNewClientsOnSharedRecord()
    {
        var time = new TestTimeProvider(DateTimeOffset.UnixEpoch);
        var throttle = new LoginThrottle(time);

        FillMap(throttle, 1024);

        for (var index = 0; index < 6; index++)
        {
            throttle.RegisterFailure($"10.9.0.{index}");
        }

        var newLockedOut = throttle.IsLockedOut("10.9.0.100", out var retryAfter);
        var trackedLockedOut = throttle.IsLockedOut("10.1.0.0", out _);

        Assert.IsTrue(newLockedOut, "A seventh new client should be locked out by the shared record!");
        Assert.AreEqual(TimeSpan.FromSeconds(1), retryAfter, "The shared record should apply the first backoff step!");
        Assert.IsFalse(trackedLockedOut, "A tracked client should be judged by its own record!");
        Assert.AreEqual(1024, throttle.TrackedClientCount, "The shared record should not count as a tracked client!");
    }

    /// <summary>
    /// A success of a client counted on the shared record does not clear the shared record
    /// </summary>
    [TestMethod]
    public void LoginThrottleSuccessDoesNotClearSharedRecord()
    {
        var time = new TestTimeProvider(DateTimeOffset.UnixEpoch);
        var throttle = new LoginThrottle(time);

        FillMap(throttle, 1024);

        for (var index = 0; index < 6; index++)
        {
            throttle.RegisterFailure($"10.9.0.{index}");
        }

        throttle.RegisterSuccess("10.9.0.0");

        var lockedOut = throttle.IsLockedOut("10.9.0.100", out _);

        Assert.IsTrue(lockedOut, "A success of a shared-record client should not clear the shared record!");
    }

    /// <summary>
    /// Register one failure for each of a number of distinct clients
    /// </summary>
    /// <param name="throttle">Throttle to fill</param>
    /// <param name="count">Number of distinct clients</param>
    private static void FillMap(LoginThrottle throttle, int count)
    {
        for (var index = 0; index < count; index++)
        {
            throttle.RegisterFailure($"10.1.{index / 256}.{index % 256}");
        }
    }

    #endregion // Methods
}