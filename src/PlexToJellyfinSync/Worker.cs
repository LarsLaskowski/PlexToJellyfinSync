using Microsoft.Extensions.Options;

using PlexToJellyfinSync.Core.Abstractions;
using PlexToJellyfinSync.Core.Options;

namespace PlexToJellyfinSync;

/// <summary>
/// Background service that periodically polls Plex and reconciles the libraries
/// </summary>
public sealed class Worker : BackgroundService
{
    #region Fields

    private readonly ISyncOrchestrator _orchestrator;
    private readonly ISyncStatusProvider _status;
    private readonly SyncOptions _options;
    private readonly ILogger<Worker> _logger;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="orchestrator">Sync orchestrator</param>
    /// <param name="status">Status provider</param>
    /// <param name="options">Sync options</param>
    /// <param name="logger">Logging interface</param>
    public Worker(ISyncOrchestrator orchestrator,
                  ISyncStatusProvider status,
                  IOptions<SyncOptions> options,
                  ILogger<Worker> logger)
    {
        _orchestrator = orchestrator;
        _status = status;
        _options = options.Value;
        _logger = logger;
    }

    #endregion // Constructors

    #region Methods

    /// <summary>
    /// Run a reconcile and swallow non-cancellation errors
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task</returns>
    private async Task SafeReconcileAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _orchestrator.ReconcileAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Initial reconcile failed");
        }
    }

    #endregion // Methods

    #region BackgroundService

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollInterval = TimeSpan.FromSeconds(Math.Max(5, _options.PollIntervalSeconds));
        var reconcileInterval = TimeSpan.FromHours(Math.Max(1, _options.FullReconcileIntervalHours));

        _logger.LogInformation("Worker started; poll every {Poll}, reconcile every {Reconcile}", pollInterval, reconcileInterval);

        await SafeReconcileAsync(stoppingToken).ConfigureAwait(false);

        var lastReconcile = DateTimeOffset.UtcNow;

        while (stoppingToken.IsCancellationRequested == false)
        {
            var next = DateTimeOffset.UtcNow.Add(pollInterval);

            _status.Update(s => s.NextPollAt = next);

            try
            {
                await Task.Delay(pollInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await _orchestrator.ProcessHistoryAsync(stoppingToken).ConfigureAwait(false);

            if (DateTimeOffset.UtcNow - lastReconcile >= reconcileInterval)
            {
                await SafeReconcileAsync(stoppingToken).ConfigureAwait(false);
                lastReconcile = DateTimeOffset.UtcNow;
            }
        }

        _logger.LogInformation("Worker stopping");
    }

    #endregion // BackgroundService
}