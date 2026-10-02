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
    #region Fields

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
        var orchestrator = new FakeSyncOrchestrator { ReconcileException = failure };
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
        var orchestrator = new FakeSyncOrchestrator { ReconcileException = new InvalidOperationException("boom") };
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
        var orchestrator = new FakeSyncOrchestrator { ReconcileWaitsForCancellation = true };
        var logger = new RecordingWorkerLogger();
        using var worker = CreateWorker(orchestrator, logger);

        await worker.StartAsync(_testContext.CancellationToken);
        await orchestrator.ReconcileCalled.WaitAsync(TimeSpan.FromSeconds(10), _testContext.CancellationToken);
        await worker.StopAsync(_testContext.CancellationToken);

        Assert.DoesNotContain(e => e.Level == LogLevel.Error, logger.GetEntries(), "Cancellation should not be logged as an error!");
    }

    /// <summary>
    /// Create a worker with a long poll interval so the loop parks after the startup reconcile
    /// </summary>
    /// <param name="orchestrator">Orchestrator fake</param>
    /// <param name="logger">Logger fake</param>
    /// <returns>Worker</returns>
    private static Worker CreateWorker(FakeSyncOrchestrator orchestrator, RecordingWorkerLogger logger)
    {
        return new Worker(orchestrator,
                          new FakeSyncStatusProvider(),
                          Options.Create(new SyncOptions { PollIntervalSeconds = 3600 }),
                          logger);
    }

    #endregion // Methods
}