using System.Text.Json.Serialization;

namespace PlexToJellyfinSync.Data.Plex;

/// <summary>
/// Plex metadata element as returned by metadata and history endpoints
/// </summary>
public sealed class PlexMetadata
{
    #region Properties

    /// <summary>
    /// Rating key (unique id within the server)
    /// </summary>
    [JsonPropertyName("ratingKey")]
    public string? RatingKey { get; set; }

    /// <summary>
    /// Item type, for example <c>movie</c>, <c>episode</c>, <c>season</c> or <c>show</c>
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>
    /// Display title
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>
    /// Sort title
    /// </summary>
    [JsonPropertyName("titleSort")]
    public string? TitleSort { get; set; }

    /// <summary>
    /// Original title
    /// </summary>
    [JsonPropertyName("originalTitle")]
    public string? OriginalTitle { get; set; }

    /// <summary>
    /// Release year
    /// </summary>
    [JsonPropertyName("year")]
    public int? Year { get; set; }

    /// <summary>
    /// Plot / summary
    /// </summary>
    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    /// <summary>
    /// Studio
    /// </summary>
    [JsonPropertyName("studio")]
    public string? Studio { get; set; }

    /// <summary>
    /// Runtime in milliseconds
    /// </summary>
    [JsonPropertyName("duration")]
    public long? Duration { get; set; }

    /// <summary>
    /// Original air/premiere date (<c>yyyy-MM-dd</c>)
    /// </summary>
    [JsonPropertyName("originallyAvailableAt")]
    public string? OriginallyAvailableAt { get; set; }

    /// <summary>
    /// Date added to the library as unix epoch seconds
    /// </summary>
    [JsonPropertyName("addedAt")]
    public long? AddedAt { get; set; }

    /// <summary>
    /// View count
    /// </summary>
    [JsonPropertyName("viewCount")]
    public int? ViewCount { get; set; }

    /// <summary>
    /// Last viewed timestamp as unix epoch seconds
    /// </summary>
    [JsonPropertyName("lastViewedAt")]
    public long? LastViewedAt { get; set; }

    /// <summary>
    /// Viewed timestamp as unix epoch seconds (history entries)
    /// </summary>
    [JsonPropertyName("viewedAt")]
    public long? ViewedAt { get; set; }

    /// <summary>
    /// Account id (history entries)
    /// </summary>
    [JsonPropertyName("accountID")]
    public int? AccountId { get; set; }

    /// <summary>
    /// Episode index within a season
    /// </summary>
    [JsonPropertyName("index")]
    public int? Index { get; set; }

    /// <summary>
    /// Season index within a series
    /// </summary>
    [JsonPropertyName("parentIndex")]
    public int? ParentIndex { get; set; }

    /// <summary>
    /// Title of the owning series
    /// </summary>
    [JsonPropertyName("grandparentTitle")]
    public string? GrandparentTitle { get; set; }

    /// <summary>
    /// Rating key of the owning series
    /// </summary>
    [JsonPropertyName("grandparentRatingKey")]
    public string? GrandparentRatingKey { get; set; }

    /// <summary>
    /// Genres
    /// </summary>
    [JsonPropertyName("Genre")]
    public List<PlexTag>? Genre { get; set; }

    /// <summary>
    /// External identifiers
    /// </summary>
    [JsonPropertyName("Guid")]
    public List<PlexGuid>? Guid { get; set; }

    /// <summary>
    /// Media elements
    /// </summary>
    [JsonPropertyName("Media")]
    public List<PlexMedia>? Media { get; set; }

    #endregion // Properties
}