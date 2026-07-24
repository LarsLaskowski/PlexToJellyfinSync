using PlexToJellyfinSync.Core.Abstractions;
using PlexToJellyfinSync.Core.Enums;
using PlexToJellyfinSync.Core.Models;

namespace PlexToJellyfinSync.Tests;

/// <summary>
/// NFO writer stub that records every write and returns a preconfigured outcome
/// </summary>
internal sealed class RecordingNfoWriter : INfoWriter
{
    #region Properties

    /// <summary>
    /// Outcome returned for every write
    /// </summary>
    public NfoWriteOutcome Outcome { get; set; } = NfoWriteOutcome.Created;

    /// <summary>
    /// All captured writes, in order
    /// </summary>
    public List<NfoWriteRecord> Writes { get; } = [];

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Get the captured writes of a given media kind
    /// </summary>
    /// <param name="kind">Media kind to filter for</param>
    /// <returns>The matching writes</returns>
    public List<NfoWriteRecord> WritesOf(MediaKind kind)
    {
        return Writes.Where(write => write.Item.Kind == kind).ToList();
    }

    #endregion // Methods

    #region INfoWriter

    /// <summary>
    /// Record the write and return the preconfigured outcome
    /// </summary>
    /// <param name="item">Media item to write</param>
    /// <param name="localPath">Local path of the media file or directory</param>
    /// <param name="cancellationToken">Ignored cancellation token</param>
    /// <returns>The preconfigured outcome</returns>
    public Task<NfoWriteOutcome> WriteAsync(MediaItem item, string localPath, CancellationToken cancellationToken)
    {
        Writes.Add(new NfoWriteRecord
                   {
                       Item = item,
                       LocalPath = localPath
                   });

        return Task.FromResult(Outcome);
    }

    #endregion // INfoWriter
}