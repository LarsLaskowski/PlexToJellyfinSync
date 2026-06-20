using System.Diagnostics;

using Microsoft.AspNetCore.Components;

namespace PlexToJellyfinSync.Components.Pages;

/// <summary>
/// Code-behind for the error page
/// </summary>
public partial class Error
{
    #region Fields

    /// <summary>
    /// Identifier of the current request
    /// </summary>
    private string? _requestId;

    #endregion // Fields

    #region Properties

    /// <summary>
    /// Gets or sets the current HTTP context
    /// </summary>
    [CascadingParameter]
    private HttpContext? HttpContext { get; set; }

    /// <summary>
    /// Gets a value indicating whether the request identifier should be shown
    /// </summary>
    private bool ShowRequestId => _requestId is { Length: > 0 };

    #endregion // Properties

    #region ComponentBase

    /// <inheritdoc/>
    protected override void OnInitialized()
    {
        _requestId = Activity.Current?.Id ?? HttpContext?.TraceIdentifier;
    }

    #endregion // ComponentBase
}