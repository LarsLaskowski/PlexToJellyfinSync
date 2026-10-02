using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using PlexToJellyfinSync.Core.Options;

namespace PlexToJellyfinSync.Tests;

/// <summary>
/// Tests for <see cref="Worker"/>
/// </summary>
[TestClass]
public sealed class WorkerTests
{
    #region Constants

    /// <summary>
    /// Smallest poll interval the worker accepts, in seconds
    /// </summary>
    private const int PollTestIntervalSeconds = 5;

    #endregion // Constants

    #region Fields

    /// <summary>
    /// Upper bound for waiting on a worker signal that needs one poll interval to arrive
    /// </summary>
    private static readonly TimeSpan _waitTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Test context
    /// </summary>
    private readonly TestContext _testContext;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="testContext">Test context</param>
    public WorkerTests(TestContext testContext)
    {
        _testContext = testContext;
    }

    #endregion // Constructors

    #region Methods

    /// <summary>
    /// A failing startup reconcile is logged once as an error with the exception and the full reconcile message
    /// </summary>
    /// <returns>Task</returns>
    [TestMethod]
    public async Task WorkerStartupReconcileFailureLogsFullReconcileFailedError()
    {
        var failure = new InvalidOperationException("boom");
        var orchestrator = new FakeSyncOrchestrator
                           {
                               ReconcileException = failure
                           };
        var logger = new RecordingWorkerLogger();
        using var worker = CreateWorker(orchestrator, logger);

        await worker.StartAsync(_testContext.CancellationToken);
        await logger.ErrorLogged.WaitAsync(TimeSpan.FromSeconds(10), _testContext.CancellationToken);
        await worker.StopAsync(_testContext.CancellationToken);

        var errors = logger.GetEntries().Where(e => e.Level == LogLevel.Error).ToList();

        Assert.HasCount(1, errors, "Exactly one error entry should be logged!");
        Assert.AreEqual("Full reconcile failed", errors[0].Message, "The error should use the full reconcile message!");
        Assert.AreSame(failure, errors[0].Exception, "The error entry should carry the orchestrator exception!");
        Assert.DoesNotContain(e => e.Message.Contains("Initial reconcile failed", StringComparison.Ordinal), logger.GetEntries(), "No entry should be labeled as initial reconcile!");
    }

    /// <summary>
    /// A failing startup reconcile is swallowed so the worker keeps running and stops cleanly
    /// </summary>
    /// <returns>Task</returns>
    [TestMethod]
    public async Task WorkerStartupReconcileFailureKeepsWorkerRunning()
    {
        var orchestrator = new FakeSyncOrchestrator
                           {
                               ReconcileException = new InvalidOperationException("boom")
                           };
        var logger = new RecordingWorkerLogger();
        using var worker = CreateWorker(orchestrator, logger);

        await worker.StartAsync(_testContext.CancellationToken);
        await logger.ErrorLogged.WaitAsync(TimeSpan.FromSeconds(10), _testContext.CancellationToken);

        var executeTask = worker.ExecuteTask;

        Assert.IsNotNull(executeTask, "The worker should have started its execution!");
        Assert.IsFalse(executeTask.IsFaulted, "The reconcile exception should not fault the worker!");
        Assert.IsFalse(executeTask.IsCompleted, "The worker should keep running after the failure!");

        await worker.StopAsync(_testContext.CancellationToken);

        Assert.IsTrue(executeTask.IsCompletedSuccessfully, "The worker should stop without an exception!");
    }

    /// <summary>
    /// A cancellation of the startup reconcile on host stop is not reported as a failure
    /// </summary>
    /// <returns>Task</returns>
    [TestMethod]
    public async Task WorkerStopDuringStartupReconcileLogsNoError()
    {
        var orchestrator = new FakeSyncOrchestrator
                           {
                               ReconcileWaitsForCancellation = true
                           };
        var logger = new RecordingWorkerLogger();
        using var worker = CreateWorker(orchestrator, logger);

        await worker.StartAsync(_testContext.CancellationToken);
        await orchestrator.ReconcileCalled.WaitAsync(TimeSpan.FromSeconds(10), _testContext.CancellationToken);
        await worker.StopAsync(_testContext.CancellationToken);

        Assert.AreEqual(TaskStatus.Canceled, worker.ExecuteTask?.Status, "The cancellation should be rethrown, not swallowed!");
        Assert.DoesNotContain(e => e.Level == LogLevel.Error, logger.GetEntries(), "Cancellation should not be logged as an error!");
    }

    /// <summary>
    /// A failing history sync is logged once as an error with the exception and the incremental message
    /// </summary>
    /// <returns>Task</returns>
    [TestMethod]
    public async Task WorkerProcessHistoryFailureLogsIncrementalHistorySyncFailedError()
    {
        var failure = new InvalidOperationException("boom");
        var orchestrator = new FakeSyncOrchestrator
                           {
                               ProcessHistoryException = failure
                           };
        var logger = new RecordingWorkerLogger();
        using var worker = CreateWorker(orchestrator, logger, PollTestIntervalSeconds);

        await worker.StartAsync(_testContext.CancellationToken);
        await logger.ErrorLogged.WaitAsync(_waitTimeout, _testContext.CancellationToken);
        await worker.StopAsync(_testContext.CancellationToken);

        var errors = logger.GetEntries().Where(e => e.Level == LogLevel.Error).ToList();

        Assert.HasCount(1, errors, "Exactly one error entry should be logged!");
        Assert.AreEqual("Incremental history sync failed", errors[0].Message, "The error should use the incremental history message!");
        Assert.AreSame(failure, errors[0].Exception, "The error entry should carry the orchestrator exception!");
    }

    /// <summary>
    /// A failing history sync is swallowed so the worker starts its next poll iteration and stops cleanly
    /// </summary>
    /// <returns>Task</returns>
    [TestMethod]
    public async Task WorkerProcessHistoryFailureKeepsWorkerRunning()
    {
        var orchestrator = new FakeSyncOrchestrator
                           {
                               ProcessHistoryException = new InvalidOperationException("boom")
                           };
        var logger = new RecordingWorkerLogger();
        var status = new FakeSyncStatusProvider();
        var changes = 0;
        var secondPoll = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        status.Changed += () =>
                          {
                              if (Interlocked.Increment(ref changes) >= 2)
                              {
                                  secondPoll.TrySetResult();
                              }
                          };

        using var worker = CreateWorker(orchestrator, logger, PollTestIntervalSeconds, status);

        await worker.StartAsync(_testContext.CancellationToken);
        await secondPoll.Task.WaitAsync(_waitTimeout, _testContext.CancellationToken);

        var executeTask = worker.ExecuteTask;

        Assert.IsNotNull(executeTask, "The worker should have started its execution!");
        Assert.IsTrue(logger.ErrorLogged.IsCompleted, "The history failure should have been logged before the next poll!");
        Assert.IsFalse(executeTask.IsFaulted, "The history exception should not fault the worker!");
        Assert.IsFalse(executeTask.IsCompleted, "The worker should keep running after the failure!");

        await worker.StopAsync(_testContext.CancellationToken);

        Assert.IsTrue(executeTask.IsCompletedSuccessfully, "The worker should stop without an exception!");
    }

    /// <summary>
    /// A cancellation of the history sync on host stop is not reported as a failure
    /// </summary>
    /// <returns>Task</returns>
    [TestMethod]
    public async Task WorkerStopDuringProcessHistoryLogsNoError()
    {
        var orchestrator = new FakeSyncOrchestrator
                           {
                               ProcessHistoryWaitsForCancellation = true
                           };
        var logger = new RecordingWorkerLogger();
        using var worker = CreateWorker(orchestrator, logger, PollTestIntervalSeconds);

        await worker.StartAsync(_testContext.CancellationToken);
        await orchestrator.ProcessHistoryCalled.WaitAsync(_waitTimeout, _testContext.CancellationToken);
        await worker.StopAsync(_testContext.CancellationToken);

        Assert.AreEqual(TaskStatus.Canceled, worker.ExecuteTask?.Status, "The cancellation should be rethrown, not swallowed!");
        Assert.DoesNotContain(e => e.Level == LogLevel.Error, logger.GetEntries(), "Cancellation should not be logged as an error!");
    }

    /// <summary>
    /// A cancellation of the startup reconcile that is not the worker's own is logged and the worker keeps running
    /// </summary>
    /// <returns>Task</returns>
    [TestMethod]
    public async Task WorkerStartupReconcileForeignCancellationLogsErrorAndKeepsWorkerRunning()
    {
        var failure = new OperationCanceledException("timeout");
        var orchestrator = new FakeSyncOrchestrator
                           {
                               ReconcileException = failure
                           };
        var logger = new RecordingWorkerLogger();
        using var worker = CreateWorker(orchestrator, logger);

        await worker.StartAsync(_testContext.CancellationToken);
        await logger.ErrorLogged.WaitAsync(TimeSpan.FromSeconds(10), _testContext.CancellationToken);

        var executeTask = worker.ExecuteTask;

        Assert.IsNotNull(executeTask, "The worker should have started its execution!");
        Assert.IsFalse(executeTask.IsFaulted, "The foreign cancellation should not fault the worker!");
        Assert.IsFalse(executeTask.IsCanceled, "The foreign cancellation should not cancel the worker!");
        Assert.IsFalse(executeTask.IsCompleted, "The worker should keep running after the failure!");

        await worker.StopAsync(_testContext.CancellationToken);

        var errors = logger.GetEntries().Where(e => e.Level == LogLevel.Error).ToList();

        Assert.HasCount(1, errors, "Exactly one error entry should be logged!");
        Assert.AreEqual("Full reconcile failed", errors[0].Message, "The error should use the full reconcile message!");
        Assert.AreSame(failure, errors[0].Exception, "The error entry should carry the cancellation exception!");
        Assert.IsTrue(executeTask.IsCompletedSuccessfully, "The worker should stop without an exception!");
    }

    /// <summary>
    /// A cancellation of the history sync that is not the worker's own is logged and the worker keeps running
    /// </summary>
    /// <returns>Task</returns>
    [TestMethod]
    public async Task WorkerProcessHistoryForeignCancellationLogsErrorAndKeepsWorkerRunning()
    {
        var failure = new TaskCanceledException("timeout");
        var orchestrator = new FakeSyncOrchestrator
                           {
                               ProcessHistoryException = failure
                           };
        var logger = new RecordingWorkerLogger();
        using var worker = CreateWorker(orchestrator, logger, PollTestIntervalSeconds);

        await worker.StartAsync(_testContext.CancellationToken);
        await logger.ErrorLogged.WaitAsync(_waitTimeout, _testContext.CancellationToken);

        var executeTask = worker.ExecuteTask;

        Assert.IsNotNull(executeTask, "The worker should have started its execution!");
        Assert.IsFalse(executeTask.IsFaulted, "The foreign cancellation should not fault the worker!");
        Assert.IsFalse(executeTask.IsCanceled, "The foreign cancellation should not cancel the worker!");
        Assert.IsFalse(executeTask.IsCompleted, "The worker should keep running after the failure!");

        await worker.StopAsync(_testContext.CancellationToken);

        var errors = logger.GetEntries().Where(e => e.Level == LogLevel.Error).ToList();

        Assert.HasCount(1, errors, "Exactly one error entry should be logged!");
        Assert.AreEqual("Incremental history sync failed", errors[0].Message, "The error should use the incremental history message!");
        Assert.AreSame(failure, errors[0].Exception, "The error entry should carry the cancellation exception!");
        Assert.IsTrue(executeTask.IsCompletedSuccessfully, "The worker should stop without an exception!");
    }

    /// <summary>
    /// Create a worker
    /// </summary>
    /// <param name="orchestrator">Orchestrator fake</param>
    /// <param name="logger">Logger fake</param>
    /// <param name="pollIntervalSeconds">Poll interval; the default is long so the loop parks after the startup reconcile</param>
    /// <param name="status">Status provider fake, or <c>null</c> for a new one</param>
    /// <returns>Worker</returns>
    private static Worker CreateWorker(FakeSyncOrchestrator orchestrator, RecordingWorkerLogger logger, int pollIntervalSeconds = 3600, FakeSyncStatusProvider? status = null)
    {
        return new Worker(orchestrator,
                          status ?? new FakeSyncStatusProvider(),
                          Options.Create(new SyncOptions
                                         {
                                             PollIntervalSeconds = pollIntervalSeconds
                                         }),
                          logger);
    }

    #endregion // Methods
}