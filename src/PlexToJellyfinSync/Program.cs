using Microsoft.Extensions.Options;

using PlexToJellyfinSync.Components;
using PlexToJellyfinSync.Core.Abstractions;
using PlexToJellyfinSync.Core.Options;
using PlexToJellyfinSync.Security;
using PlexToJellyfinSync.Service;
using PlexToJellyfinSync.Service.Logging;

namespace PlexToJellyfinSync;

/// <summary>
/// Main entry point for the Plex to Jellyfin sync application
/// </summary>
public static partial class Program
{
    #region Main entry point

    /// <summary>
    /// Main entry point for the application. Configures and starts the web host, which also runs the background sync worker
    /// </summary>
    /// <param name="args">An array of command-line arguments used to configure the application at startup</param>
    /// <returns>A task that represents the asynchronous operation of running the web application</returns>
    private static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Configuration.AddEnvironmentVariables(prefix: "PLEXSYNC__");

        builder.Services.AddPlexToJellyfinSync(builder.Configuration);
        builder.Services.AddHostedService<Worker>();

        builder.Services.AddRazorComponents()
                        .AddInteractiveServerComponents();

        builder.Services.AddSingleton<ILoggerProvider>(serviceProvider =>
        new InMemoryLogProvider(serviceProvider.GetRequiredService<ILogStore>()));

        var app = builder.Build();

        var dashboardOptions = app.Services.GetRequiredService<IOptions<DashboardOptions>>().Value;

        if (app.Environment.IsDevelopment() == false)
        {
            app.UseExceptionHandler("/Error", createScopeForErrors: true);
        }

        app.Use(async (context, next) =>
                {
                    var headers = context.Response.Headers;

                    headers["X-Content-Type-Options"] = "nosniff";
                    headers["X-Frame-Options"] = "DENY";
                    headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";

                    // Blazor Server requires 'unsafe-inline' for its SignalR bootstrap script and
                    // wss:/ws: for WebSocket connections; all other sources are restricted to 'self'.
                    headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; connect-src 'self' wss: ws:; img-src 'self' data:";

                    await next(context).ConfigureAwait(false);
                });

        app.MapGet("/health",
                   (ISyncStatusProvider status) =>
                   {
                       var snapshot = status.GetSnapshot();

                       return Results.Json(new
                                           {
                                               status = "ok",
                                               plexConnected = snapshot.PlexConnected,
                                               isRunning = snapshot.IsRunning,
                                               lastPollAt = snapshot.LastPollAt,
                                               lastReconcileAt = snapshot.LastReconcileAt,
                                               errors = snapshot.Errors
                                           });
                   });

        if (dashboardOptions.Enabled)
        {
            app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
            app.UseAntiforgery();
            app.UseMiddleware<TokenAuthMiddleware>();

            app.MapStaticAssets();
            app.MapRazorComponents<App>()
               .AddInteractiveServerRenderMode();

            app.MapGet("/login", () => Results.Content(LoginPage.Html, "text/html"));

            app.MapPost("/login",
                        async (HttpContext context, IOptions<DashboardOptions> options) =>
                        {
                            var form = await context.Request.ReadFormAsync();
                            var token = form["token"].ToString();

                            if (string.Equals(token, options.Value.Token, StringComparison.Ordinal))
                            {
                                context.Response.Cookies.Append(TokenAuthMiddleware.CookieName,
                                                                "1",
                                                                new CookieOptions
                                                                {
                                                                    HttpOnly = true,
                                                                    SameSite = SameSiteMode.Lax
                                                                });
                                context.Response.Redirect("/");

                                return;
                            }

                            context.Response.Redirect("/login?error=1");
                        })
               .DisableAntiforgery();
        }

        await app.RunAsync();
    }

    #endregion // Main entry point
}