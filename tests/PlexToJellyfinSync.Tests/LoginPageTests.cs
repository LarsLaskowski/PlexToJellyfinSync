using PlexToJellyfinSync.Security;

namespace PlexToJellyfinSync.Tests;

/// <summary>
/// Tests for <see cref="LoginPage"/>
/// </summary>
[TestClass]
public sealed class LoginPageTests
{
    #region Methods

    /// <summary>
    /// The rendered page embeds the supplied antiforgery field markup
    /// </summary>
    [TestMethod]
    public void LoginPageRenderEmbedsAntiforgeryField()
    {
        const string antiforgeryField = "<input type=\"hidden\" name=\"__RequestVerificationToken\" value=\"abc123\" />";

        var html = LoginPage.Render(antiforgeryField, showError: false);

        Assert.IsTrue(html.Contains(antiforgeryField, StringComparison.Ordinal), "The rendered page should embed the antiforgery field!");
    }

    /// <summary>
    /// The rendered page shows an error message when the previous attempt failed
    /// </summary>
    [TestMethod]
    public void LoginPageRenderWithErrorShowsErrorMessage()
    {
        var html = LoginPage.Render(string.Empty, showError: true);

        Assert.IsTrue(html.Contains("Invalid access token", StringComparison.Ordinal), "The rendered page should show an error message when the previous attempt failed!");
    }

    /// <summary>
    /// The rendered page omits the error message when there was no failed attempt
    /// </summary>
    [TestMethod]
    public void LoginPageRenderWithoutErrorOmitsErrorMessage()
    {
        var html = LoginPage.Render(string.Empty, showError: false);

        Assert.IsFalse(html.Contains("Invalid access token", StringComparison.Ordinal), "The rendered page should omit the error message when there was no failed attempt!");
    }

    #endregion // Methods
}