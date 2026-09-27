namespace PlexToJellyfinSync.Tests;

/// <summary>
/// HTTP client factory stub that returns a preconfigured client for every request and counts how often
/// a client was requested
/// </summary>
internal sealed class FakeHttpClientFactory : IHttpClientFactory
{
    #region Fields

    private readonly HttpClient _httpClient;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="httpClient">Client returned for every request</param>
    public FakeHttpClientFactory(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Number of times <see cref="CreateClient"/> was called
    /// </summary>
    public int CreateClientCallCount { get; private set; }

    #endregion // Properties

    #region IHttpClientFactory

    /// <inheritdoc/>
    public HttpClient CreateClient(string name)
    {
        CreateClientCallCount++;

        return _httpClient;
    }

    #endregion // IHttpClientFactory
}