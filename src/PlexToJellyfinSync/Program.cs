using System.Security.Cryptography;

using Microsoft.Extensions.Caching.Memory;
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

        builder.Services.AddMemoryCache();

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
                        async (HttpContext context, IOptions<DashboardOptions> options, IMemoryCache cache) =>
                        {
                            var form = await context.Request.ReadFormAsync();
                            var token = form["token"].ToString();

                            if (string.Equals(token, options.Value.Token, StringComparison.Ordinal))
                            {
                                var sessionId = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

                                cache.Set(TokenAuthMiddleware.SessionCachePrefix + sessionId,
                                          true,
                                          TimeSpan.FromHours(8));

                                context.Response.Cookies.Append(TokenAuthMiddleware.CookieName,
                                                                sessionId,
                                                                new CookieOptions
                                                                {
                                                                    HttpOnly = true,
                                                                    Secure = true,
                                                                    SameSite = SameSiteMode.Strict,
                                                                    MaxAge = TimeSpan.FromHours(8)
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