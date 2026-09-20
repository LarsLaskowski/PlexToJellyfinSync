using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Options;

using PlexToJellyfinSync.Core.Abstractions;
using PlexToJellyfinSync.Core.Options;

namespace PlexToJellyfinSync.Service.Logging;

/// <summary>
/// Masks the configured Plex and dashboard tokens out of log text before it is stored
/// </summary>
public sealed class SecretLogRedactor : ILogRedactor
{
    #region Constants

    /// <summary>
    /// Placeholder that replaces a redacted secret
    /// </summary>
    public const string Placeholder = "***REDACTED***";

    #endregion // Constants

    #region Fields

    private readonly string[] _secrets;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="plexOptions">Plex options</param>
    /// <param name="dashboardOptions">Dashboard options</param>
    public SecretLogRedactor(IOptions<PlexOptions> plexOptions, IOptions<DashboardOptions> dashboardOptions)
    {
        var tokens = new[] { plexOptions.Value.Token, dashboardOptions.Value.Token };

        // Snapshotted once rather than tracked via IOptionsMonitor<T>: this deployment is
        // environment-variable driven, so a rotated token always restarts the container.
        // Longest first so a secret that is a prefix of another is not left partially unmasked.
        _secrets = tokens.Where(secret => string.IsNullOrWhiteSpace(secret) == false)
                         .Distinct(StringComparer.Ordinal)
                         .OrderByDescending(secret => secret.Length)
                         .ToArray();
    }

    #endregion // Constructors

    #region ILogRedactor

    /// <inheritdoc/>
    [return: NotNullIfNotNull(nameof(text))]
    public string? Redact(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        foreach (var secret in _secrets)
        {
            text = text.Replace(secret, Placeholder, StringComparison.Ordinal);
        }

        return text;
    }

    #endregion // ILogRedactor
}