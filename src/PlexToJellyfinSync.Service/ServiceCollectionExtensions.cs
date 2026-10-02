using System.Net.Http.Headers;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using PlexToJellyfinSync.Core.Abstractions;
using PlexToJellyfinSync.Core.Options;
using PlexToJellyfinSync.Service.Logging;
using PlexToJellyfinSync.Service.Security;
using PlexToJellyfinSync.Service.State;

namespace PlexToJellyfinSync.Service;

/// <summary>
/// Dependency injection registration for the synchronization services
/// </summary>
public static class ServiceCollectionExtensions
{
    #region Static methods

    /// <summary>
    /// Register all synchronization services and bind configuration
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Application configuration</param>
    /// <returns>The service collection</returns>
    public static IServiceCollection AddPlexToJellyfinSync(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<PlexOptions>().Bind(configuration.GetSection(PlexOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<SyncOptions>().Bind(configuration.GetSection(SyncOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.Configure<NfoOptions>(configuration.GetSection(NfoOptions.SectionName));
        services.AddOptions<StateOptions>().Bind(configuration.GetSection(StateOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<DashboardOptions>().Bind(configuration.GetSection(DashboardOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.Configure<List<PathMapping>>(configuration.GetSection("PathMappings"));

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ILoginThrottle, LoginThrottle>();
        services.AddSingleton<IDashboardLoginService, DashboardLoginService>();
        services.AddSingleton<ILogStore, InMemoryLogStore>();
        services.AddSingleton<ILogRedactor, SecretLogRedactor>();
        services.AddSingleton<ISyncStatusProvider, SyncStatusService>();
        services.AddSingleton<WatchAggregator>();
        services.AddSingleton<IPathMapper, PathMapper>();
        services.AddSingleton<INfoWriter, NfoWriter>();
        services.AddSingleton<IStateStore, StateStore>();
        services.AddSingleton<IPlexClient, PlexClient>();
        services.AddSingleton<ISyncOrchestrator, SyncOrchestrator>();

        // Registered as a named client, not a typed client (AddHttpClient<IPlexClient, PlexClient>), so
        // PlexClient can ask IHttpClientFactory for a fresh client per request instead of one client
        // instance being captured for the app's lifetime by the singleton PlexClient above.
        services.AddHttpClient(nameof(IPlexClient),
                               (serviceProvider, client) =>
                               {
                                   var options = serviceProvider.GetRequiredService<IOptions<PlexOptions>>().Value;

                                   if (string.IsNullOrWhiteSpace(options.BaseUrl) == false)
                                   {
                                       client.BaseAddress = new Uri(options.BaseUrl);
                                   }

                                   if (string.IsNullOrWhiteSpace(options.Token) == false)
                                   {
                                       client.DefaultRequestHeaders.Add("X-Plex-Token", options.Token);
                                   }

                                   client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                                   client.Timeout = TimeSpan.FromSeconds(30);
                               });

        return services;
    }

    #endregion // Static methods
}