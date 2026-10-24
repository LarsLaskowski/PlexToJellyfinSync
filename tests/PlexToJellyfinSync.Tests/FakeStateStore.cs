using PlexToJellyfinSync.Core.Abstractions;

namespace PlexToJellyfinSync.Tests;

/// <summary>
/// State store stub that keeps the high-water mark in memory and records every write
/// </summary>
internal sealed class FakeStateStore : IStateStore
{
    #region Properties

    /// <summary>
    /// Currently stored high-water mark
    /// </summary>
    public DateTimeOffset? HighWaterMark { get; set; }

    /// <summary>
    /// All values that have been persisted, in order
    /// </summary>
    public List<DateTimeOffset> Writes { get; } = [];

    /// <summary>
    /// Exception thrown when a high-water mark is written, if any; the stored mark stays unchanged then
    /// </summary>
    public Exception? SetException { get; set; }

    #endregion // Properties

    #region IStateStore

    /// <inheritdoc/>
    public Task<DateTimeOffset?> GetHighWaterMarkAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(HighWaterMark);
    }

    /// <inheritdoc/>
    public Task SetHighWaterMarkAsync(DateTimeOffset value, CancellationToken cancellationToken)
    {
        if (SetException is not null)
        {
            throw SetException;
        }

        HighWaterMark = value;
        Writes.Add(value);

        return Task.CompletedTask;
    }

    #endregion // IStateStore
}