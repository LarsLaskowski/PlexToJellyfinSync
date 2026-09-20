using Microsoft.Extensions.Options;

using PlexToJellyfinSync.Core.Options;
using PlexToJellyfinSync.Service.Logging;

namespace PlexToJellyfinSync.Tests;

/// <summary>
/// Tests for <see cref="SecretLogRedactor"/>
/// </summary>
[TestClass]
public sealed class SecretLogRedactorTests
{
    #region Methods

    /// <summary>
    /// A text containing the configured Plex token has it masked
    /// </summary>
    [TestMethod]
    public void SecretLogRedactorPlexTokenIsMasked()
    {
        var redactor = CreateRedactor(plexToken: "plex-secret");

        var result = redactor.Redact("Requesting http://plex.test?X-Plex-Token=plex-secret");

        Assert.AreEqual($"Requesting http://plex.test?X-Plex-Token={SecretLogRedactor.Placeholder}", result, "The Plex token should be masked!");
    }

    /// <summary>
    /// A text containing the configured dashboard token has it masked
    /// </summary>
    [TestMethod]
    public void SecretLogRedactorDashboardTokenIsMasked()
    {
        var redactor = CreateRedactor(dashboardToken: "dash-secret");

        var result = redactor.Redact("Login attempt with token dash-secret");

        Assert.AreEqual($"Login attempt with token {SecretLogRedactor.Placeholder}", result, "The dashboard token should be masked!");
    }

    /// <summary>
    /// Every occurrence of a repeated secret is masked, not just the first
    /// </summary>
    [TestMethod]
    public void SecretLogRedactorRepeatedTokenAllOccurrencesAreMasked()
    {
        var redactor = CreateRedactor(plexToken: "abc");

        var result = redactor.Redact("abc then abc again");

        Assert.AreEqual($"{SecretLogRedactor.Placeholder} then {SecretLogRedactor.Placeholder} again", result, "Every occurrence should be masked!");
    }

    /// <summary>
    /// Text without any configured secret is returned unchanged
    /// </summary>
    [TestMethod]
    public void SecretLogRedactorNoSecretConfiguredReturnsTextUnchanged()
    {
        var redactor = CreateRedactor();

        var result = redactor.Redact("Processed the movie library");

        Assert.AreEqual("Processed the movie library", result, "Text should be unchanged when no secret is configured!");
    }

    /// <summary>
    /// Null input is returned unchanged
    /// </summary>
    [TestMethod]
    public void SecretLogRedactorNullTextReturnsNull()
    {
        var redactor = CreateRedactor(plexToken: "plex-secret");

        var result = redactor.Redact(null);

        Assert.IsNull(result, "Null input should be returned unchanged!");
    }

    /// <summary>
    /// Create a redactor with the given tokens configured
    /// </summary>
    /// <param name="plexToken">Plex token, or <c>null</c> for none configured</param>
    /// <param name="dashboardToken">Dashboard token, or <c>null</c> for none configured</param>
    /// <returns>The redactor under test</returns>
    private static SecretLogRedactor CreateRedactor(string? plexToken = null, string? dashboardToken = null)
    {
        return new SecretLogRedactor(Options.Create(new PlexOptions
                                                    {
                                                        Token = plexToken ?? string.Empty
                                                    }),
                                     Options.Create(new DashboardOptions
                                                    {
                                                        Token = dashboardToken ?? string.Empty
                                                    }));
    }

    #endregion // Methods
}