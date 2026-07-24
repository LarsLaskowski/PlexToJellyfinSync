using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using PlexToJellyfinSync.Core.Abstractions;
using PlexToJellyfinSync.Core.Options;
using PlexToJellyfinSync.Service;

namespace PlexToJellyfinSync.Tests;

/// <summary>
/// Tests for <see cref="ServiceCollectionExtensions"/>
/// </summary>
[TestClass]
public sealed class ServiceCollectionExtensionsTests
{
    #region Methods

    /// <summary>
    /// The registration resolves the login throttle and login service
    /// </summary>
    [TestMethod]
    public void ServiceCollectionExtensionsRegistersLoginServices()
    {
        using var provider = BuildProvider();

        Assert.IsNotNull(provider.GetService<ILoginThrottle>(), "The login throttle should be registered!");
        Assert.IsNotNull(provider.GetService<IDashboardLoginService>(), "The login service should be registered!");
    }

    /// <summary>
    /// The registration resolves the complete synchronization pipeline
    /// </summary>
    [TestMethod]
    public void ServiceCollectionExtensionsRegistersSyncPipeline()
    {
        using var provider = BuildProvider();

        Assert.IsNotNull(provider.GetService<IPlexClient>(), "The Plex client should be registered!");
        Assert.IsNotNull(provider.GetService<IPathMapper>(), "The path mapper should be registered!");
        Assert.IsNotNull(provider.GetService<INfoWriter>(), "The NFO writer should be registered!");
        Assert.IsNotNull(provider.GetService<IStateStore>(), "The state store should be registered!");
        Assert.IsNotNull(provider.GetService<ISyncStatusProvider>(), "The status provider should be registered!");
        Assert.IsNotNull(provider.GetService<ILogStore>(), "The log store should be registered!");
        Assert.IsNotNull(provider.GetService<WatchAggregator>(), "The watch aggregator should be registered!");
        Assert.IsNotNull(provider.GetService<ISyncOrchestrator>(), "The orchestrator should be registered!");
        Assert.IsNotNull(provider.GetService<TimeProvider>(), "The time provider should be registered!");
    }

    /// <summary>
    /// The shared state holders are registered as singletons
    /// </summary>
    [TestMethod]
    public void ServiceCollectionExtensionsRegistersSharedStateAsSingleton()
    {
        using var provider = BuildProvider();

        Assert.AreSame(provider.GetRequiredService<ISyncStatusProvider>(),
                       provider.GetRequiredService<ISyncStatusProvider>(),
                       "The status provider should be a singleton!");

        Assert.AreSame(provider.GetRequiredService<ILogStore>(),
                       provider.GetRequiredService<ILogStore>(),
                       "The log store should be a singleton!");

        Assert.AreSame(provider.GetRequiredService<IStateStore>(),
                       provider.GetRequiredService<IStateStore>(),
                       "The state store should be a singleton!");
    }

    /// <summary>
    /// The registration binds every configuration section
    /// </summary>
    [TestMethod]
    public void ServiceCollectionExtensionsBindsConfigurationSections()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>(StringComparer.Ordinal)
                                           {
                                               ["Plex:BaseUrl"] = "http://plex.test:32400",
                                               ["Plex:Token"] = "secret",
                                               ["Plex:Libraries:0"] = "1",
                                               ["Sync:PollIntervalSeconds"] = "15",
                                               ["State:Directory"] = "/state",
                                               ["Dashboard:LogBufferSize"] = "25",
                                               ["PathMappings:0:Plex"] = "/data/Movies",
                                               ["PathMappings:0:Local"] = "/media/Movies"
                                           });

        var plexOptions = provider.GetRequiredService<IOptions<PlexOptions>>().Value;
        var syncOptions = provider.GetRequiredService<IOptions<SyncOptions>>().Value;
        var stateOptions = provider.GetRequiredService<IOptions<StateOptions>>().Value;
        var dashboardOptions = provider.GetRequiredService<IOptions<DashboardOptions>>().Value;
        var pathMappings = provider.GetRequiredService<IOptions<List<PathMapping>>>().Value;

        Assert.AreEqual("http://plex.test:32400", plexOptions.BaseUrl, "The Plex base URL should be bound!");
        Assert.AreEqual("secret", plexOptions.Token, "The Plex token should be bound!");
        Assert.HasCount(1, plexOptions.Libraries, "The library filter should be bound!");
        Assert.AreEqual(15, syncOptions.PollIntervalSeconds, "The poll interval should be bound!");
        Assert.AreEqual("/state", stateOptions.Directory, "The state directory should be bound!");
        Assert.AreEqual(25, dashboardOptions.LogBufferSize, "The log buffer size should be bound!");
        Assert.HasCount(1, pathMappings, "The path mappings should be bound!");
        Assert.AreEqual("/data/Movies", pathMappings[0].Plex, "The Plex prefix of the mapping should be bound!");
        Assert.AreEqual("/media/Movies", pathMappings[0].Local, "The local prefix of the mapping should be bound!");
    }

    /// <summary>
    /// The configured base URL and token are applied to the Plex HTTP client
    /// </summary>
    [TestMethod]
    public void ServiceCollectionExtensionsConfiguresPlexHttpClient()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>(StringComparer.Ordinal)
                                           {
                                               ["Plex:BaseUrl"] = "http://plex.test:32400",
                                               ["Plex:Token"] = "secret"
                                           });

        var factory = provider.GetRequiredService<IHttpClientFactory>();

        using var httpClient = factory.CreateClient(nameof(IPlexClient));

        Assert.AreEqual(new Uri("http://plex.test:32400"), httpClient.BaseAddress, "The configured base URL should be applied!");
        Assert.IsTrue(httpClient.DefaultRequestHeaders.Contains("X-Plex-Token"), "The Plex token header should be applied!");
        Assert.AreEqual(TimeSpan.FromSeconds(30), httpClient.Timeout, "The request timeout should be applied!");
    }

    /// <summary>
    /// Build a service provider from the given configuration values
    /// </summary>
    /// <param name="values">Configuration values, or <c>null</c> for an empty configuration</param>
    /// <returns>The built service provider</returns>
    private static ServiceProvider BuildProvider(Dictionary<string, string?>? values = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values ?? new Dictionary<string, string?>(StringComparer.Ordinal)).Build();
        var services = new ServiceCollection();

        services.AddPlexToJellyfinSync(configuration);

        return services.BuildServiceProvider();
    }

    #endregion // Methods
}