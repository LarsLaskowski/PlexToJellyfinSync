using PlexToJellyfinSync.Core.Models;
using PlexToJellyfinSync.Service;

namespace PlexToJellyfinSync.Tests;

/// <summary>
/// Tests for <see cref="WatchAggregator"/>
/// </summary>
[TestClass]
public sealed class WatchAggregatorTests
{
    #region Methods

    /// <summary>
    /// When all children are watched the aggregate is watched
    /// </summary>
    [TestMethod]
    public void WatchAggregatorAllWatchedReturnsWatched()
    {
        var aggregator = new WatchAggregator();
        var children = new List<WatchInfo>
                       {
                           new()
                           {
                               Watched = true,
                               PlayCount = 1,
                               LastPlayed = DateTimeOffset.UnixEpoch.AddDays(1)
                           },
                           new()
                           {
                               Watched = true,
                               PlayCount = 2,
                               LastPlayed = DateTimeOffset.UnixEpoch.AddDays(2)
                           }
                       };

        var result = aggregator.Aggregate(children);

        Assert.IsTrue(result.Watched, "Aggregate should be watched!");
        Assert.AreEqual(DateTimeOffset.UnixEpoch.AddDays(2), result.LastPlayed, "Last played should be the maximum!");
    }

    /// <summary>
    /// When at least one child is unwatched the aggregate is not watched
    /// </summary>
    [TestMethod]
    public void WatchAggregatorPartiallyWatchedReturnsNotWatched()
    {
        var aggregator = new WatchAggregator();
        var children = new List<WatchInfo>
                       {
                           new()
                           {
                               Watched = true
                           },
                           new()
                           {
                               Watched = false
                           }
                       };

        var result = aggregator.Aggregate(children);

        Assert.IsFalse(result.Watched, "Aggregate should not be watched!");
    }

    /// <summary>
    /// An empty collection yields an unwatched aggregate
    /// </summary>
    [TestMethod]
    public void WatchAggregatorNoChildrenReturnsDefault()
    {
        var aggregator = new WatchAggregator();

        var result = aggregator.Aggregate([]);

        Assert.IsFalse(result.Watched, "Empty aggregate should not be watched!");
        Assert.IsNull(result.LastPlayed, "Empty aggregate should have no last played!");
    }

    #endregion // Methods
}