using PlexToJellyfinSync.Core.Options;

namespace PlexToJellyfinSync.Tests;

/// <summary>
/// Tests for the data annotation validation of <see cref="StateOptions"/>
/// </summary>
[TestClass]
public sealed class StateOptionsTests
{
    #region Methods

    /// <summary>
    /// An empty or whitespace state directory is invalid
    /// </summary>
    /// <param name="directory">State directory</param>
    [TestMethod]
    [DataRow("", DisplayName = "empty")]
    [DataRow("   ", DisplayName = "whitespace")]
    public void StateOptionsBlankDirectoryIsInvalid(string directory)
    {
        var results = DataAnnotationsValidation.Validate(new StateOptions
                                                         {
                                                             Directory = directory
                                                         });

        DataAnnotationsValidation.AssertNamesMember(results, nameof(StateOptions.Directory));
    }

    /// <summary>
    /// A configured state directory is valid
    /// </summary>
    [TestMethod]
    public void StateOptionsConfiguredDirectoryIsValid()
    {
        var results = DataAnnotationsValidation.Validate(new StateOptions
                                                         {
                                                             Directory = "/config"
                                                         });

        Assert.IsEmpty(results, "A configured directory should be valid!");
    }

    /// <summary>
    /// The default state options pass validation
    /// </summary>
    [TestMethod]
    public void StateOptionsDefaultsAreValid()
    {
        var results = DataAnnotationsValidation.Validate(new StateOptions());

        Assert.IsEmpty(results, "The default state options should be valid!");
    }

    #endregion // Methods
}