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
        Assert.IsTrue(retryAfter > TimeSpan.Zero, "The retry-after should be positive while locked out!");
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
        Assert.IsTrue(secondRetryAfter > firstRetryAfter, "The backoff should grow with additional failures!");
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

    #endregion // Methods
}