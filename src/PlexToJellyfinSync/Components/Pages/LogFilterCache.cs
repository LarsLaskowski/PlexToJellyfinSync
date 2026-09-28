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
    /// Whether an entry was skipped since the last resync, so the next update can no longer be applied incrementally
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
    /// Apply a possibly changed level or text filter, rebuilding the cache only when it actually changed or a resync is
    /// pending, in which case the rebuild uses the given live entries rather than the possibly stale cached ones
    /// </summary>
    /// <param name="currentEntries">The current chronological entries, used only when a resync is pending</param>
    /// <param name="minLevel">The minimum level an entry must have to be included</param>
    /// <param name="filter">Optional case-insensitive text an entry's message must contain to be included</param>
    public void ApplyFilter(IReadOnlyList<LogEntry> currentEntries, LogLevel minLevel, string? filter)
    {
        if (_needsResync)
        {
            Reset(currentEntries, minLevel, filter);

            return;
        }

        if (minLevel == _minLevel && filter == _filter)
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
    /// rebuild whenever the live entries turn out to hold more than just that one addition since the last update -
    /// which happens when a resync is pending, or when several additions were made before their handlers ran
    /// </summary>
    /// <param name="addedEntry">The entry that was just added</param>
    /// <param name="updatedEntries">The full chronological entries after the addition</param>
    public void Append(LogEntry addedEntry, IReadOnlyList<LogEntry> updatedEntries)
    {
        if (_needsResync == false && _entries.Count > 0 && ReferenceEquals(_entries[^1], addedEntry))
        {
            return;
        }

        if (IsSingleAppend(addedEntry, updatedEntries) == false)
        {
            Reset(updatedEntries, _minLevel, _filter);

            return;
        }

        var evicted = updatedEntries.Count == _entries.Count && _entries.Count > 0 ? _entries[0] : null;

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

    /// <summary>
    /// Determine whether the given live entries are exactly the cached entries plus the one added entry, with at most
    /// the single oldest entry evicted - the only shape the incremental path can correctly account for
    /// </summary>
    /// <param name="addedEntry">The entry that was just added</param>
    /// <param name="updatedEntries">The full chronological entries after the addition</param>
    /// <returns>True when the incremental path applies</returns>
    private bool IsSingleAppend(LogEntry addedEntry, IReadOnlyList<LogEntry> updatedEntries)
    {
        if (_needsResync || updatedEntries.Count == 0 || ReferenceEquals(updatedEntries[^1], addedEntry) == false)
        {
            return false;
        }

        if (_entries.Count == 0)
        {
            return updatedEntries.Count == 1;
        }

        if (updatedEntries.Count != _entries.Count && updatedEntries.Count != _entries.Count + 1)
        {
            return false;
        }

        return ReferenceEquals(updatedEntries[^2], _entries[^1]);
    }

    #endregion // Methods
}