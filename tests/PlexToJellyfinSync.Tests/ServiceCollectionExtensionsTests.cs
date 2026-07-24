using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using PlexToJellyfinSync.Core.Abstractions;
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
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();

        services.AddPlexToJellyfinSync(configuration);

        using var provider = services.BuildServiceProvider();

        Assert.IsNotNull(provider.GetService<ILoginThrottle>(), "The login throttle should be registered!");
        Assert.IsNotNull(provider.GetService<IDashboardLoginService>(), "The login service should be registered!");
    }

    #endregion // Methods
}