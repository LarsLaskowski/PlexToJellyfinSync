using System.Globalization;
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
    /// Map a Plex metadata element to a media item
    /// </summary>
    /// <param name="metadata">Plex metadata</param>
    /// <returns>Media item</returns>
    private static MediaItem MapMediaItem(PlexMetadata metadata)
    {
        var kind = MapKind(metadata.Type);
        var filePath = metadata.Media?.FirstOrDefault()?
        .Part?
        .FirstOrDefault()?
        .File;

        var item = new MediaItem
                   {
                       RatingKey = metadata.RatingKey ?? string.Empty,
                       Kind = kind,
                       Title = metadata.Title ?? string.Empty,
                       OriginalTitle = metadata.OriginalTitle,
                       SortTitle = metadata.TitleSort,
                       Year = metadata.Year,
                       Plot = metadata.Summary,
                       Studio = metadata.Studio,
                       FilePath = filePath,
                       ShowTitle = metadata.GrandparentTitle,
                       ShowRatingKey = metadata.GrandparentRatingKey,
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

    #endregion // Static methods

    #region Methods

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
        catch (Exception ex)
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
        var url = $"/status/sessions/history/all?sort=viewedAt:desc&accountID={accountId}&X-Plex-Container-Start=0&X-Plex-Container-Size=500";
        var response = await GetAsync<PlexMetadataResponse>(url, cancellationToken).ConfigureAwait(false);
        var entries = response?.MediaContainer?.Metadata;

        if (entries is null)
        {
            return [];
        }

        return entries.Where(e => e.ViewedAt is > 0 && string.IsNullOrWhiteSpace(e.RatingKey) == false)
                      .Select(e => new PlexHistoryEntry
                                   {
                                       RatingKey = e.RatingKey!,
                                       AccountId = e.AccountId ?? accountId,
                                       ViewedAt = DateTimeOffset.FromUnixTimeSeconds(e.ViewedAt!.Value)
                                   })
                      .Where(e => e.ViewedAt > since && e.AccountId == accountId)
                      .OrderBy(e => e.ViewedAt)
                      .ToList();
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
    public async Task<IReadOnlyList<MediaItem>> GetEpisodesAsync(string showRatingKey, CancellationToken cancellationToken)
    {
        var response = await GetAsync<PlexMetadataResponse>($"/library/metadata/{showRatingKey}/allLeaves", cancellationToken).ConfigureAwait(false);
        var entries = response?.MediaContainer?.Metadata;

        if (entries is null)
        {
            return [];
        }

        return entries.Select(MapMediaItem).ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<MediaItem>> GetLibraryItemsAsync(string libraryKey, CancellationToken cancellationToken)
    {
        var response = await GetAsync<PlexMetadataResponse>($"/library/sections/{libraryKey}/all", cancellationToken).ConfigureAwait(false);
        var entries = response?.MediaContainer?.Metadata;

        if (entries is null)
        {
            return [];
        }

        return entries.Select(MapMediaItem).ToList();
    }

    #endregion // IPlexClient
}