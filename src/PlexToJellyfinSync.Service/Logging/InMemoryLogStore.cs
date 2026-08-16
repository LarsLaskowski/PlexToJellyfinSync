using Microsoft.Extensions.Options;

using PlexToJellyfinSync.Core.Abstractions;
using PlexToJellyfinSync.Core.Models;
using PlexToJellyfinSync.Core.Options;

namespace PlexToJellyfinSync.Service.Logging;

/// <summary>
/// In-memory ring buffer of recent log entries
/// </summary>
public sealed class InMemoryLogStore : ILogStore
{
    #region Fields

    private readonly Lock _lock = new();
    private readonly Queue<LogEntry> _entries = new();
    private readonly int _capacity;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="options">Dashboard options</param>
    public InMemoryLogStore(IOptions<DashboardOptions> options)
    {
        _capacity = Math.Max(1, options.Value.LogBufferSize);
    }

    #endregion // Constructors

    #region ILogStore

    #region Events

    /// <inheritdoc/>
    public event Action<LogEntry>? EntryAdded;

    #endregion // Events

    /// <inheritdoc/>
    public IReadOnlyList<LogEntry> GetEntries()
    {
        lock (_lock)
        {
            return _entries.ToList();
        }
    }

    /// <inheritdoc/>
    public void Add(LogEntry entry)
    {
        lock (_lock)
        {
            _entries.Enqueue(entry);

            while (_entries.Count > _capacity)
            {
                _entries.Dequeue();
            }
        }

        EntryAdded?.Invoke(entry);
    }

    #endregion // ILogStore
}