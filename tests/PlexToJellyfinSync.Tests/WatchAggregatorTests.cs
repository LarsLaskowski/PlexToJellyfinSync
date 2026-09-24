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
    /// A partially watched aggregate must not report a last played timestamp, since that would
    /// contradict the unwatched state
    /// </summary>
    [TestMethod]
    public void WatchAggregatorPartiallyWatchedReturnsNoLastPlayed()
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
                               Watched = false
                           }
                       };

        var result = aggregator.Aggregate(children);

        Assert.IsFalse(result.Watched, "Aggregate should not be watched!");
        Assert.IsNull(result.LastPlayed, "Partially watched aggregate should have no last played!");
    }

    /// <summary>
    /// A last played timestamp equal to <see cref="DateTimeOffset.MinValue"/> must not be
    /// mistaken for the absence of a value
    /// </summary>
    [TestMethod]
    public void WatchAggregatorAllWatchedWithMinValueLastPlayedReturnsMinValue()
    {
        var aggregator = new WatchAggregator();
        var children = new List<WatchInfo>
                       {
                           new()
                           {
                               Watched = true,
                               PlayCount = 1,
                               LastPlayed = DateTimeOffset.MinValue
                           }
                       };

        var result = aggregator.Aggregate(children);

        Assert.AreEqual(DateTimeOffset.MinValue, result.LastPlayed, "A genuine minimum-value last played should be preserved, not treated as absent!");
    }

    /// <summary>
    /// When all children are watched but only some carry a last played value, the maximum of the
    /// values that are present is used
    /// </summary>
    [TestMethod]
    public void WatchAggregatorAllWatchedWithMixedLastPlayedReturnsMaxOfPresentValues()
    {
        var aggregator = new WatchAggregator();
        var children = new List<WatchInfo>
                       {
                           new()
                           {
                               Watched = true,
                               PlayCount = 1,
                               LastPlayed = null
                           },
                           new()
                           {
                               Watched = true,
                               PlayCount = 1,
                               LastPlayed = DateTimeOffset.UnixEpoch.AddDays(1)
                           }
                       };

        var result = aggregator.Aggregate(children);

        Assert.AreEqual(DateTimeOffset.UnixEpoch.AddDays(1), result.LastPlayed, "The last played value that is present should be used!");
    }

    /// <summary>
    /// When all children are watched but none carries a last played value, the aggregate has none
    /// either
    /// </summary>
    [TestMethod]
    public void WatchAggregatorAllWatchedWithoutLastPlayedReturnsNull()
    {
        var aggregator = new WatchAggregator();
        var children = new List<WatchInfo>
                       {
                           new()
                           {
                               Watched = true,
                               PlayCount = 1,
                               LastPlayed = null
                           }
                       };

        var result = aggregator.Aggregate(children);

        Assert.IsNull(result.LastPlayed, "An aggregate with no last played value among its children should have none either!");
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