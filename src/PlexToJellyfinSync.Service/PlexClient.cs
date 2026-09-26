using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using PlexToJellyfinSync.Core.Abstractions;
using PlexToJellyfinSync.Core.Enums;
using PlexToJellyfinSync.Core.Models;
using PlexToJellyfinSync.Core.Options;
using PlexToJellyfinSync.Data.Plex;

namespace PlexToJellyfinSync.Service;

/// <summary>
/// Reads data from the Plex media server via its HTTP API
/// </summary>
public sealed class PlexClient : IPlexClient
{
    #region Constants

    /// <summary>
    /// Maximum accepted length for a rating key before an item is treated as invalid
    /// </summary>
    private const int MaxRatingKeyLength = 64;

    /// <summary>
    /// Maximum accepted length for a Plex file path before it is treated as invalid
    /// </summary>
    private const int MaxFilePathLength = 4096;

    /// <summary>
    /// Maximum accepted length for short text fields such as titles and the studio name
    /// </summary>
    private const int MaxShortTextLength = 512;

    /// <summary>
    /// Maximum accepted length for the plot / summary field
    /// </summary>
    private const int MaxPlotLength = 4000;

    /// <summary>
    /// Number of history entries requested per page
    /// </summary>
    private const int HistoryPageSize = 500;

    /// <summary>
    /// Number of items requested per page when reading a library section or a series' episodes
    /// </summary>
    private const int LibraryPageSize = 200;

    #endregion // Constants

    #region Fields

    private static readonly JsonSerializerOptions _jsonOptions = PlexJsonOptions.Default;

    private readonly HttpClient _httpClient;
    private readonly ILogger<PlexClient> _logger;
    private readonly PlexOptions _options;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="httpClient">Configured HTTP client</param>
    /// <param name="logger">Logging interface</param>
    /// <param name="options">Plex options</param>
    public PlexClient(HttpClient httpClient, ILogger<PlexClient> logger, IOptions<PlexOptions> options)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;
    }

    #endregion // Constructors

    #region Static methods

    /// <summary>
    /// Map a Plex item type string to a media kind
    /// </summary>
    /// <param name="type">Plex type string</param>
    /// <returns>Media kind</returns>
    private static MediaKind MapKind(string? type)
    {
        switch (type)
        {
            case "movie":
                {
                    return MediaKind.Movie;
                }

            case "episode":
                {
                    return MediaKind.Episode;
                }

            case "season":
                {
                    return MediaKind.Season;
                }

            case "show":
                {
                    return MediaKind.Series;
                }

            default:
                {
                    return MediaKind.Unknown;
                }
        }
    }

    /// <summary>
    /// Convert unix epoch seconds to a date/time offset
    /// </summary>
    /// <param name="epochSeconds">Epoch seconds, or <c>null</c></param>
    /// <returns>Date/time offset, or <c>null</c></returns>
    private static DateTimeOffset? FromEpoch(long? epochSeconds)
    {
        if (epochSeconds is null || epochSeconds.Value <= 0)
        {
            return null;
        }

        return DateTimeOffset.FromUnixTimeSeconds(epochSeconds.Value);
    }

    /// <summary>
    /// Parse the external identifiers of a Plex metadata element
    /// </summary>
    /// <param name="metadata">Plex metadata</param>
    /// <returns>List of unique identifiers</returns>
    private static List<UniqueId> ParseUniqueIds(PlexMetadata metadata)
    {
        var result = new List<UniqueId>();

        if (metadata.Guid is null)
        {
            return result;
        }

        var first = true;

        foreach (var guid in metadata.Guid)
        {
            if (string.IsNullOrWhiteSpace(guid.Id))
            {
                continue;
            }

            var separatorIndex = guid.Id.IndexOf("://", StringComparison.Ordinal);

            if (separatorIndex <= 0)
            {
                continue;
            }

            var type = guid.Id.Substring(0, separatorIndex);
            var value = guid.Id.Substring(separatorIndex + 3);
            var queryIndex = value.IndexOf('?', StringComparison.Ordinal);

            if (queryIndex >= 0)
            {
                value = value.Substring(0, queryIndex);
            }

            result.Add(new UniqueId
                       {
                           Type = type,
                           Value = value,
                           IsDefault = first
                       });

            first = false;
        }

        return result;
    }

    /// <summary>
    /// Check whether a rating key is present, within a sane length and free of control or whitespace characters
    /// </summary>
    /// <param name="ratingKey">Rating key to validate</param>
    /// <returns><c>true</c> if the rating key is valid</returns>
    private static bool IsValidRatingKey(string? ratingKey)
    {
        return string.IsNullOrWhiteSpace(ratingKey) == false
               && ratingKey.Length <= MaxRatingKeyLength
               && ratingKey.All(c => char.IsControl(c) == false && char.IsWhiteSpace(c) == false);
    }

    /// <summary>
    /// Validate a Plex file path, rejecting values that are too long or contain unsafe characters
    /// </summary>
    /// <param name="filePath">Raw file path reported by Plex</param>
    /// <returns>The file path, or <c>null</c> if it is missing or invalid</returns>
    private static string? SanitizeFilePath(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return null;
        }

        if (filePath.Length > MaxFilePathLength || filePath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            return null;
        }

        return filePath;
    }

    /// <summary>
    /// Cap a text field to a sane maximum length, never splitting a surrogate pair
    /// </summary>
    /// <param name="text">Raw text value</param>
    /// <param name="maxLength">Maximum accepted length</param>
    /// <returns>The text, truncated to <paramref name="maxLength"/> if necessary, or <c>null</c></returns>
    private static string? CapLength(string? text, int maxLength)
    {
        if (text is null || text.Length <= maxLength)
        {
            return text;
        }

        var end = maxLength > 0 && char.IsHighSurrogate(text[maxLength - 1]) ? maxLength - 1 : maxLength;

        return text.Substring(0, end);
    }

    /// <summary>
    /// Filter and append the valid, newer-than-"since" entries of a history page to the result, for
    /// the requested account
    /// </summary>
    /// <param name="page">Raw history entries of the page</param>
    /// <param name="since">Lower bound (exclusive) for the viewed-at timestamp</param>
    /// <param name="accountId">Account id to filter for</param>
    /// <param name="result">List to append matching entries to</param>
    /// <returns><c>true</c> if an entry at or before "since" was seen on this page</returns>
    private static bool AppendHistoryPage(IReadOnlyList<PlexMetadata> page, DateTimeOffset since, int accountId, List<PlexHistoryEntry> result)
    {
        var reachedSince = false;

        foreach (var entry in page)
        {
            if (entry.ViewedAt is not > 0 || string.IsNullOrWhiteSpace(entry.RatingKey))
            {
                continue;
            }

            var viewedAt = DateTimeOffset.FromUnixTimeSeconds(entry.ViewedAt.Value);

            if (viewedAt <= since)
            {
                reachedSince = true;

                continue;
            }

            var entryAccountId = entry.AccountId ?? accountId;

            if (entryAccountId == accountId)
            {
                result.Add(new PlexHistoryEntry
                           {
                               RatingKey = entry.RatingKey,
                               AccountId = entryAccountId,
                               ViewedAt = viewedAt
                           });
            }
        }

        return reachedSince;
    }

    /// <summary>
    /// Rating key of the first entry of a page, used to detect a server that ignores the paging
    /// parameters and keeps returning the same page
    /// </summary>
    /// <param name="page">Page of metadata entries</param>
    /// <returns>The rating key of the first entry, or <c>null</c> if the page is empty or unkeyed</returns>
    private static string? FirstRatingKeyOf(List<PlexMetadata> page)
    {
        return page.Count > 0 ? page[0].RatingKey : null;
    }

    /// <summary>
    /// Rating key and viewed-at timestamp of the first entry of a history page, used to detect a
    /// server that ignores the paging parameters and keeps returning the same page. Unlike a plain
    /// rating key, this pair stays distinct across two genuinely different pages even when an item
    /// was watched more than once, since a rewatch repeats the rating key with a different
    /// viewed-at timestamp
    /// </summary>
    /// <param name="page">Page of history entries</param>
    /// <returns>The rating key and viewed-at timestamp of the first entry, or <c>null</c> if the page is empty</returns>
    private static (string? RatingKey, long? ViewedAt)? FirstHistoryPageKeyOf(List<PlexMetadata> page)
    {
        return page.Count > 0 ? (page[0].RatingKey, page[0].ViewedAt) : null;
    }

    #endregion // Static methods

    #region Methods

    /// <summary>
    /// Resolve the rating key of the owning series, dropping and logging it if it is present but invalid
    /// </summary>
    /// <param name="metadata">Plex metadata</param>
    /// <returns>The show rating key, or <c>null</c> if it is missing or invalid</returns>
    private string? ResolveShowRatingKey(PlexMetadata metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata.GrandparentRatingKey))
        {
            return null;
        }

        if (IsValidRatingKey(metadata.GrandparentRatingKey))
        {
            return metadata.GrandparentRatingKey;
        }

        _logger.LogWarning("Item {RatingKey} reported an invalid show rating key, dropping it", metadata.RatingKey);

        return null;
    }

    /// <summary>
    /// Map a Plex metadata element to a media item
    /// </summary>
    /// <param name="metadata">Plex metadata</param>
    /// <returns>Media item, or <c>null</c> if the metadata does not carry a valid rating key</returns>
    private MediaItem? MapMediaItem(PlexMetadata metadata)
    {
        if (IsValidRatingKey(metadata.RatingKey) == false)
        {
            _logger.LogWarning("Skipping a Plex item with a missing or invalid rating key");

            return null;
        }

        var kind = MapKind(metadata.Type);
        var filePath = metadata.Media?.FirstOrDefault()
                                     ?.Part
                                     ?.FirstOrDefault()
                                     ?.File;
        var sanitizedFilePath = SanitizeFilePath(filePath);

        if (sanitizedFilePath is null && string.IsNullOrWhiteSpace(filePath) == false)
        {
            _logger.LogWarning("Item {RatingKey} reported an invalid file path, treating it as missing", metadata.RatingKey);
        }

        var showRatingKey = ResolveShowRatingKey(metadata);

        var item = new MediaItem
                   {
                       RatingKey = metadata.RatingKey!,
                       Kind = kind,
                       Title = CapLength(metadata.Title, MaxShortTextLength) ?? string.Empty,
                       OriginalTitle = CapLength(metadata.OriginalTitle, MaxShortTextLength),
                       SortTitle = CapLength(metadata.TitleSort, MaxShortTextLength),
                       Year = metadata.Year,
                       Plot = CapLength(metadata.Summary, MaxPlotLength),
                       Studio = CapLength(metadata.Studio, MaxShortTextLength),
                       FilePath = sanitizedFilePath,
                       ShowTitle = CapLength(metadata.GrandparentTitle, MaxShortTextLength),
                       ShowRatingKey = showRatingKey,
                       DateAdded = FromEpoch(metadata.AddedAt),
                       UniqueIds = ParseUniqueIds(metadata)
                   };

        if (metadata.Duration is > 0)
        {
            item.RuntimeMinutes = (int)(metadata.Duration.Value / 60000);
        }

        if (metadata.Genre is not null)
        {
            item.Genres = metadata.Genre
                                  .Where(g => string.IsNullOrWhiteSpace(g.Tag) == false)
                                  .Select(g => g.Tag!)
                                  .ToList();
        }

        if (string.IsNullOrWhiteSpace(metadata.OriginallyAvailableAt) == false
            && DateTimeOffset.TryParse(metadata.OriginallyAvailableAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var premiered))
        {
            item.Premiered = premiered;
        }

        if (kind == MediaKind.Episode)
        {
            item.SeasonNumber = metadata.ParentIndex;
            item.EpisodeNumber = metadata.Index;
        }
        else if (kind == MediaKind.Season)
        {
            item.SeasonNumber = metadata.Index;
        }

        item.Watch = new WatchInfo
                     {
                         PlayCount = metadata.ViewCount ?? 0,
                         Watched = (metadata.ViewCount ?? 0) > 0,
                         LastPlayed = FromEpoch(metadata.LastViewedAt)
                     };

        return item;
    }

    /// <summary>
    /// Stream every page of a metadata listing endpoint and map the entries to media items, requesting the next
    /// page only once the caller has consumed the current one
    /// </summary>
    /// <param name="baseUrl">Relative request URL, without paging parameters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The mapped media items</returns>
    private async IAsyncEnumerable<MediaItem> GetPagedMediaItemsAsync(string baseUrl, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var start = 0;
        string? previousFirstRatingKey = null;

        while (true)
        {
            var url = $"{baseUrl}?X-Plex-Container-Start={start}&X-Plex-Container-Size={LibraryPageSize}";
            var response = await GetAsync<PlexMetadataResponse>(url, cancellationToken).ConfigureAwait(false);
            var entries = response?.MediaContainer?.Metadata;

            if (entries is null || entries.Count == 0)
            {
                yield break;
            }

            var firstRatingKey = FirstRatingKeyOf(entries);

            if (firstRatingKey is not null && firstRatingKey == previousFirstRatingKey)
            {
                _logger.LogWarning("Plex server returned a non-advancing page for {BaseUrl}, stopping pagination", baseUrl);

                yield break;
            }

            previousFirstRatingKey = firstRatingKey;

            foreach (var item in entries.Select(MapMediaItem).OfType<MediaItem>())
            {
                yield return item;
            }

            if (entries.Count < LibraryPageSize)
            {
                yield break;
            }

            start += LibraryPageSize;
        }
    }

    /// <summary>
    /// Perform a GET request and deserialize the JSON response
    /// </summary>
    /// <typeparam name="T">Response type</typeparam>
    /// <param name="url">Relative request URL</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Deserialized response, or <c>null</c></returns>
    private async Task<T?> GetAsync<T>(string url, CancellationToken cancellationToken)
        where T : class
    {
        using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

        return await JsonSerializer.DeserializeAsync<T>(stream, _jsonOptions, cancellationToken).ConfigureAwait(false);
    }

    #endregion // Methods

    #region IPlexClient

    /// <inheritdoc/>
    public async Task<int> GetOwnerAccountIdAsync(CancellationToken cancellationToken)
    {
        if (_options.OwnerAccountId is > 0)
        {
            return _options.OwnerAccountId.Value;
        }

        try
        {
            var response = await GetAsync<PlexAccountsResponse>("/accounts", cancellationToken).ConfigureAwait(false);
            var accounts = response?.MediaContainer?.Account;

            if (accounts is not null)
            {
                var owner = accounts.Where(a => a.Id > 0)
                                    .OrderBy(a => a.Id)
                                    .FirstOrDefault();

                if (owner is not null)
                {
                    return owner.Id;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            _logger.LogWarning(ex, "Plex rejected the request to auto-detect the owner account id, check the configured token");

            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not auto-detect the Plex owner account id, defaulting to 1");
        }

        return 1;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PlexLibrary>> GetLibrariesAsync(CancellationToken cancellationToken)
    {
        var response = await GetAsync<PlexLibrariesResponse>("/library/sections", cancellationToken).ConfigureAwait(false);
        var directories = response?.MediaContainer?.Directory;

        if (directories is null)
        {
            return [];
        }

        return directories.Where(d => string.IsNullOrWhiteSpace(d.Key) == false)
                          .Select(d => new PlexLibrary
                                       {
                                           Key = d.Key!,
                                           Title = d.Title ?? string.Empty,
                                           Kind = MapKind(d.Type)
                                       })
                          .ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PlexHistoryEntry>> GetHistorySinceAsync(DateTimeOffset since, int accountId, CancellationToken cancellationToken)
    {
        var result = new List<PlexHistoryEntry>();
        var start = 0;
        (string? RatingKey, long? ViewedAt)? previousFirstEntry = null;

        while (true)
        {
            var url = $"/status/sessions/history/all?sort=viewedAt:desc&accountID={accountId}&X-Plex-Container-Start={start}&X-Plex-Container-Size={HistoryPageSize}";
            var response = await GetAsync<PlexMetadataResponse>(url, cancellationToken).ConfigureAwait(false);
            var entries = response?.MediaContainer?.Metadata;

            if (entries is null || entries.Count == 0)
            {
                break;
            }

            var firstEntry = FirstHistoryPageKeyOf(entries);

            if (previousFirstEntry is not null && firstEntry == previousFirstEntry)
            {
                _logger.LogWarning("Plex server returned a non-advancing history page, stopping pagination");

                break;
            }

            previousFirstEntry = firstEntry;

            // Entries are sorted descending by viewed-at, so once one entry on a page is at or
            // before "since", later pages would be too; stop paging after this page.
            var reachedSince = AppendHistoryPage(entries, since, accountId, result);

            if (reachedSince || entries.Count < HistoryPageSize)
            {
                break;
            }

            start += HistoryPageSize;
        }

        return result.OrderBy(e => e.ViewedAt).ToList();
    }

    /// <inheritdoc/>
    public async Task<MediaItem?> GetMediaItemAsync(string ratingKey, CancellationToken cancellationToken)
    {
        var response = await GetAsync<PlexMetadataResponse>($"/library/metadata/{ratingKey}", cancellationToken).ConfigureAwait(false);
        var metadata = response?.MediaContainer?.Metadata?.FirstOrDefault();

        if (metadata is null)
        {
            return null;
        }

        return MapMediaItem(metadata);
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<MediaItem> GetEpisodesAsync(string showRatingKey, CancellationToken cancellationToken)
    {
        return GetPagedMediaItemsAsync($"/library/metadata/{showRatingKey}/allLeaves", cancellationToken);
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<MediaItem> GetLibraryItemsAsync(string libraryKey, CancellationToken cancellationToken)
    {
        return GetPagedMediaItemsAsync($"/library/sections/{libraryKey}/all", cancellationToken);
    }

    #endregion // IPlexClient
}