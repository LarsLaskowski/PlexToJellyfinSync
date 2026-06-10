using System.Text.Json;
using System.Text.Json.Serialization;

namespace PlexToJellyfinSync.Service;

/// <summary>
/// Shared JSON options for deserializing Plex API responses
/// </summary>
public static class PlexJsonOptions
{
    #region Properties

    /// <summary>
    /// Default options. Case-sensitive on purpose so the lowercase legacy <c>guid</c> string is not
    /// mistaken for the uppercase <c>Guid</c> array, and numbers may be read from JSON strings
    /// </summary>
    public static JsonSerializerOptions Default { get; } = new()
                                                           {
                                                               PropertyNameCaseInsensitive = false,
                                                               NumberHandling = JsonNumberHandling.AllowReadingFromString
                                                           };

    #endregion // Properties
}