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

    #endregion // Properties

    #region IStateStore

    /// <summary>
    /// Return the stored high-water mark
    /// </summary>
    /// <param name="cancellationToken">Ignored cancellation token</param>
    /// <returns>The stored high-water mark, or <c>null</c></returns>
    public Task<DateTimeOffset?> GetHighWaterMarkAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(HighWaterMark);
    }

    /// <summary>
    /// Store the given high-water mark and record the write
    /// </summary>
    /// <param name="value">New high-water mark</param>
    /// <param name="cancellationToken">Ignored cancellation token</param>
    /// <returns>Returns a task representing the asynchronous operation</returns>
    public Task SetHighWaterMarkAsync(DateTimeOffset value, CancellationToken cancellationToken)
    {
        HighWaterMark = value;
        Writes.Add(value);

        return Task.CompletedTask;
    }

    #endregion // IStateStore
}