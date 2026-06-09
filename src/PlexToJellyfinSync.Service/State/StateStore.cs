using System.Text.Json;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using PlexToJellyfinSync.Core.Abstractions;
using PlexToJellyfinSync.Core.Options;

namespace PlexToJellyfinSync.Service.State;

/// <summary>
/// Persists the synchronization state to a JSON file
/// </summary>
public sealed class StateStore : IStateStore
{
    #region Fields

    private static readonly JsonSerializerOptions JsonOptions = new()
                                                                {
                                                                    WriteIndented = true
                                                                };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ILogger<StateStore> _logger;
    private readonly string _filePath;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="options">State options</param>
    /// <param name="logger">Logging interface</param>
    public StateStore(IOptions<StateOptions> options, ILogger<StateStore> logger)
    {
        _logger = logger;
        _filePath = Path.Combine(options.Value.Directory, "state.json");
    }

    #endregion // Constructors

    #region Methods

    /// <summary>
    /// Read the persisted state file
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The persisted state, or a new instance</returns>
    private async Task<SyncStateFile> ReadAsync(CancellationToken cancellationToken)
    {
        if (File.Exists(_filePath) == false)
        {
            return new SyncStateFile();
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            var state = await JsonSerializer.DeserializeAsync<SyncStateFile>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);

            return state ?? new SyncStateFile();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read state file {Path}, starting fresh", _filePath);

            return new SyncStateFile();
        }
    }

    #endregion // Methods

    #region IStateStore

    /// <inheritdoc/>
    public async Task<DateTimeOffset?> GetHighWaterMarkAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var state = await ReadAsync(cancellationToken).ConfigureAwait(false);

            return state.HighWaterMark;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc/>
    public async Task SetHighWaterMarkAsync(DateTimeOffset value, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var directory = Path.GetDirectoryName(_filePath);

            if (string.IsNullOrEmpty(directory) == false && Directory.Exists(directory) == false)
            {
                Directory.CreateDirectory(directory);
            }

            var state = await ReadAsync(cancellationToken).ConfigureAwait(false);
            state.HighWaterMark = value;

            await using var stream = new FileStream(_filePath, FileMode.Create, FileAccess.Write, FileShare.None);

            await JsonSerializer.SerializeAsync(stream, state, JsonOptions, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    #endregion // IStateStore
}