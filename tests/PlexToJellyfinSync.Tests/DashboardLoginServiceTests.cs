using Microsoft.Extensions.Options;

using PlexToJellyfinSync.Core.Enums;
using PlexToJellyfinSync.Core.Options;
using PlexToJellyfinSync.Service.Security;

namespace PlexToJellyfinSync.Tests;

/// <summary>
/// Tests for <see cref="DashboardLoginService"/>
/// </summary>
[TestClass]
public sealed class DashboardLoginServiceTests
{
    #region Methods

    /// <summary>
    /// A matching token yields a successful result with a session identifier
    /// </summary>
    [TestMethod]
    public void DashboardLoginServiceMatchingTokenSucceeds()
    {
        var service = CreateService("s3cr3t");

        var result = service.Authenticate("10.0.0.1", "s3cr3t");

        Assert.AreEqual(LoginResultKind.Succeeded, result.Kind, "A matching token should succeed!");
        Assert.IsGreaterThan(0, result.SessionId.Length, "A successful login should issue a session identifier!");
    }

    /// <summary>
    /// A wrong token yields a failed result
    /// </summary>
    [TestMethod]
    public void DashboardLoginServiceWrongTokenFails()
    {
        var service = CreateService("s3cr3t");

        var result = service.Authenticate("10.0.0.1", "wrong");

        Assert.AreEqual(LoginResultKind.Failed, result.Kind, "A wrong token should fail!");
        Assert.AreEqual(0, result.SessionId.Length, "A failed login should not issue a session identifier!");
    }

    /// <summary>
    /// Repeated failures eventually lock the client out
    /// </summary>
    [TestMethod]
    public void DashboardLoginServiceRepeatedFailuresLockOut()
    {
        var service = CreateService("s3cr3t");

        for (var index = 0; index < 6; index++)
        {
            service.Authenticate("10.0.0.1", "wrong");
        }

        var result = service.Authenticate("10.0.0.1", "wrong");

        Assert.AreEqual(LoginResultKind.LockedOut, result.Kind, "Repeated failures should lock the client out!");
        Assert.IsGreaterThan(TimeSpan.Zero, result.RetryAfter, "A locked-out result should carry a positive retry-after!");
    }

    /// <summary>
    /// A successful login clears earlier failures so the client is not locked out
    /// </summary>
    [TestMethod]
    public void DashboardLoginServiceSuccessClearsFailures()
    {
        var service = CreateService("s3cr3t");

        service.Authenticate("10.0.0.1", "wrong");
        service.Authenticate("10.0.0.1", "wrong");
        service.Authenticate("10.0.0.1", "s3cr3t");

        for (var index = 0; index < 5; index++)
        {
            service.Authenticate("10.0.0.1", "wrong");
        }

        var result = service.Authenticate("10.0.0.1", "wrong");

        Assert.AreEqual(LoginResultKind.Failed, result.Kind, "Failures after a success should not immediately lock the client out!");
    }

    #endregion // Methods

    #region Static methods

    /// <summary>
    /// Create a login service with the given configured token
    /// </summary>
    /// <param name="token">Configured dashboard token</param>
    /// <returns>The login service</returns>
    private static DashboardLoginService CreateService(string token)
    {
        var throttle = new LoginThrottle(new TestTimeProvider(DateTimeOffset.UnixEpoch));
        var options = Options.Create(new DashboardOptions
                                     {
                                         Token = token
                                     });

        return new DashboardLoginService(throttle, options);
    }

    #endregion // Static methods
}