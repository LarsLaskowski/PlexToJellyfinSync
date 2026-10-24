namespace PlexToJellyfinSync.Security;

/// <summary>
/// Logs one warning per process when forwarded headers arrive from a sender that is not a trusted proxy
/// </summary>
public sealed class ForwardedHeadersWarning
{
    #region Fields

    private readonly ILogger<ForwardedHeadersWarning> _logger;
    private int _logged;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="logger">Logger</param>
    public ForwardedHeadersWarning(ILogger<ForwardedHeadersWarning> logger)
    {
        _logger = logger;
    }

    #endregion // Constructors

    #region Methods

    /// <summary>
    /// Inspect a request after the forwarded-headers middleware and log the warning on first untrusted use
    /// </summary>
    /// <param name="context">HTTP context</param>
    public void Inspect(HttpContext context)
    {
        var request = context.Request;
        var untrusted = (request.Headers.ContainsKey("X-Forwarded-For") && request.Headers.ContainsKey("X-Original-For") == false)
                        || (request.Headers.ContainsKey("X-Forwarded-Proto") && request.Headers.ContainsKey("X-Original-Proto") == false);

        if (untrusted == false || Interlocked.Exchange(ref _logged, 1) != 0)
        {
            return;
        }

        if (_logger.IsEnabled(LogLevel.Warning))
        {
            var sender = context.Connection.RemoteIpAddress;

            _logger.LogWarning("Forwarded headers from {Sender} were not applied: the sender is not a trusted proxy or the header is malformed. List the reverse proxy in Dashboard:TrustedProxies (environment variable PLEXSYNC__Dashboard__TrustedProxies__0). This warning is logged once.", sender);
        }
    }

    #endregion // Methods
}