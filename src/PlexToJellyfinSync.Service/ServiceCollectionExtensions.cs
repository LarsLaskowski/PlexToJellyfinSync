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
        services.Configure<PlexOptions>(configuration.GetSection(PlexOptions.SectionName));
        services.Configure<SyncOptions>(configuration.GetSection(SyncOptions.SectionName));
        services.Configure<NfoOptions>(configuration.GetSection(NfoOptions.SectionName));
        services.Configure<StateOptions>(configuration.GetSection(StateOptions.SectionName));
        services.Configure<DashboardOptions>(configuration.GetSection(DashboardOptions.SectionName));
        services.Configure<List<PathMapping>>(configuration.GetSection("PathMappings"));

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ILoginThrottle, LoginThrottle>();
        services.AddSingleton<IDashboardLoginService, DashboardLoginService>();
        services.AddSingleton<ILogStore, InMemoryLogStore>();
        services.AddSingleton<ISyncStatusProvider, SyncStatusService>();
        services.AddSingleton<WatchAggregator>();
        services.AddSingleton<IPathMapper, PathMapper>();
        services.AddSingleton<INfoWriter, NfoWriter>();
        services.AddSingleton<IStateStore, StateStore>();
        services.AddSingleton<ISyncOrchestrator, SyncOrchestrator>();

        services.AddHttpClient<IPlexClient, PlexClient>((serviceProvider, client) =>
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