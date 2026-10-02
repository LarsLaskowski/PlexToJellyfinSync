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

    #endregion // Methods
}