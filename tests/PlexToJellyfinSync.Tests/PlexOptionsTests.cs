using System.ComponentModel.DataAnnotations;

using PlexToJellyfinSync.Core.Options;

namespace PlexToJellyfinSync.Tests;

/// <summary>
/// Tests for the data annotation validation of <see cref="PlexOptions"/>
/// </summary>
[TestClass]
public sealed class PlexOptionsTests
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
        var results = DataAnnotationsValidation.Validate(new PlexOptions
                                                         {
                                                             BaseUrl = baseUrl!
                                                         });

        DataAnnotationsValidation.AssertNamesMember(results, nameof(PlexOptions.BaseUrl));
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
        var results = DataAnnotationsValidation.Validate(new PlexOptions
                                                         {
                                                             BaseUrl = baseUrl
                                                         });

        DataAnnotationsValidation.AssertNamesMember(results, nameof(PlexOptions.BaseUrl));
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
        var results = DataAnnotationsValidation.Validate(new PlexOptions
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
        var results = DataAnnotationsValidation.Validate(new PlexOptions
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
        var results = DataAnnotationsValidation.Validate(new PlexOptions
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
        var results = DataAnnotationsValidation.Validate(new PlexOptions
                                                         {
                                                             BaseUrl = ValidBaseUrl,
                                                             OwnerAccountId = accountId
                                                         });

        DataAnnotationsValidation.AssertNamesMember(results, nameof(PlexOptions.OwnerAccountId));
    }

    /// <summary>
    /// The default Plex options with a base URL pass validation
    /// </summary>
    [TestMethod]
    public void PlexOptionsDefaultsWithBaseUrlAreValid()
    {
        var results = DataAnnotationsValidation.Validate(new PlexOptions
                                                         {
                                                             BaseUrl = ValidBaseUrl
                                                         });

        Assert.IsEmpty(results, "The default Plex options with a base URL should be valid!");
    }

    /// <summary>
    /// No validation message contains a configured value
    /// </summary>
    [TestMethod]
    public void PlexOptionsValidationMessagesDoNotContainConfiguredValues()
    {
        var results = DataAnnotationsValidation.Validate(new PlexOptions
                                                         {
                                                             BaseUrl = "http://user:hunter2@",
                                                             Token = "tok-secret-123"
                                                         });

        Assert.IsNotEmpty(results, "The base URL without a host should be invalid!");

        var messages = string.Join(' ', results.Select(obj => obj.ErrorMessage));

        Assert.DoesNotContain("hunter2", messages, "The messages should not contain the URL credentials!");
        Assert.DoesNotContain("tok-secret-123", messages, "The messages should not contain the token!");
    }

    /// <summary>
    /// A blank base URL yields no results from the direct validation because the required attribute reports it
    /// </summary>
    /// <param name="baseUrl">Base URL</param>
    [TestMethod]
    [DataRow(null, DisplayName = "null")]
    [DataRow("", DisplayName = "empty")]
    [DataRow("   ", DisplayName = "whitespace")]
    public void PlexOptionsValidateWithBlankBaseUrlReturnsNoResults(string? baseUrl)
    {
        var options = new PlexOptions
                      {
                          BaseUrl = baseUrl!
                      };

        var results = options.Validate(new ValidationContext(options)).ToList();

        Assert.IsEmpty(results, "A blank base URL should be left to the required attribute!");
    }

    #endregion // Methods
}