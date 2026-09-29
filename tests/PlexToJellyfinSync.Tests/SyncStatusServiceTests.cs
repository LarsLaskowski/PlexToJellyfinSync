using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using PlexToJellyfinSync.Service;

namespace PlexToJellyfinSync.Tests;

/// <summary>
/// Tests for <see cref="SyncStatusService"/>
/// </summary>
[TestClass]
public sealed class SyncStatusServiceTests
{
    #region Methods

    /// <summary>
    /// A new instance records the start timestamp and reports an idle state
    /// </summary>
    [TestMethod]
    public void SyncStatusServiceNewInstanceReportsIdleState()
    {
        var service = new SyncStatusService(NullLogger<SyncStatusService>.Instance);

        var snapshot = service.GetSnapshot();

        Assert.AreNotEqual(default, snapshot.StartedAt, "The start timestamp should be set!");
        Assert.IsFalse(snapshot.IsRunning, "A new instance should not report a running synchronization!");
        Assert.IsFalse(snapshot.PlexConnected, "A new instance should not report a Plex connection!");
        Assert.AreEqual(0L, snapshot.ItemsProcessed, "A new instance should not report processed items!");
    }

    /// <summary>
    /// A mutation is visible in the next snapshot
    /// </summary>
    [TestMethod]
    public void SyncStatusServiceUpdateIsVisibleInSnapshot()
    {
        var service = new SyncStatusService(NullLogger<SyncStatusService>.Instance);

        service.Update(status =>
                       {
                           status.PlexConnected = true;
                           status.ItemsProcessed = 12;
                           status.LastError = "boom";
                       });

        var snapshot = service.GetSnapshot();

        Assert.IsTrue(snapshot.PlexConnected, "The connection flag should be updated!");
        Assert.AreEqual(12L, snapshot.ItemsProcessed, "The processed count should be updated!");
        Assert.AreEqual("boom", snapshot.LastError, "The last error should be updated!");
    }

    /// <summary>
    /// A snapshot is a copy that does not write back into the service
    /// </summary>
    [TestMethod]
    public void SyncStatusServiceSnapshotIsDetachedCopy()
    {
        var service = new SyncStatusService(NullLogger<SyncStatusService>.Instance);

        service.Update(status => status.ItemsProcessed = 5);

        var first = service.GetSnapshot();

        first.ItemsProcessed = 99;
        first.LastError = "tampered";

        var second = service.GetSnapshot();

        Assert.AreNotSame(first, second, "Every snapshot should be a new instance!");
        Assert.AreEqual(5L, second.ItemsProcessed, "Mutating a snapshot should not change the service state!");
        Assert.IsNull(second.LastError, "Mutating a snapshot should not change the service state!");
    }

    /// <summary>
    /// Every update notifies the subscribers
    /// </summary>
    [TestMethod]
    public void SyncStatusServiceUpdateRaisesChanged()
    {
        var service = new SyncStatusService(NullLogger<SyncStatusService>.Instance);
        var raised = 0;

        service.Changed += () => raised++;

        service.Update(status => status.ItemsProcessed++);
        service.Update(status => status.ItemsProcessed++);

        Assert.AreEqual(2, raised, "Every update should notify the subscribers!");
    }

    /// <summary>
    /// A throwing subscriber neither propagates into the caller nor blocks the other subscribers
    /// </summary>
    [TestMethod]
    public void SyncStatusServiceUpdateThrowingSubscriberIsIsolated()
    {
        var logger = new RecordingSyncStatusLogger();
        var service = new SyncStatusService(logger);
        var raised = 0;

        service.Changed += () => throw new InvalidOperationException("UI failure");
        service.Changed += () => raised++;

        service.Update(status => status.ItemsProcessed = 3);

        Assert.AreEqual(1, raised, "A subscriber after a throwing one should still be notified!");
        Assert.AreEqual(3L, service.GetSnapshot().ItemsProcessed, "The update should be applied despite the throwing subscriber!");
        Assert.HasCount(1, logger.Warnings, "The throwing subscriber should be logged exactly once as a warning!");
        Assert.IsInstanceOfType<InvalidOperationException>(logger.Warnings[0], "The logged warning should carry the subscriber's exception!");
    }

    /// <summary>
    /// Concurrent updates are serialized and no increment is lost
    /// </summary>
    [TestMethod]
    public void SyncStatusServiceConcurrentUpdatesLoseNoIncrement()
    {
        var service = new SyncStatusService(NullLogger<SyncStatusService>.Instance);

        Parallel.For(0, 500, _ => service.Update(status => status.ItemsProcessed++));

        Assert.AreEqual(500L, service.GetSnapshot().ItemsProcessed, "Concurrent updates should not lose an increment!");
    }

    /// <summary>
    /// Snapshots taken while updates run are always internally consistent
    /// </summary>
    [TestMethod]
    public void SyncStatusServiceConcurrentSnapshotsStayConsistent()
    {
        var service = new SyncStatusService(NullLogger<SyncStatusService>.Instance);

        Parallel.For(0,
                     500,
                     index =>
                     {
                         if (index % 2 == 0)
                         {
                             service.Update(status =>
                                            {
                                                status.NfoCreated++;
                                                status.ItemsProcessed++;
                                            });
                         }
                         else
                         {
                             var snapshot = service.GetSnapshot();

                             Assert.IsGreaterThanOrEqualTo(snapshot.NfoCreated, snapshot.ItemsProcessed, "A snapshot should never show more writes than processed items!");
                         }
                     });

        Assert.AreEqual(250L, service.GetSnapshot().NfoCreated, "Every update should have been applied!");
    }

    #endregion // Methods
}