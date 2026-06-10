using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using PlexToJellyfinSync.Core.Abstractions;
using PlexToJellyfinSync.Core.Enums;
using PlexToJellyfinSync.Core.Models;
using PlexToJellyfinSync.Core.Options;

namespace PlexToJellyfinSync.Service;

/// <summary>
/// Creates or updates Jellyfin NFO files for media items
/// </summary>
public sealed class NfoWriter : INfoWriter
{
    #region Fields

    private static readonly UTF8Encoding _utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly NfoOptions _nfoOptions;
    private readonly SyncOptions _syncOptions;
    private readonly ILogger<NfoWriter> _logger;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="nfoOptions">NFO options</param>
    /// <param name="syncOptions">Sync options</param>
    /// <param name="logger">Logging interface</param>
    public NfoWriter(IOptions<NfoOptions> nfoOptions, IOptions<SyncOptions> syncOptions, ILogger<NfoWriter> logger)
    {
        _nfoOptions = nfoOptions.Value;
        _syncOptions = syncOptions.Value;
        _logger = logger;
    }

    #endregion // Constructors

    #region Static methods

    /// <summary>
    /// Get the NFO root element name for a media kind
    /// </summary>
    /// <param name="kind">Media kind</param>
    /// <returns>Root element name</returns>
    private static string GetRootName(MediaKind kind)
    {
        switch (kind)
        {
            case MediaKind.Movie:
                {
                    return "movie";
                }

            case MediaKind.Episode:
                {
                    return "episodedetails";
                }

            case MediaKind.Season:
                {
                    return "season";
                }

            case MediaKind.Series:
                {
                    return "tvshow";
                }

            default:
                {
                    return "movie";
                }
        }
    }

    /// <summary>
    /// Set or add a child element and report whether a change occurred
    /// </summary>
    /// <param name="root">Root element</param>
    /// <param name="name">Child element name</param>
    /// <param name="value">Value to set</param>
    /// <returns>True if the element was added or changed</returns>
    private static bool SetChild(XElement root, string name, string value)
    {
        var element = root.Element(name);

        if (element is null)
        {
            root.Add(new XElement(name, value));

            return true;
        }

        if (string.Equals(element.Value, value, StringComparison.Ordinal))
        {
            return false;
        }

        element.Value = value;

        return true;
    }

    /// <summary>
    /// Add a child element when the value is not empty
    /// </summary>
    /// <param name="root">Root element</param>
    /// <param name="name">Child element name</param>
    /// <param name="value">Value to add</param>
    private static void AddIfNotEmpty(XElement root, string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value) == false)
        {
            root.Add(new XElement(name, value));
        }
    }

    #endregion // Static methods

    #region Methods

    /// <summary>
    /// Serialize an NFO document to disk
    /// </summary>
    /// <param name="document">Document to serialize</param>
    /// <param name="path">Target path</param>
    /// <param name="indent">Whether to indent the output</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task</returns>
    private static async Task SaveAsync(XDocument document, string path, bool indent, CancellationToken cancellationToken)
    {
        var settings = new XmlWriterSettings
                       {
                           Async = true,
                           Encoding = _utf8NoBom,
                           Indent = indent,
                           OmitXmlDeclaration = false
                       };

        await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        await using var writer = XmlWriter.Create(stream, settings);

        await document.SaveAsync(writer, cancellationToken).ConfigureAwait(false);
        await writer.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Resolve the target NFO file path for an item
    /// </summary>
    /// <param name="item">Media item</param>
    /// <param name="localPath">Local media file path or directory</param>
    /// <returns>Target NFO file path</returns>
    private string ResolveTargetPath(MediaItem item, string localPath)
    {
        switch (item.Kind)
        {
            case MediaKind.Movie:
                {
                    return ResolveMoviePath(localPath);
                }

            case MediaKind.Episode:
                {
                    return Path.ChangeExtension(localPath, ".nfo");
                }

            case MediaKind.Season:
                {
                    return Path.Combine(localPath, "season.nfo");
                }

            case MediaKind.Series:
                {
                    return Path.Combine(localPath, "tvshow.nfo");
                }

            default:
                {
                    return Path.ChangeExtension(localPath, ".nfo");
                }
        }
    }

    /// <summary>
    /// Resolve the NFO file path for a movie based on the configured strategy
    /// </summary>
    /// <param name="localPath">Local media file path</param>
    /// <returns>Target NFO file path</returns>
    private string ResolveMoviePath(string localPath)
    {
        var directory = Path.GetDirectoryName(localPath) ?? string.Empty;
        var movieNfo = Path.Combine(directory, "movie.nfo");
        var videoNfo = Path.ChangeExtension(localPath, ".nfo");

        switch (_nfoOptions.MovieFilenameStrategy)
        {
            case MovieNfoFilenameStrategy.MovieNfo:
                {
                    return movieNfo;
                }

            case MovieNfoFilenameStrategy.VideoFileName:
                {
                    return videoNfo;
                }

            default:
                {
                    if (File.Exists(movieNfo))
                    {
                        return movieNfo;
                    }

                    return videoNfo;
                }
        }
    }

    /// <summary>
    /// Apply the watch state to a root element
    /// </summary>
    /// <param name="root">Root element</param>
    /// <param name="watch">Watch state</param>
    /// <returns>True if any value changed</returns>
    private bool ApplyWatchState(XElement root, WatchInfo watch)
    {
        var changed = SetChild(root, "watched", watch.Watched ? "true" : "false");
        changed |= SetChild(root, "playcount", watch.PlayCount.ToString(CultureInfo.InvariantCulture));

        if (watch.LastPlayed.HasValue)
        {
            changed |= SetChild(root, "lastplayed", watch.LastPlayed.Value.LocalDateTime.ToString(_nfoOptions.DateTimeFormat, CultureInfo.InvariantCulture));
        }

        return changed;
    }

    /// <summary>
    /// Build a complete NFO document for an item
    /// </summary>
    /// <param name="item">Media item</param>
    /// <returns>NFO document</returns>
    private XDocument BuildDocument(MediaItem item)
    {
        var root = new XElement(GetRootName(item.Kind));

        AddIfNotEmpty(root, "title", item.Title);
        AddIfNotEmpty(root, "originaltitle", item.OriginalTitle);
        AddIfNotEmpty(root, "sorttitle", item.SortTitle);

        if (item.Kind == MediaKind.Episode)
        {
            if (item.SeasonNumber.HasValue)
            {
                root.Add(new XElement("season", item.SeasonNumber.Value.ToString(CultureInfo.InvariantCulture)));
            }

            if (item.EpisodeNumber.HasValue)
            {
                root.Add(new XElement("episode", item.EpisodeNumber.Value.ToString(CultureInfo.InvariantCulture)));
            }
        }

        if (item.Kind == MediaKind.Season && item.SeasonNumber.HasValue)
        {
            root.Add(new XElement("seasonnumber", item.SeasonNumber.Value.ToString(CultureInfo.InvariantCulture)));
        }

        if (item.Year.HasValue)
        {
            root.Add(new XElement("year", item.Year.Value.ToString(CultureInfo.InvariantCulture)));
        }

        AddIfNotEmpty(root, "plot", item.Plot);

        if (item.RuntimeMinutes.HasValue)
        {
            root.Add(new XElement("runtime", item.RuntimeMinutes.Value.ToString(CultureInfo.InvariantCulture)));
        }

        foreach (var genre in item.Genres)
        {
            AddIfNotEmpty(root, "genre", genre);
        }

        AddIfNotEmpty(root, "studio", item.Studio);

        if (item.Premiered.HasValue)
        {
            var formatted = item.Premiered.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            root.Add(new XElement(item.Kind == MediaKind.Episode ? "aired" : "premiered", formatted));
        }

        foreach (var uniqueId in item.UniqueIds)
        {
            var element = new XElement("uniqueid", uniqueId.Value);

            element.SetAttributeValue("type", uniqueId.Type);

            if (uniqueId.IsDefault)
            {
                element.SetAttributeValue("default", "true");
            }

            root.Add(element);
        }

        if (item.Kind == MediaKind.Movie && item.DateAdded.HasValue)
        {
            root.Add(new XElement("dateadded", item.DateAdded.Value.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)));
        }

        ApplyWatchState(root, item.Watch);

        return new XDocument(new XDeclaration("1.0", "utf-8", null), root);
    }

    #endregion // Methods

    #region INfoWriter

    /// <inheritdoc/>
    public async Task<NfoWriteOutcome> WriteAsync(MediaItem item, string localPath, CancellationToken cancellationToken)
    {
        var targetPath = ResolveTargetPath(item, localPath);

        if (File.Exists(targetPath))
        {
            var xml = await File.ReadAllTextAsync(targetPath, cancellationToken).ConfigureAwait(false);
            var document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
            var root = document.Root;

            if (root is null)
            {
                document = BuildDocument(item);
                await SaveAsync(document, targetPath, indent: true, cancellationToken).ConfigureAwait(false);

                return NfoWriteOutcome.Updated;
            }

            var changed = ApplyWatchState(root, item.Watch);

            if (changed == false)
            {
                return NfoWriteOutcome.Skipped;
            }

            await SaveAsync(document, targetPath, indent: false, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Updated watch state in {Path}", targetPath);

            return NfoWriteOutcome.Updated;
        }

        if (_syncOptions.CreateMissingNfo == false)
        {
            return NfoWriteOutcome.Skipped;
        }

        var directory = Path.GetDirectoryName(targetPath);

        if (string.IsNullOrEmpty(directory) == false && Directory.Exists(directory) == false)
        {
            Directory.CreateDirectory(directory);
        }

        var newDocument = BuildDocument(item);

        await SaveAsync(newDocument, targetPath, indent: true, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Created NFO {Path}", targetPath);

        return NfoWriteOutcome.Created;
    }

    #endregion // INfoWriter
}