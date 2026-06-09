using PlexToJellyfinSync.Core.Models;

namespace PlexToJellyfinSync.Core.Abstractions;

/// <summary>
/// In-memory ring buffer of recent log entries for the dashboard
/// </summary>
public interface ILogStore
{
    #region Events

    /// <summary>
    /// Raised whenever a new entry has been added
    /// </summary>
    event Action<LogEntry>? EntryAdded;

    #endregion // Events

    #region Methods

    /// <summary>
    /// Get a snapshot of the currently buffered entries, oldest first
    /// </summary>
    /// <returns>List of log entries</returns>
    IReadOnlyList<LogEntry> GetEntries();

    /// <summary>
    /// Add a log entry to the buffer
    /// </summary>
    /// <param name="entry">Entry to add</param>
    void Add(LogEntry entry);

    #endregion // Methods
}