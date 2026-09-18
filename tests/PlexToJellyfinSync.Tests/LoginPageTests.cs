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

    /// <summary>
    /// The built antiforgery field embeds the field name and the request token
    /// </summary>
    [TestMethod]
    public void LoginPageBuildAntiforgeryFieldEmbedsFieldNameAndToken()
    {
        var field = LoginPage.BuildAntiforgeryField("__RequestVerificationToken", "abc123");

        Assert.IsTrue(field.Contains("name=\"__RequestVerificationToken\"", StringComparison.Ordinal), "The built field should carry the given form field name!");
        Assert.IsTrue(field.Contains("value=\"abc123\"", StringComparison.Ordinal), "The built field should carry the given request token!");
    }

    /// <summary>
    /// The built antiforgery field HTML-encodes the request token, so a token containing markup cannot break out of the attribute
    /// </summary>
    [TestMethod]
    public void LoginPageBuildAntiforgeryFieldEncodesToken()
    {
        var field = LoginPage.BuildAntiforgeryField("__RequestVerificationToken", "\"><script>");

        Assert.IsFalse(field.Contains("<script>", StringComparison.Ordinal), "A raw script tag in the token should never appear unescaped in the rendered field!");
    }

    /// <summary>
    /// An error query value of "1" indicates the previous login attempt failed
    /// </summary>
    [TestMethod]
    public void LoginPageShouldShowErrorWithErrorValueOneReturnsTrue()
    {
        var result = LoginPage.ShouldShowError("1");

        Assert.IsTrue(result, "An error query value of \"1\" should show the error message!");
    }

    /// <summary>
    /// A missing error query value does not show the error message
    /// </summary>
    [TestMethod]
    public void LoginPageShouldShowErrorWithNullValueReturnsFalse()
    {
        var result = LoginPage.ShouldShowError(null);

        Assert.IsFalse(result, "A missing error query value should not show the error message!");
    }

    /// <summary>
    /// An unrecognized error query value does not show the error message
    /// </summary>
    [TestMethod]
    public void LoginPageShouldShowErrorWithUnrecognizedValueReturnsFalse()
    {
        var result = LoginPage.ShouldShowError("true");

        Assert.IsFalse(result, "An unrecognized error query value should not show the error message!");
    }

    #endregion // Methods
}