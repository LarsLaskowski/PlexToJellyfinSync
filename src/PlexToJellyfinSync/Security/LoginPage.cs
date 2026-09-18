namespace PlexToJellyfinSync.Security;

/// <summary>
/// Renders the HTML for the token login page
/// </summary>
public static class LoginPage
{
    #region Constants

    /// <summary>
    /// The login page HTML template, with placeholders for the antiforgery field and the error message
    /// </summary>
    private const string HtmlTemplate = """
                                        <!DOCTYPE html>
                                        <html lang="en">
                                        <head>
                                            <meta charset="utf-8" />
                                            <meta name="viewport" content="width=device-width, initial-scale=1.0" />
                                            <title>PlexToJellyfinSync - Login</title>
                                            <style>
                                                body { font-family: system-ui, sans-serif; background: #1f1f25; color: #eee; display: flex; min-height: 100vh; align-items: center; justify-content: center; margin: 0; }
                                                form { background: #2b2b33; padding: 2rem; border-radius: 8px; box-shadow: 0 4px 20px rgba(0,0,0,.4); width: 320px; }
                                                h1 { font-size: 1.2rem; margin-top: 0; }
                                                .error { color: #f28b82; margin: 0 0 1rem; font-size: .9rem; }
                                                input { width: 100%; padding: .6rem; margin: .5rem 0 1rem; border-radius: 4px; border: 1px solid #555; background: #1f1f25; color: #eee; box-sizing: border-box; }
                                                button { width: 100%; padding: .6rem; border: none; border-radius: 4px; background: #e5a00d; color: #111; font-weight: 600; cursor: pointer; }
                                            </style>
                                        </head>
                                        <body>
                                            <form method="post" action="/login">
                                                <h1>PlexToJellyfinSync</h1>
                                                __ERROR__
                                                __ANTIFORGERY__
                                                <label for="token">Access token</label>
                                                <input type="password" id="token" name="token" autofocus />
                                                <button type="submit">Sign in</button>
                                            </form>
                                        </body>
                                        </html>
                                        """;

    /// <summary>
    /// Message shown when the previous login attempt failed
    /// </summary>
    private const string ErrorMessageHtml = "<p class=\"error\">Invalid access token.</p>";

    #endregion // Constants

    #region Static methods

    /// <summary>
    /// Render the login page HTML
    /// </summary>
    /// <param name="antiforgeryFieldHtml">Hidden input markup carrying the antiforgery token</param>
    /// <param name="showError">Whether the previous login attempt failed and an error message should be shown</param>
    /// <returns>The rendered login page HTML</returns>
    public static string Render(string antiforgeryFieldHtml, bool showError)
    {
        return HtmlTemplate.Replace("__ANTIFORGERY__", antiforgeryFieldHtml, StringComparison.Ordinal)
                           .Replace("__ERROR__", showError ? ErrorMessageHtml : string.Empty, StringComparison.Ordinal);
    }

    #endregion // Static methods
}