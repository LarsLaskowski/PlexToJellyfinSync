using System.Diagnostics;
using System.Reflection;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using PlexToJellyfinSync.Components.Pages;

namespace PlexToJellyfinSync.Tests;

/// <summary>
/// Tests for <see cref="ErrorPage"/>
/// </summary>
[TestClass]
public sealed class ErrorPageTests
{
    #region Constants

    /// <summary>
    /// Heading the page always shows
    /// </summary>
    private const string Heading = "An error occurred while processing your request.";

    /// <summary>
    /// Label of the request identifier
    /// </summary>
    private const string RequestIdLabel = "Request ID:";

    #endregion // Constants

    #region Methods

    /// <summary>
    /// Render the error page, optionally inside a cascading HTTP context
    /// </summary>
    /// <param name="httpContext">Context to cascade, or <see langword="null"/> for none</param>
    /// <returns>The rendered HTML</returns>
    private static async Task<string> RenderAsync(HttpContext? httpContext)
    {
        await using var services = new ServiceCollection().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, NullLoggerFactory.Instance);

        return await renderer.Dispatcher.InvokeAsync(async () =>
                                                     {
                                                         var parameters = httpContext is null
                                                                              ? ParameterView.Empty
                                                                              : ParameterView.FromDictionary(new Dictionary<string, object?>
                                                                                                             {
                                                                                                                 ["Value"] = httpContext,
                                                                                                                 ["ChildContent"] = (RenderFragment)(builder =>
                                                                                                                                                     {
                                                                                                                                                         builder.OpenComponent<ErrorPage>(0);
                                                                                                                                                         builder.CloseComponent();
                                                                                                                                                     })
                                                                                                             });

                                                         var root = httpContext is null
                                                                        ? await renderer.RenderComponentAsync<ErrorPage>(parameters)
                                                                        : await renderer.RenderComponentAsync<CascadingValue<HttpContext>>(parameters);

                                                         return root.ToHtmlString();
                                                     }).ConfigureAwait(false);
    }

    #endregion // Methods

    #region Tests

    /// <summary>
    /// The page keeps the route /Error
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    public void ErrorPageRouteIsErrorPath()
    {
        var routes = typeof(ErrorPage).GetCustomAttributes<RouteAttribute>().Select(r => r.Template).ToList();

        Assert.HasCount(1, routes, "The error page should have exactly one route!");
        Assert.AreEqual("/Error", routes[0], "The error page should be routed at /Error!");
    }

    /// <summary>
    /// Without an activity and an HTTP context the page shows the heading but no request id
    /// </summary>
    /// <returns>Task</returns>
    [TestMethod]
    [TestCategory("AC1")]
    public async Task ErrorPageWithoutRequestIdShowsNoRequestId()
    {
        var previous = Activity.Current;

        Activity.Current = null;

        try
        {
            var html = await RenderAsync(null);

            Assert.Contains(Heading, html, "The heading should always be shown!");
            Assert.DoesNotContain(RequestIdLabel, html, "No request id should be shown without a source!");
        }
        finally
        {
            Activity.Current = previous;
        }
    }

    /// <summary>
    /// The trace identifier of the HTTP context is shown when no activity is current
    /// </summary>
    /// <returns>Task</returns>
    [TestMethod]
    [TestCategory("AC1")]
    public async Task ErrorPageWithTraceIdentifierShowsIt()
    {
        var previous = Activity.Current;
        var httpContext = new DefaultHttpContext
                          {
                              TraceIdentifier = "trace-4711"
                          };

        Activity.Current = null;

        try
        {
            var html = await RenderAsync(httpContext);

            Assert.Contains(RequestIdLabel, html, "The request id label should be shown!");
            Assert.Contains("trace-4711", html, "The trace identifier should be shown!");
        }
        finally
        {
            Activity.Current = previous;
        }
    }

    /// <summary>
    /// The id of the current activity wins over the trace identifier
    /// </summary>
    /// <returns>Task</returns>
    [TestMethod]
    [TestCategory("AC1")]
    public async Task ErrorPageWithCurrentActivityShowsItsId()
    {
        var httpContext = new DefaultHttpContext
                          {
                              TraceIdentifier = "trace-4711"
                          };
        using var activity = new Activity("error-page-test");

        activity.Start();

        try
        {
            var html = await RenderAsync(httpContext);

            Assert.IsNotNull(activity.Id, "The started activity should have an id!");
            Assert.Contains(activity.Id, html, "The activity id should be shown!");
            Assert.DoesNotContain("trace-4711", html, "The trace identifier should not be shown next to the activity id!");
        }
        finally
        {
            activity.Stop();
        }
    }

    #endregion // Tests
}