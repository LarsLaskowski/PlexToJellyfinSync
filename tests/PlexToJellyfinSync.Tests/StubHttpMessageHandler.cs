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
    /// JSON payloads to be served in order for successive requests to the same path, keyed by the
    /// absolute request path; checked before <see cref="Responses"/> and consumed one at a time
    /// </summary>
    public Dictionary<string, Queue<string>> ResponseSequences { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Path and query of every received request, in order
    /// </summary>
    public List<string> Requests { get; } = [];

    /// <summary>
    /// Status codes to serve for a given absolute request path instead of the default 200/404,
    /// checked before <see cref="Responses"/> is consulted
    /// </summary>
    public Dictionary<string, HttpStatusCode> StatusCodes { get; } = new(StringComparer.Ordinal);

    #endregion // Properties

    #region HttpMessageHandler

    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var uri = request.RequestUri;

        Requests.Add(uri is null ? string.Empty : uri.PathAndQuery);

        if (uri is null)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        if (StatusCodes.TryGetValue(uri.AbsolutePath, out var statusCode))
        {
            return Task.FromResult(new HttpResponseMessage(statusCode));
        }

        if (ResponseSequences.TryGetValue(uri.AbsolutePath, out var sequence) && sequence.Count > 0)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                                   {
                                       Content = new StringContent(sequence.Dequeue(), Encoding.UTF8, "application/json")
                                   });
        }

        if (Responses.TryGetValue(uri.AbsolutePath, out var json) == false)
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