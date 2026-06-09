namespace PlexToJellyfinSync.Security;

/// <summary>
/// Static HTML for the token login page
/// </summary>
public static class LoginPage
{
    #region Constants

    /// <summary>
    /// The login page HTML
    /// </summary>
    public const string Html = """
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
                                       input { width: 100%; padding: .6rem; margin: .5rem 0 1rem; border-radius: 4px; border: 1px solid #555; background: #1f1f25; color: #eee; box-sizing: border-box; }
                                       button { width: 100%; padding: .6rem; border: none; border-radius: 4px; background: #e5a00d; color: #111; font-weight: 600; cursor: pointer; }
                                   </style>
                               </head>
                               <body>
                                   <form method="post" action="/login">
                                       <h1>PlexToJellyfinSync</h1>
                                       <label for="token">Access token</label>
                                       <input type="password" id="token" name="token" autofocus />
                                       <button type="submit">Sign in</button>
                                   </form>
                               </body>
                               </html>
                               """;

    #endregion // Constants
}