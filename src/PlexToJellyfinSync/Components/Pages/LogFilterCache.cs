using Microsoft.Extensions.Logging;

using PlexToJellyfinSync.Core.Models;

namespace PlexToJellyfinSync.Components.Pages;

/// <summary>
/// Maintains the filtered, newest-first view of the log buffer shown on the <see cref="Logs"/> page,
/// updating it incrementally as entries are appended instead of re-filtering the whole buffer on every change
/// </summary>
public sealed class LogFilterCache
{
    #region Fields

    /// <summary>
    /// The cached filtered view, newest first
    /// </summary>
    private readonly List<LogEntry> _filtered = [];

    /// <summary>
    /// The chronological entries the cached filtered view was last built from
    /// </summary>
    private IReadOnlyList<LogEntry> _entries = [];

    /// <summary>
    /// The minimum level the cached filtered view was last built with
    /// </summary>
    private LogLevel _minLevel;

    /// <summary>
    /// The message filter the cached filtered view was last built with
    /// </summary>
    private string? _filter;

    /// <summary>
    /// Whether an entry was skipped since the last <see cref="Reset"/>, so the next update can no longer be applied incrementally
    /// </summary>
    private bool _needsResync;

    #endregion // Fields

    #region Properties

    /// <summary>
    /// The current filtered entries, newest first
    /// </summary>
    public IReadOnlyList<LogEntry> Filtered => _filtered;

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Rebuild the cache from the full given entries
    /// </summary>
    /// <param name="entries">The chronological entries to filter</param>
    /// <param name="minLevel">The minimum level an entry must have to be included</param>
    /// <param name="filter">Optional case-insensitive text an entry's message must contain to be included</param>
    public void Reset(IReadOnlyList<LogEntry> entries, LogLevel minLevel, string? filter)
    {
        _entries = entries;
        _minLevel = minLevel;
        _filter = filter;
        _needsResync = false;

        _filtered.Clear();
        _filtered.AddRange(LogFiltering.Apply(entries, minLevel, filter));
    }

    /// <summary>
    /// Apply a possibly changed level or text filter, rebuilding the cache only when it actually changed or a resync is pending
    /// </summary>
    /// <param name="minLevel">The minimum level an entry must have to be included</param>
    /// <param name="filter">Optional case-insensitive text an entry's message must contain to be included</param>
    public void ApplyFilter(LogLevel minLevel, string? filter)
    {
        if (_needsResync == false && minLevel == _minLevel && filter == _filter)
        {
            return;
        }

        Reset(_entries, minLevel, filter);
    }

    /// <summary>
    /// Mark the cache as no longer reflecting the live buffer, because an entry was skipped without updating it
    /// </summary>
    public void MarkStale()
    {
        _needsResync = true;
    }

    /// <summary>
    /// Fold a single newly added entry into the cache instead of re-filtering the whole buffer, falling back to a full
    /// rebuild when a resync is pending or the buffer changed by more than the one entry this call accounts for
    /// </summary>
    /// <param name="addedEntry">The entry that was just added</param>
    /// <param name="updatedEntries">The full chronological entries after the addition</param>
    public void Append(LogEntry addedEntry, IReadOnlyList<LogEntry> updatedEntries)
    {
        if (_needsResync)
        {
            Reset(updatedEntries, _minLevel, _filter);

            return;
        }

        var evicted = _entries.Count > 0 && (updatedEntries.Count == 0 || ReferenceEquals(updatedEntries[0], _entries[0]) == false)
                          ? _entries[0]
                          : null;

        _entries = updatedEntries;

        if (evicted is not null && _filtered.Count > 0 && ReferenceEquals(_filtered[^1], evicted))
        {
            _filtered.RemoveAt(_filtered.Count - 1);
        }

        if (addedEntry.Level >= _minLevel && (string.IsNullOrEmpty(_filter) || addedEntry.Message.Contains(_filter, StringComparison.OrdinalIgnoreCase)))
        {
            _filtered.Insert(0, addedEntry);
        }
    }

    #endregion // Methods
}