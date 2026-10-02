using PlexToJellyfinSync.Core.Options;

namespace PlexToJellyfinSync.Tests;

/// <summary>
/// Tests for the data annotation validation of <see cref="SyncOptions"/>
/// </summary>
[TestClass]
public sealed class SyncOptionsTests
{
    #region Methods

    /// <summary>
    /// A poll interval within the allowed range is valid
    /// </summary>
    /// <param name="seconds">Interval in seconds</param>
    [TestMethod]
    [DataRow(5)]
    [DataRow(86400)]
    public void SyncOptionsPollIntervalInRangeIsValid(int seconds)
    {
        var results = DataAnnotationsValidation.Validate(new SyncOptions
                                                         {
                                                             PollIntervalSeconds = seconds
                                                         });

        Assert.IsEmpty(results, "A poll interval in range should be valid!");
    }

    /// <summary>
    /// A poll interval outside the allowed range is invalid
    /// </summary>
    /// <param name="seconds">Interval in seconds</param>
    [TestMethod]
    [DataRow(4)]
    [DataRow(0)]
    [DataRow(-1)]
    [DataRow(86401)]
    public void SyncOptionsPollIntervalOutOfRangeIsInvalid(int seconds)
    {
        var results = DataAnnotationsValidation.Validate(new SyncOptions
                                                         {
                                                             PollIntervalSeconds = seconds
                                                         });

        DataAnnotationsValidation.AssertNamesMember(results, nameof(SyncOptions.PollIntervalSeconds));
    }

    /// <summary>
    /// A full reconcile interval within the allowed range is valid
    /// </summary>
    /// <param name="hours">Interval in hours</param>
    [TestMethod]
    [DataRow(1)]
    [DataRow(8760)]
    public void SyncOptionsFullReconcileIntervalInRangeIsValid(int hours)
    {
        var results = DataAnnotationsValidation.Validate(new SyncOptions
                                                         {
                                                             FullReconcileIntervalHours = hours
                                                         });

        Assert.IsEmpty(results, "A reconcile interval in range should be valid!");
    }

    /// <summary>
    /// A full reconcile interval outside the allowed range is invalid
    /// </summary>
    /// <param name="hours">Interval in hours</param>
    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    [DataRow(8761)]
    public void SyncOptionsFullReconcileIntervalOutOfRangeIsInvalid(int hours)
    {
        var results = DataAnnotationsValidation.Validate(new SyncOptions
                                                         {
                                                             FullReconcileIntervalHours = hours
                                                         });

        DataAnnotationsValidation.AssertNamesMember(results, nameof(SyncOptions.FullReconcileIntervalHours));
    }

    /// <summary>
    /// A parallelism of one is valid for both settings
    /// </summary>
    [TestMethod]
    public void SyncOptionsParallelismOfOneIsValid()
    {
        var results = DataAnnotationsValidation.Validate(new SyncOptions
                                                         {
                                                             EpisodeReconcileParallelism = 1,
                                                             LibraryReconcileParallelism = 1
                                                         });

        Assert.IsEmpty(results, "A parallelism of one should be valid!");
    }

    /// <summary>
    /// A non-positive episode parallelism is invalid
    /// </summary>
    /// <param name="value">Parallelism</param>
    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void SyncOptionsNonPositiveEpisodeParallelismIsInvalid(int value)
    {
        var results = DataAnnotationsValidation.Validate(new SyncOptions
                                                         {
                                                             EpisodeReconcileParallelism = value
                                                         });

        DataAnnotationsValidation.AssertNamesMember(results, nameof(SyncOptions.EpisodeReconcileParallelism));
    }

    /// <summary>
    /// A non-positive library parallelism is invalid
    /// </summary>
    /// <param name="value">Parallelism</param>
    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void SyncOptionsNonPositiveLibraryParallelismIsInvalid(int value)
    {
        var results = DataAnnotationsValidation.Validate(new SyncOptions
                                                         {
                                                             LibraryReconcileParallelism = value
                                                         });

        DataAnnotationsValidation.AssertNamesMember(results, nameof(SyncOptions.LibraryReconcileParallelism));
    }

    /// <summary>
    /// The default sync options pass validation
    /// </summary>
    [TestMethod]
    public void SyncOptionsDefaultsAreValid()
    {
        var results = DataAnnotationsValidation.Validate(new SyncOptions());

        Assert.IsEmpty(results, "The default sync options should be valid!");
    }

    #endregion // Methods
}