using Microsoft.Extensions.Logging;

using PlexToJellyfinSync.Core.Abstractions;
using PlexToJellyfinSync.Core.Models;

namespace PlexToJellyfinSync.Service;

/// <summary>
/// Writes the watch state of a single media item into its NFO file and counts the outcome
/// </summary>
public sealed class MediaItemWriter : IMediaItemWriter
{
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
    }

    #endregion // Constructors

    #region IMediaItemWriter

    /// <inheritdoc />
    public Task WriteItemAsync(MediaItem item, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Task WriteAggregateAsync(MediaItem item, string localDirectory, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    #endregion // IMediaItemWriter
}