namespace PlexToJellyfinSync.Components.Layout;

/// <summary>
/// Code-behind for the main layout
/// </summary>
public sealed partial class MainLayout
{
    #region Properties

    /// <summary>
    /// Gets a value indicating whether the logout button is shown, which is the case when an access token protects the dashboard
    /// </summary>
    private bool IsLogoutShown => string.IsNullOrWhiteSpace(DashboardOptions.Value.Token) == false;

    #endregion // Properties
}