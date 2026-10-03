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
        Assert.IsNotNull(provider.GetService<ILogRedactor>(), "The log redactor should be registered!");
        Assert.IsNotNull(provider.GetService<WatchAggregator>(), "The watch aggregator should be registered!");
        Assert.IsNotNull(provider.GetService<ISyncOrchestrator>(), "The orchestrator should be registered!");
        Assert.IsNotNull(provider.GetService<TimeProvider>(), "The time provider should be registered!");
    }

    /// <summary>
    /// The orchestrator collaborators are registered as singletons and the orchestrator still resolves
    /// </summary>
    [TestMethod]
    public void ServiceCollectionExtensionsRegistersOrchestratorCollaboratorsAsSingletons()
    {
        using var provider = BuildProvider();

        Assert.IsNotNull(provider.GetService<IMediaItemWriter>(), "The media item writer should be registered!");
        Assert.IsNotNull(provider.GetService<ISeriesAggregateWriter>(), "The series aggregate writer should be registered!");
        Assert.IsNotNull(provider.GetService<ILibraryReconciler>(), "The library reconciler should be registered!");
        Assert.IsNotNull(provider.GetService<ISyncOrchestrator>(), "The orchestrator should still resolve!");

        Assert.AreSame(provider.GetRequiredService<IMediaItemWriter>(),
                       provider.GetRequiredService<IMediaItemWriter>(),
                       "The media item writer should be a singleton!");

        Assert.AreSame(provider.GetRequiredService<ISeriesAggregateWriter>(),
                       provider.GetRequiredService<ISeriesAggregateWriter>(),
                       "The series aggregate writer should be a singleton!");

        Assert.AreSame(provider.GetRequiredService<ILibraryReconciler>(),
                       provider.GetRequiredService<ILibraryReconciler>(),
                       "The library reconciler should be a singleton!");
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
    /// The Plex client is registered as a singleton, resolving its HTTP client from
    /// <see cref="IHttpClientFactory"/> on every request instead of one being captured by a typed client
    /// registration
    /// </summary>
    [TestMethod]
    public void ServiceCollectionExtensionsRegistersPlexClientAsSingleton()
    {
        using var provider = BuildProvider();

        Assert.AreSame(provider.GetRequiredService<IPlexClient>(),
                       provider.GetRequiredService<IPlexClient>(),
                       "The Plex client should be a singleton!");
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
    /// With a valid base URL only, every options type resolves and the startup validation succeeds
    /// </summary>
    [TestMethod]
    public void ServiceCollectionExtensionsMinimalConfigurationIsValid()
    {
        using var provider = BuildProvider();

        Assert.IsNotNull(provider.GetRequiredService<IOptions<PlexOptions>>().Value, "The Plex options should resolve!");
        Assert.IsNotNull(provider.GetRequiredService<IOptions<SyncOptions>>().Value, "The sync options should resolve!");
        Assert.IsNotNull(provider.GetRequiredService<IOptions<StateOptions>>().Value, "The state options should resolve!");
        Assert.IsNotNull(provider.GetRequiredService<IOptions<DashboardOptions>>().Value, "The dashboard options should resolve!");
        Assert.IsNotNull(provider.GetRequiredService<IOptions<NfoOptions>>().Value, "The NFO options should resolve!");

        provider.GetRequiredService<IStartupValidator>().Validate();
    }

    /// <summary>
    /// An empty configuration fails the startup validation without any options having been resolved
    /// </summary>
    [TestMethod]
    public void ServiceCollectionExtensionsEmptyConfigurationFailsStartupValidation()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>(StringComparer.Ordinal));

        var validator = provider.GetRequiredService<IStartupValidator>();

        Assert.Throws<OptionsValidationException>(validator.Validate, "A missing base URL should fail the startup validation!");
    }

    /// <summary>
    /// An out-of-range sync option fails the startup validation and the options resolution
    /// </summary>
    [TestMethod]
    public void ServiceCollectionExtensionsInvalidSyncOptionsFailValidation()
    {
        using var provider = BuildProvider(WithBaseUrl("Sync:PollIntervalSeconds", "0"));

        AssertInvalid<SyncOptions>(provider);
    }

    /// <summary>
    /// An out-of-range dashboard option fails the startup validation and the options resolution
    /// </summary>
    [TestMethod]
    public void ServiceCollectionExtensionsInvalidDashboardOptionsFailValidation()
    {
        using var provider = BuildProvider(WithBaseUrl("Dashboard:LogBufferSize", "0"));

        AssertInvalid<DashboardOptions>(provider);
    }

    /// <summary>
    /// A blank state directory fails the startup validation and the options resolution
    /// </summary>
    [TestMethod]
    public void ServiceCollectionExtensionsBlankStateDirectoryFailsValidation()
    {
        using var provider = BuildProvider(WithBaseUrl("State:Directory", " "));

        AssertInvalid<StateOptions>(provider);
    }

    /// <summary>
    /// The startup validation message does not contain the configured token or URL credentials
    /// </summary>
    [TestMethod]
    public void ServiceCollectionExtensionsValidationMessageDoesNotLeakSecrets()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>(StringComparer.Ordinal)
                                           {
                                               ["Plex:BaseUrl"] = "http://user:hunter2@",
                                               ["Plex:Token"] = "tok-secret-123"
                                           });

        var validator = provider.GetRequiredService<IStartupValidator>();
        var exception = Assert.Throws<OptionsValidationException>(validator.Validate, "A base URL without a host should fail the startup validation!");

        Assert.DoesNotContain("hunter2", exception.Message, "The message should not contain the URL credentials!");
        Assert.DoesNotContain("tok-secret-123", exception.Message, "The message should not contain the token!");
    }

    /// <summary>
    /// Create a configuration with a valid base URL and one additional value
    /// </summary>
    /// <param name="key">Additional configuration key</param>
    /// <param name="value">Additional configuration value</param>
    /// <returns>The configuration values</returns>
    private static Dictionary<string, string?> WithBaseUrl(string key, string value)
    {
        return new Dictionary<string, string?>(StringComparer.Ordinal)
               {
                   ["Plex:BaseUrl"] = "http://plex.test:32400",
                   [key] = value
               };
    }

    /// <summary>
    /// Assert that the startup validation and the options resolution of the given type fail
    /// </summary>
    /// <typeparam name="TOptions">Options type</typeparam>
    /// <param name="provider">Service provider</param>
    private static void AssertInvalid<TOptions>(ServiceProvider provider)
        where TOptions : class
    {
        var validator = provider.GetRequiredService<IStartupValidator>();

        Assert.Throws<OptionsValidationException>(validator.Validate, "The startup validation should fail!");

        var exception = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<TOptions>>().Value, "The options resolution should fail!");

        Assert.AreEqual(typeof(TOptions), exception.OptionsType, "The exception should name the affected options type!");
    }

    /// <summary>
    /// Build a service provider from the given configuration values
    /// </summary>
    /// <param name="values">Configuration values, or <c>null</c> for a minimal valid configuration</param>
    /// <returns>The built service provider</returns>
    private static ServiceProvider BuildProvider(Dictionary<string, string?>? values = null)
    {
        var source = values ?? new Dictionary<string, string?>(StringComparer.Ordinal)
                               {
                                   ["Plex:BaseUrl"] = "http://plex.test:32400"
                               };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(source)
                                                      .Build();
        var services = new ServiceCollection();

        services.AddPlexToJellyfinSync(configuration);

        return services.BuildServiceProvider();
    }

    #endregion // Methods
}