using Microsoft.Extensions.Logging;

using PlexToJellyfinSync.Core.Abstractions;
using PlexToJellyfinSync.Core.Enums;
using PlexToJellyfinSync.Core.Models;

namespace PlexToJellyfinSync.Service;

/// <summary>
/// Writes the watch state of a single media item into its NFO file and counts the outcome
/// </summary>
public sealed class MediaItemWriter : IMediaItemWriter
{
    #region Fields

    private readonly INfoWriter _nfoWriter;
    private readonly IPathMapper _pathMapper;
    private readonly ISyncStatusProvider _status;
    private readonly ILogger<MediaItemWriter> _logger;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="nfoWriter">NFO writer</param>
    /// <param name="pathMapper">Path mapper</param>
    /// <param name="status">Status provider</param>
    /// <param name="logger">Logging interface</param>
    public MediaItemWriter(INfoWriter nfoWriter,
                           IPathMapper pathMapper,
                           ISyncStatusProvider status,
                           ILogger<MediaItemWriter> logger)
    {
        _nfoWriter = nfoWriter;
        _pathMapper = pathMapper;
        _status = status;
        _logger = logger;
    }

    #endregion // Constructors

    #region Methods

    /// <summary>
    /// Record an NFO write outcome in the status
    /// </summary>
    /// <param name="outcome">Write outcome</param>
    private void RecordOutcome(NfoWriteOutcome outcome)
    {
        if (outcome == NfoWriteOutcome.Created)
        {
            _status.Update(s => s.NfoCreated++);
        }
        else if (outcome == NfoWriteOutcome.Updated)
        {
            _status.Update(s => s.NfoUpdated++);
        }
    }

    #endregion // Methods

    #region IMediaItemWriter

    /// <inheritdoc />
    public async Task WriteItemAsync(MediaItem item, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(item.FilePath))
        {
            _logger.LogWarning("Item {RatingKey} ({Title}) has no file path, skipping", item.RatingKey, item.Title);

            return;
        }

        var localPath = _pathMapper.MapToLocal(item.FilePath);

        if (localPath is null)
        {
            _logger.LogWarning("No path mapping for {FilePath}, skipping", item.FilePath);

            return;
        }

        var outcome = await _nfoWriter.WriteAsync(item, localPath, cancellationToken).ConfigureAwait(false);

        RecordOutcome(outcome);
    }

    /// <inheritdoc />
    public async Task WriteAggregateAsync(MediaItem item, string localDirectory, CancellationToken cancellationToken)
    {
        var outcome = await _nfoWriter.WriteAsync(item, localDirectory, cancellationToken).ConfigureAwait(false);

        RecordOutcome(outcome);
    }

    #endregion // IMediaItemWriter
}