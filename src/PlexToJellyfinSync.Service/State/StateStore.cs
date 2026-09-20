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

    private static readonly JsonSerializerOptions _jsonOptions = new()
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
    /// Delete a leftover temp file without masking the write failure that triggered the cleanup
    /// </summary>
    /// <param name="tempPath">Temp file path</param>
    private static void TryDeleteTempFile(string tempPath)
    {
        try
        {
            File.Delete(tempPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Best-effort cleanup; the write failure that triggered it is what the caller sees.
        }
    }

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
            var state = await JsonSerializer.DeserializeAsync<SyncStateFile>(stream, _jsonOptions, cancellationToken).ConfigureAwait(false);

            return state ?? new SyncStateFile();
        }
        catch (JsonException ex)
        {
            var corruptPath = _filePath + ".corrupt";

            PreserveCorruptFile(corruptPath);

            _logger.LogError(ex, "State file {Path} is corrupt, preserved as {CorruptPath}; starting fresh with no high-water mark", _filePath, corruptPath);

            return new SyncStateFile();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read state file {Path}, starting fresh", _filePath);

            return new SyncStateFile();
        }
    }

    /// <summary>
    /// Move the corrupt state file aside so the failure can be investigated instead of being overwritten
    /// </summary>
    /// <param name="corruptPath">Path to preserve the corrupt file at</param>
    private void PreserveCorruptFile(string corruptPath)
    {
        try
        {
            File.Move(_filePath, corruptPath, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Best-effort preservation; the corruption itself is already logged.
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

            var tempPath = _filePath + ".tmp";

            try
            {
                await using var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None);

                await JsonSerializer.SerializeAsync(stream, state, _jsonOptions, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                TryDeleteTempFile(tempPath);

                throw;
            }

            File.Move(tempPath, _filePath, overwrite: true);
        }
        finally
        {
            _gate.Release();
        }
    }

    #endregion // IStateStore
}