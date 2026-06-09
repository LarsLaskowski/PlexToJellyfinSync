using PlexToJellyfinSync.Core.Models;

namespace PlexToJellyfinSync.Service;

/// <summary>
/// Aggregates episode watch states into season and series watch states
/// </summary>
public sealed class WatchAggregator
{
    #region Methods

    /// <summary>
    /// Aggregate the watch states of child items
    /// </summary>
    /// <param name="children">Child watch states (for example episodes of a season)</param>
    /// <returns>Aggregated watch state</returns>
    public WatchInfo Aggregate(IReadOnlyCollection<WatchInfo> children)
    {
        if (children.Count == 0)
        {
            return new WatchInfo();
        }

        var allWatched = children.All(c => c.Watched);
        var lastPlayed = children.Where(c => c.LastPlayed.HasValue)
                                 .Select(c => c.LastPlayed!.Value)
                                 .DefaultIfEmpty()
                                 .Max();

        return new WatchInfo
               {
                   Watched = allWatched,
                   PlayCount = allWatched ? 1 : 0,
                   LastPlayed = lastPlayed == default
                                    ? null
                                    : lastPlayed
               };
    }

    #endregion // Methods
}