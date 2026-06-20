using Microsoft.AspNetCore.Components;

using PlexToJellyfinSync.Core.Abstractions;
using PlexToJellyfinSync.Core.Models;

namespace PlexToJellyfinSync.Components.Pages;

/// <summary>
/// Code-behind for the live log viewer page
/// </summary>
public sealed partial class Logs : IDisposable
{
    #region Fields

    /// <summary>
    /// Currently displayed log entries
    /// </summary>
    private List<LogEntry> _entries = [];

    /// <summary>
    /// Minimum level of entries to display
    /// </summary>
    private LogLevel _minLevel;

    /// <summary>
    /// Free text filter applied to the message
    /// </summary>
    private string? _filter;

    /// <summary>
    /// Indicates whether live updates are paused
    /// </summary>
    private bool _paused;

    #endregion // Fields

    #region Properties

    /// <summary>
    /// Gets or sets the log store
    /// </summary>
    [Inject]
    private ILogStore LogStore { get; set; } = default!;

    #endregion // Properties

    #region ComponentBase

    /// <inheritdoc/>
    protected override void OnInitialized()
    {
        _minLevel = LogLevel.Information;
        _filter = string.Empty;
        _paused = false;
        _entries = LogStore.GetEntries().ToList();
        LogStore.EntryAdded += OnEntryAdded;
    }

    #endregion // ComponentBase

    #region Methods

    /// <summary>
    /// Shorten a category name to its final segment
    /// </summary>
    /// <param name="category">The full category name</param>
    /// <returns>The shortened category name</returns>
    private static string ShortCategory(string category)
    {
        var index = category.LastIndexOf('.');

        return index >= 0 ? category[(index + 1)..] : category;
    }

    /// <summary>
    /// Handle a newly added log entry by refreshing the view unless paused or filtered out
    /// </summary>
    /// <param name="entry">The added log entry</param>
    private void OnEntryAdded(LogEntry entry)
    {
        if (_paused || entry.Level < _minLevel)
        {
            return;
        }

        InvokeAsync(() =>
                    {
                        _entries = LogStore.GetEntries().ToList();
                        StateHasChanged();
                    });
    }

    /// <summary>
    /// Filter the entries by level and message text
    /// </summary>
    /// <returns>The filtered entries in reverse chronological order</returns>
    private IEnumerable<LogEntry> Filtered()
    {
        return _entries.Where(e => e.Level >= _minLevel)
                       .Where(e => string.IsNullOrEmpty(_filter) || e.Message.Contains(_filter, StringComparison.OrdinalIgnoreCase))
                       .Reverse();
    }

    #endregion // Methods

    #region IDisposable

    /// <inheritdoc/>
    public void Dispose()
    {
        LogStore.EntryAdded -= OnEntryAdded;
    }

    #endregion // IDisposable
}