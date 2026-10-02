using System.ComponentModel.DataAnnotations;

namespace PlexToJellyfinSync.Core.Options;

/// <summary>
/// Configuration for connecting to the Plex media server
/// </summary>
public sealed class PlexOptions : IValidatableObject
{
    #region Constants

    /// <summary>
    /// Configuration section name
    /// </summary>
    public const string SectionName = "Plex";

    #endregion // Constants

    #region Properties

    /// <summary>
    /// Base URL of the Plex server (for example <c>http://plex:32400</c>)
    /// </summary>
    [Required(ErrorMessage = "Plex:BaseUrl is required (environment variable PLEXSYNC__Plex__BaseUrl).")]
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Plex authentication token sent as <c>X-Plex-Token</c>
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Optional explicit owner account id; when <c>null</c> it is auto-detected
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "Plex:OwnerAccountId must be at least 1 (environment variable PLEXSYNC__Plex__OwnerAccountId).")]
    public int? OwnerAccountId { get; set; }

    /// <summary>
    /// Optional list of library section keys to restrict synchronization to; empty means all libraries
    /// </summary>
    public string[] Libraries { get; set; } = [];

    #endregion // Properties

    #region IValidatableObject

    /// <inheritdoc/>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(BaseUrl))
        {
            yield break;
        }

        if (Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) == false
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || string.IsNullOrEmpty(uri.Host))
        {
            yield return new ValidationResult("Plex:BaseUrl must be an absolute http or https URL with a host, e.g. http://plex:32400 (environment variable PLEXSYNC__Plex__BaseUrl).",
                                              [nameof(BaseUrl)]);
        }
    }

    #endregion // IValidatableObject
}