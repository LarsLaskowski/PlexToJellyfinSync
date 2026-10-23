namespace PlexToJellyfinSync.Security;

/// <summary>
/// Logs one warning per process when forwarded headers arrive from a sender that is not a trusted proxy
/// </summary>
public sealed class ForwardedHeadersWarning
{
    #region Fields

    private readonly ILogger<ForwardedHeadersWarning> _logger;

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
        throw new NotImplementedException();
    }

    #endregion // Methods
}