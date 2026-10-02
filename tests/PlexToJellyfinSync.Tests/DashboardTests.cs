using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using PlexToJellyfinSync.Components.Pages;
using PlexToJellyfinSync.Core.Abstractions;
using PlexToJellyfinSync.Core.Models;

namespace PlexToJellyfinSync.Tests;

/// <summary>
/// Tests for <see cref="Dashboard"/>
/// </summary>
[TestClass]
public sealed class DashboardTests
{
    #region Constants

    /// <summary>
    /// Maximum time to wait for an asynchronous re-render
    /// </summary>
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);

    #endregion // Constants

    #region Methods

    /// <summary>
    /// Create a service provider that hosts the given provider fake
    /// </summary>
    /// <param name="provider">Fake to register</param>
    /// <returns>The built service provider</returns>
    private static ServiceProvider CreateServices(FakeSyncStatusProvider provider)
    {
        var services = new ServiceCollection();

        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton<ISyncStatusProvider>(provider);

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Render the dashboard and read its current HTML on the renderer's dispatcher
    /// </summary>
    /// <param name="renderer">Renderer that hosts the dashboard</param>
    /// <returns>Returns the root component to read the output from later</returns>
    private static async Task<HtmlRootComponent> RenderAsync(HtmlRenderer renderer)
    {
        return await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<Dashboard>()).ConfigureAwait(false);
    }

    /// <summary>
    /// Read the current HTML of the root component on the renderer's dispatcher
    /// </summary>
    /// <param name="renderer">Renderer that hosts the dashboard</param>
    /// <param name="root">Root component</param>
    /// <returns>The current HTML</returns>
    private static Task<string> ReadHtmlAsync(HtmlRenderer renderer, HtmlRootComponent root)
    {
        return renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
    }

    /// <summary>
    /// Wait until the rendered HTML contains the expected text
    /// </summary>
    /// <param name="renderer">Renderer that hosts the dashboard</param>
    /// <param name="root">Root component</param>
    /// <param name="expected">Text that has to appear</param>
    /// <returns>The last read HTML</returns>
    private static async Task<string> WaitForHtmlAsync(HtmlRenderer renderer, HtmlRootComponent root, string expected)
    {
        var deadline = DateTime.UtcNow + _timeout;
        var html = await ReadHtmlAsync(renderer, root).ConfigureAwait(false);

        while (html.Contains(expected, StringComparison.Ordinal) == false && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20).ConfigureAwait(false);
            html = await ReadHtmlAsync(renderer, root).ConfigureAwait(false);
        }

        return html;
    }

    /// <summary>
    /// Wait until the fake has served at least the given number of snapshot requests
    /// </summary>
    /// <param name="provider">Fake to observe</param>
    /// <param name="calls">Minimum number of requests</param>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    private static async Task WaitForSnapshotCallsAsync(FakeSyncStatusProvider provider, int calls)
    {
        var deadline = DateTime.UtcNow + _timeout;

        while (provider.SnapshotCalls < calls && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20).ConfigureAwait(false);
        }
    }

    #endregion // Methods

    #region Tests

    /// <summary>
    /// A status change raised from a foreign thread reads the snapshot only on the renderer's dispatcher
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task DashboardChangedFromBackgroundThreadReadsSnapshotOnDispatcher()
    {
        var provider = new FakeSyncStatusProvider();

        await using var services = CreateServices(provider);
        await using var renderer = new HtmlRenderer(services, NullLoggerFactory.Instance);

        provider.DispatcherCheck = () => renderer.Dispatcher.CheckAccess();

        var root = await RenderAsync(renderer).ConfigureAwait(false);

        provider.ResetCalls();
        provider.Snapshot = new SyncStatusViewData
                            {
                                ItemsProcessed = 4242
                            };

        await Task.Run(provider.RaiseChanged).ConfigureAwait(false);
        await WaitForSnapshotCallsAsync(provider, 1).ConfigureAwait(false);
        await WaitForHtmlAsync(renderer, root, "4242").ConfigureAwait(false);

        var checks = provider.GetDispatcherChecks();

        Assert.IsGreaterThanOrEqualTo(1, checks.Count, "The dashboard should read a snapshot in response to the change!");
        Assert.IsTrue(checks.All(onDispatcher => onDispatcher), "Every snapshot read in response to the change should happen on the renderer's dispatcher!");
    }

    /// <summary>
    /// A status change re-renders the dashboard with the new snapshot
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task DashboardChangedRendersNewSnapshot()
    {
        var provider = new FakeSyncStatusProvider
                       {
                           Snapshot = new SyncStatusViewData
                                      {
                                          ItemsProcessed = 1,
                                          LastError = null
                                      }
                       };

        await using var services = CreateServices(provider);
        await using var renderer = new HtmlRenderer(services, NullLoggerFactory.Instance);

        var root = await RenderAsync(renderer).ConfigureAwait(false);

        provider.Snapshot = new SyncStatusViewData
                            {
                                ItemsProcessed = 7777,
                                Errors = 1,
                                LastError = "Plex went away"
                            };

        await Task.Run(provider.RaiseChanged).ConfigureAwait(false);

        var html = await WaitForHtmlAsync(renderer, root, "Plex went away").ConfigureAwait(false);

        Assert.IsTrue(html.Contains("7777", StringComparison.Ordinal), "The re-rendered dashboard should show the new item count!");
        Assert.IsTrue(html.Contains("Plex went away", StringComparison.Ordinal), "The re-rendered dashboard should show the new last error!");
    }

    /// <summary>
    /// The initial render shows the snapshot that is current at initialization
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task DashboardInitialRenderShowsCurrentSnapshot()
    {
        var provider = new FakeSyncStatusProvider
                       {
                           Snapshot = new SyncStatusViewData
                                      {
                                          ItemsProcessed = 31337,
                                          PlexConnected = true,
                                          LastError = "initial failure"
                                      }
                       };

        await using var services = CreateServices(provider);
        await using var renderer = new HtmlRenderer(services, NullLoggerFactory.Instance);

        var root = await RenderAsync(renderer).ConfigureAwait(false);
        var html = await ReadHtmlAsync(renderer, root).ConfigureAwait(false);

        Assert.IsTrue(html.Contains("31337", StringComparison.Ordinal), "The initial render should show the current item count!");
        Assert.IsTrue(html.Contains("initial failure", StringComparison.Ordinal), "The initial render should show the current last error!");
        Assert.IsTrue(html.Contains("Connected", StringComparison.Ordinal), "The initial render should show the current connection state!");
        Assert.AreEqual(1, provider.SubscriberCount, "The dashboard should subscribe to the status changes on initialization!");
    }

    /// <summary>
    /// A status change after disposal does not read a snapshot any more
    /// </summary>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    [TestMethod]
    public async Task DashboardChangedAfterDisposeDoesNotReadSnapshot()
    {
        var provider = new FakeSyncStatusProvider();

        await using var services = CreateServices(provider);

        var renderer = new HtmlRenderer(services, NullLoggerFactory.Instance);

        await RenderAsync(renderer).ConfigureAwait(false);
        await renderer.DisposeAsync().ConfigureAwait(false);

        provider.ResetCalls();

        await Task.Run(provider.RaiseChanged).ConfigureAwait(false);
        await Task.Delay(200).ConfigureAwait(false);

        Assert.AreEqual(0, provider.SubscriberCount, "The dashboard should unsubscribe from the status changes on dispose!");
        Assert.AreEqual(0, provider.SnapshotCalls, "A disposed dashboard should not read a snapshot any more!");
    }

    #endregion // Tests
}