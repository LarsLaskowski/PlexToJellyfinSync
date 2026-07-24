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
        var service = new SyncStatusService();

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
        var service = new SyncStatusService();

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
        var service = new SyncStatusService();

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
        var service = new SyncStatusService();
        var raised = 0;

        service.Changed += () => raised++;

        service.Update(status => status.ItemsProcessed++);
        service.Update(status => status.ItemsProcessed++);

        Assert.AreEqual(2, raised, "Every update should notify the subscribers!");
    }

    /// <summary>
    /// Concurrent updates are serialized and no increment is lost
    /// </summary>
    [TestMethod]
    public void SyncStatusServiceConcurrentUpdatesLoseNoIncrement()
    {
        var service = new SyncStatusService();

        Parallel.For(0, 500, _ => service.Update(status => status.ItemsProcessed++));

        Assert.AreEqual(500L, service.GetSnapshot().ItemsProcessed, "Concurrent updates should not lose an increment!");
    }

    /// <summary>
    /// Snapshots taken while updates run are always internally consistent
    /// </summary>
    [TestMethod]
    public void SyncStatusServiceConcurrentSnapshotsStayConsistent()
    {
        var service = new SyncStatusService();

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

                             Assert.IsTrue(snapshot.ItemsProcessed >= snapshot.NfoCreated, "A snapshot should never show more writes than processed items!");
                         }
                     });

        Assert.AreEqual(250L, service.GetSnapshot().NfoCreated, "Every update should have been applied!");
    }

    #endregion // Methods
}