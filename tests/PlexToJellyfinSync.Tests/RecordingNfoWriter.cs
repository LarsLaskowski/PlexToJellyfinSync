using PlexToJellyfinSync.Core.Abstractions;
using PlexToJellyfinSync.Core.Enums;
using PlexToJellyfinSync.Core.Models;

namespace PlexToJellyfinSync.Tests;

/// <summary>
/// NFO writer stub that records every write and returns a preconfigured outcome
/// </summary>
internal sealed class RecordingNfoWriter : INfoWriter
{
    #region Fields

    private readonly Lock _lock = new();
    private readonly Dictionary<string, int> _concurrentCallsByPath = new(StringComparer.Ordinal);

    private TaskCompletionSource<bool> _concurrencyGateReleased = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _concurrentCalls;

    #endregion // Fields

    #region Properties

    /// <summary>
    /// Outcome returned for every write
    /// </summary>
    public NfoWriteOutcome Outcome { get; set; } = NfoWriteOutcome.Created;

    /// <summary>
    /// Number of writes to wait for in flight at once before releasing every waiting write, so an actual overflow
    /// past a configured bound is caught deterministically instead of through an artificial delay; zero disables
    /// the gate. The gate re-arms as soon as the in-flight count drops back below the threshold after a release,
    /// so a later, independent batch of concurrent writes is still forced through its own rendezvous rather than
    /// passing through unchecked. A write that never sees this many concurrent calls times out after
    /// <see cref="ConcurrencyGateTimeout"/> and proceeds anyway, so a correctly bounded caller only pays that
    /// timeout instead of hanging.
    /// </summary>
    public int ConcurrencyGate { get; set; }

    /// <summary>
    /// How long a write waits for <see cref="ConcurrencyGate"/> to be reached before proceeding regardless
    /// </summary>
    public TimeSpan ConcurrencyGateTimeout { get; set; } = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Highest number of writes observed in flight at the same time, across every target path
    /// </summary>
    public int MaxObservedConcurrency { get; private set; }

    /// <summary>
    /// Highest number of writes observed in flight at the same time, keyed by target local path
    /// </summary>
    public Dictionary<string, int> MaxObservedConcurrencyByPath { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// All captured writes, in order they completed
    /// </summary>
    public List<NfoWriteRecord> Writes { get; } = [];

    /// <summary>
    /// Exceptions to throw instead of recording the write, keyed by the item's rating key
    /// </summary>
    public Dictionary<string, Exception> FailuresByRatingKey { get; } = new(StringComparer.Ordinal);

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
    /// Record the write and return the preconfigured outcome, or throw the configured failure for this item
    /// </summary>
    /// <param name="item">Media item to write</param>
    /// <param name="localPath">Local path of the media file or directory</param>
    /// <param name="cancellationToken">Cancellation token honored while waiting for the configured <see cref="ConcurrencyGate"/></param>
    /// <returns>The preconfigured outcome</returns>
    public async Task<NfoWriteOutcome> WriteAsync(MediaItem item, string localPath, CancellationToken cancellationToken)
    {
        Task? gateTask = null;

        lock (_lock)
        {
            _concurrentCalls++;
            MaxObservedConcurrency = Math.Max(MaxObservedConcurrency, _concurrentCalls);

            var concurrentForPath = _concurrentCallsByPath.GetValueOrDefault(localPath) + 1;

            _concurrentCallsByPath[localPath] = concurrentForPath;
            MaxObservedConcurrencyByPath[localPath] = Math.Max(MaxObservedConcurrencyByPath.GetValueOrDefault(localPath), concurrentForPath);

            if (ConcurrencyGate > 0)
            {
                if (_concurrentCalls >= ConcurrencyGate)
                {
                    _concurrencyGateReleased.TrySetResult(true);
                }
                else
                {
                    gateTask = _concurrencyGateReleased.Task;
                }
            }
        }

        try
        {
            if (gateTask is not null)
            {
                try
                {
                    await gateTask.WaitAsync(ConcurrencyGateTimeout, cancellationToken).ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    // No further concurrent write arrived within the window; proceed as if the gate were
                    // disabled so a correctly bounded caller never blocks needlessly on it.
                }
            }

            if (FailuresByRatingKey.TryGetValue(item.RatingKey, out var failure))
            {
                throw failure;
            }

            lock (_lock)
            {
                Writes.Add(new NfoWriteRecord
                           {
                               Item = item,
                               LocalPath = localPath
                           });
            }

            return Outcome;
        }
        finally
        {
            lock (_lock)
            {
                _concurrentCalls--;
                _concurrentCallsByPath[localPath]--;

                if (ConcurrencyGate > 0 && _concurrentCalls < ConcurrencyGate && _concurrencyGateReleased.Task.IsCompleted)
                {
                    // Re-arm for the next batch of concurrent writes so it is forced through its own
                    // rendezvous instead of passing straight through the already-released gate.
                    _concurrencyGateReleased = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                }
            }
        }
    }

    #endregion // INfoWriter
}