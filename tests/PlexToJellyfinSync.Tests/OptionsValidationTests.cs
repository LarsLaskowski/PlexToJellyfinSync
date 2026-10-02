using System.ComponentModel.DataAnnotations;

using PlexToJellyfinSync.Core.Options;

namespace PlexToJellyfinSync.Tests;

/// <summary>
/// Tests for the data annotation validation of the options classes
/// </summary>
[TestClass]
public sealed class OptionsValidationTests
{
    #region Constants

    private const string ValidBaseUrl = "http://plex:32400";

    #endregion // Constants

    #region Methods

    /// <summary>
    /// A missing base URL is invalid and names the configuration key
    /// </summary>
    /// <param name="baseUrl">Base URL</param>
    [TestMethod]
    [DataRow(null, DisplayName = "null")]
    [DataRow("", DisplayName = "empty")]
    [DataRow("   ", DisplayName = "whitespace")]
    public void PlexOptionsMissingBaseUrlIsInvalid(string? baseUrl)
    {
        var results = Validate(new PlexOptions
                               {
                                   BaseUrl = baseUrl!
                               });

        AssertNamesMember(results, nameof(PlexOptions.BaseUrl));
        Assert.Contains("Plex:BaseUrl", string.Join(' ', results.Select(obj => obj.ErrorMessage)), "The message should name Plex:BaseUrl!");
    }

    /// <summary>
    /// A base URL that is not an absolute http or https URI is invalid
    /// </summary>
    /// <param name="baseUrl">Base URL</param>
    [TestMethod]
    [DataRow("plex:32400")]
    [DataRow("/relative/path")]
    [DataRow("ftp://plex:21")]
    [DataRow("http://")]
    [DataRow("not a url")]
    public void PlexOptionsMalformedBaseUrlIsInvalid(string baseUrl)
    {
        var results = Validate(new PlexOptions
                               {
                                   BaseUrl = baseUrl
                               });

        AssertNamesMember(results, nameof(PlexOptions.BaseUrl));
    }

    /// <summary>
    /// A well-formed http or https base URL is valid
    /// </summary>
    /// <param name="baseUrl">Base URL</param>
    [TestMethod]
    [DataRow("http://plex:32400")]
    [DataRow("https://plex.example.com")]
    [DataRow("http://192.168.1.10:32400/")]
    public void PlexOptionsWellFormedBaseUrlIsValid(string baseUrl)
    {
        var results = Validate(new PlexOptions
                               {
                                   BaseUrl = baseUrl
                               });

        Assert.IsEmpty(results, "A well-formed base URL should be valid!");
    }

    /// <summary>
    /// The Plex token stays optional
    /// </summary>
    [TestMethod]
    public void PlexOptionsEmptyTokenIsValid()
    {
        var results = Validate(new PlexOptions
                               {
                                   BaseUrl = ValidBaseUrl,
                                   Token = string.Empty
                               });

        Assert.IsEmpty(results, "An empty token should be valid!");
    }

    /// <summary>
    /// A positive or absent owner account id is valid
    /// </summary>
    /// <param name="accountId">Owner account id</param>
    [TestMethod]
    [DataRow(null, DisplayName = "null")]
    [DataRow(1, DisplayName = "one")]
    public void PlexOptionsValidOwnerAccountIdIsValid(int? accountId)
    {
        var results = Validate(new PlexOptions
                               {
                                   BaseUrl = ValidBaseUrl,
                                   OwnerAccountId = accountId
                               });

        Assert.IsEmpty(results, "The owner account id should be valid!");
    }

    /// <summary>
    /// A non-positive owner account id is invalid
    /// </summary>
    /// <param name="accountId">Owner account id</param>
    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void PlexOptionsNonPositiveOwnerAccountIdIsInvalid(int accountId)
    {
        var results = Validate(new PlexOptions
                               {
                                   BaseUrl = ValidBaseUrl,
                                   OwnerAccountId = accountId
                               });

        AssertNamesMember(results, nameof(PlexOptions.OwnerAccountId));
    }

    /// <summary>
    /// A poll interval within the allowed range is valid
    /// </summary>
    /// <param name="seconds">Interval in seconds</param>
    [TestMethod]
    [DataRow(5)]
    [DataRow(86400)]
    public void SyncOptionsPollIntervalInRangeIsValid(int seconds)
    {
        var results = Validate(new SyncOptions
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
        var results = Validate(new SyncOptions
                               {
                                   PollIntervalSeconds = seconds
                               });

        AssertNamesMember(results, nameof(SyncOptions.PollIntervalSeconds));
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
        var results = Validate(new SyncOptions
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
        var results = Validate(new SyncOptions
                               {
                                   FullReconcileIntervalHours = hours
                               });

        AssertNamesMember(results, nameof(SyncOptions.FullReconcileIntervalHours));
    }

    /// <summary>
    /// A parallelism of one is valid for both settings
    /// </summary>
    [TestMethod]
    public void SyncOptionsParallelismOfOneIsValid()
    {
        var results = Validate(new SyncOptions
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
        var results = Validate(new SyncOptions
                               {
                                   EpisodeReconcileParallelism = value
                               });

        AssertNamesMember(results, nameof(SyncOptions.EpisodeReconcileParallelism));
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
        var results = Validate(new SyncOptions
                               {
                                   LibraryReconcileParallelism = value
                               });

        AssertNamesMember(results, nameof(SyncOptions.LibraryReconcileParallelism));
    }

    /// <summary>
    /// A log buffer size of one is valid and the dashboard token stays optional
    /// </summary>
    [TestMethod]
    public void DashboardOptionsMinimalBufferAndEmptyTokenIsValid()
    {
        var results = Validate(new DashboardOptions
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
        var results = Validate(new DashboardOptions
                               {
                                   LogBufferSize = size
                               });

        AssertNamesMember(results, nameof(DashboardOptions.LogBufferSize));
    }

    /// <summary>
    /// An empty or whitespace state directory is invalid
    /// </summary>
    /// <param name="directory">State directory</param>
    [TestMethod]
    [DataRow("", DisplayName = "empty")]
    [DataRow("   ", DisplayName = "whitespace")]
    public void StateOptionsBlankDirectoryIsInvalid(string directory)
    {
        var results = Validate(new StateOptions
                               {
                                   Directory = directory
                               });

        AssertNamesMember(results, nameof(StateOptions.Directory));
    }

    /// <summary>
    /// A configured state directory is valid
    /// </summary>
    [TestMethod]
    public void StateOptionsConfiguredDirectoryIsValid()
    {
        var results = Validate(new StateOptions
                               {
                                   Directory = "/config"
                               });

        Assert.IsEmpty(results, "A configured directory should be valid!");
    }

    /// <summary>
    /// Every shipped default except the Plex base URL passes validation
    /// </summary>
    [TestMethod]
    public void OptionsDefaultsAreValid()
    {
        Assert.IsEmpty(Validate(new SyncOptions()), "The default sync options should be valid!");
        Assert.IsEmpty(Validate(new DashboardOptions()), "The default dashboard options should be valid!");
        Assert.IsEmpty(Validate(new StateOptions()), "The default state options should be valid!");
        Assert.IsEmpty(Validate(new PlexOptions
                                {
                                    BaseUrl = ValidBaseUrl
                                }),
                       "The default Plex options with a base URL should be valid!");
    }

    /// <summary>
    /// No validation message contains a configured value
    /// </summary>
    [TestMethod]
    public void PlexOptionsValidationMessagesDoNotContainConfiguredValues()
    {
        var results = Validate(new PlexOptions
                               {
                                   BaseUrl = "http://user:hunter2@",
                                   Token = "tok-secret-123"
                               });

        Assert.IsNotEmpty(results, "The base URL without a host should be invalid!");

        foreach (var result in results)
        {
            Assert.DoesNotContain("hunter2", result.ErrorMessage ?? string.Empty, "The message should not contain the URL credentials!");
            Assert.DoesNotContain("tok-secret-123", result.ErrorMessage ?? string.Empty, "The message should not contain the token!");
        }
    }

    /// <summary>
    /// Validate an object the way <c>ValidateDataAnnotations</c> does
    /// </summary>
    /// <param name="instance">Object to validate</param>
    /// <returns>The validation results</returns>
    private static List<ValidationResult> Validate(object instance)
    {
        var results = new List<ValidationResult>();

        Validator.TryValidateObject(instance, new ValidationContext(instance), results, true);

        return results;
    }

    /// <summary>
    /// Assert that exactly the given member is reported
    /// </summary>
    /// <param name="results">Validation results</param>
    /// <param name="memberName">Expected member name</param>
    private static void AssertNamesMember(List<ValidationResult> results, string memberName)
    {
        Assert.Contains(memberName, results.SelectMany(obj => obj.MemberNames).ToList(), $"The result should name {memberName}!");
    }

    #endregion // Methods
}