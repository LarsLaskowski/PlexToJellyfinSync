using System.ComponentModel.DataAnnotations;

namespace PlexToJellyfinSync.Core.Options;

/// <summary>
/// Configuration for the web dashboard (status and logs)
/// </summary>
public sealed class DashboardOptions : IValidatableObject
{
    #region Constants

    /// <summary>
    /// Configuration section name
    /// </summary>
    public const string SectionName = "Dashboard";

    #endregion // Constants

    #region Properties

    /// <summary>
    /// Whether the web dashboard is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Optional access token; when empty the dashboard is publicly reachable
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Number of log entries kept in the in-memory ring buffer
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "Dashboard:LogBufferSize must be at least 1 (environment variable PLEXSYNC__Dashboard__LogBufferSize).")]
    public int LogBufferSize { get; set; } = 1000;

    /// <summary>
    /// IP addresses or CIDR networks of reverse proxies whose forwarded headers are trusted, in addition to loopback
    /// </summary>
    public string[] TrustedProxies { get; set; } = [];

    #endregion // Properties

    #region IValidatableObject

    /// <inheritdoc/>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var entries = TrustedProxies ?? [];

        for (var index = 0; index < entries.Length; index++)
        {
            if (TrustedProxyParser.TryParse(entries[index], out _) == false)
            {
                yield return new ValidationResult($"Dashboard:TrustedProxies:{index} must be an IP address or a CIDR network such as 172.18.0.0/16 (environment variable PLEXSYNC__Dashboard__TrustedProxies__{index}).",
                                                  [nameof(TrustedProxies)]);
            }
        }
    }

    #endregion // IValidatableObject
}