using PlexToJellyfinSync.Core.Options;

namespace PlexToJellyfinSync.Tests;

/// <summary>
/// Tests for the data annotation validation of <see cref="DashboardOptions"/>
/// </summary>
[TestClass]
public sealed class DashboardOptionsTests
{
    #region Methods

    /// <summary>
    /// A log buffer size of one is valid and the dashboard token stays optional
    /// </summary>
    [TestMethod]
    public void DashboardOptionsMinimalBufferAndEmptyTokenIsValid()
    {
        var results = DataAnnotationsValidation.Validate(new DashboardOptions
                                                         {
                                                             LogBufferSize = 1,
                                                             Token = string.Empty
                                                         });

        Assert.IsEmpty(results, "A buffer of one and an empty token should be valid!");
    }

    /// <summary>
    /// A non-positive log buffer size is invalid
    /// </summary>
    /// <param name="size">Buffer size</param>
    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void DashboardOptionsNonPositiveLogBufferSizeIsInvalid(int size)
    {
        var results = DataAnnotationsValidation.Validate(new DashboardOptions
                                                         {
                                                             LogBufferSize = size
                                                         });

        DataAnnotationsValidation.AssertNamesMember(results, nameof(DashboardOptions.LogBufferSize));
    }

    /// <summary>
    /// The default dashboard options pass validation
    /// </summary>
    [TestMethod]
    public void DashboardOptionsDefaultsAreValid()
    {
        var results = DataAnnotationsValidation.Validate(new DashboardOptions());

        Assert.IsEmpty(results, "The default dashboard options should be valid!");
    }

    /// <summary>
    /// Valid trusted proxy entries pass validation
    /// </summary>
    [TestMethod]
    public void DashboardOptionsValidTrustedProxiesAreValid()
    {
        var results = DataAnnotationsValidation.Validate(new DashboardOptions
                                                         {
                                                             TrustedProxies = ["172.18.0.0/16", "192.0.2.10", "2001:db8::/32"]
                                                         });

        Assert.IsEmpty(results, "Valid addresses and networks should be accepted!");
    }

    /// <summary>
    /// An invalid trusted proxy entry is reported with its index and without its value
    /// </summary>
    [TestMethod]
    public void DashboardOptionsInvalidTrustedProxyReportsIndexWithoutValue()
    {
        var results = DataAnnotationsValidation.Validate(new DashboardOptions
                                                         {
                                                             TrustedProxies = ["10.0.0.0/8", "proxy.local"]
                                                         });

        Assert.HasCount(1, results, "Only the invalid entry should be reported!");
        Assert.AreEqual("Dashboard:TrustedProxies:1 must be an IP address or a CIDR network such as 172.18.0.0/16 (environment variable PLEXSYNC__Dashboard__TrustedProxies__1).",
                        results[0].ErrorMessage,
                        "The message should name the index and the environment variable!");
        Assert.DoesNotContain("proxy.local", results[0].ErrorMessage ?? string.Empty, "The message should not contain the entry!");
        DataAnnotationsValidation.AssertNamesMember(results, nameof(DashboardOptions.TrustedProxies));
    }

    #endregion // Methods
}