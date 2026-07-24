using System.Net;
using System.Text;

namespace PlexToJellyfinSync.Tests;

/// <summary>
/// HTTP message handler that answers requests from a preconfigured map of path to JSON payload
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    #region Properties

    /// <summary>
    /// JSON payloads keyed by the absolute request path
    /// </summary>
    public Dictionary<string, string> Responses { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Path and query of every received request, in order
    /// </summary>
    public List<string> Requests { get; } = [];

    #endregion // Properties

    #region HttpMessageHandler

    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri;

        Requests.Add(uri is null ? string.Empty : uri.PathAndQuery);

        if (uri is null || Responses.TryGetValue(uri.AbsolutePath, out var json) == false)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                               {
                                   Content = new StringContent(json, Encoding.UTF8, "application/json")
                               });
    }

    #endregion // HttpMessageHandler
}