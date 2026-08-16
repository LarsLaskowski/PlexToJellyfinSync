using Microsoft.AspNetCore.Components;

using PlexToJellyfinSync.Core.Abstractions;
using PlexToJellyfinSync.Core.Models;

namespace PlexToJellyfinSync.Components.Pages;

/// <summary>
/// Code-behind for the status dashboard page
/// </summary>
public sealed partial class Dashboard : IDisposable
{
    #region Fields

    /// <summary>
    /// Current status snapshot
    /// </summary>
    private SyncStatusViewData _status = new();

    #endregion // Fields

    #region Properties

    /// <summary>
    /// Gets or sets the status provider
    /// </summary>
    [Inject]
    private ISyncStatusProvider StatusProvider { get; set; } = default!;

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Format an optional timestamp for display
    /// </summary>
    /// <param name="value">Timestamp to format</param>
    /// <returns>Formatted local time, or a dash when no value is present</returns>
    private static string Format(DateTimeOffset? value)
    {
        return value.HasValue ? value.Value.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss") : "-";
    }

    /// <summary>
    /// Handle a status change notification by refreshing the snapshot
    /// </summary>
    private void OnChanged()
    {
        _status = StatusProvider.GetSnapshot();
        InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// Calculate the uptime since the service started
    /// </summary>
    /// <returns>Human readable uptime</returns>
    private string Uptime()
    {
        var span = DateTimeOffset.UtcNow - _status.StartedAt;

        return $"{(int)span.TotalHours}h {span.Minutes}m";
    }

    #endregion // Methods

    #region ComponentBase

    /// <inheritdoc/>
    protected override void OnInitialized()
    {
        _status = StatusProvider.GetSnapshot();
        StatusProvider.Changed += OnChanged;
    }

    #endregion // ComponentBase

    #region IDisposable

    /// <inheritdoc/>
    public void Dispose()
    {
        StatusProvider.Changed -= OnChanged;
    }

    #endregion // IDisposable
}