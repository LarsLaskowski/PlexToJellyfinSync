namespace PlexToJellyfinSync.Service.State;

/// <summary>
/// Reads only the schema version of a state file, so a file of another version is recognized before its other values are bound
/// </summary>
internal sealed class SchemaVersionProbe
{
    #region Properties

    /// <summary>
    /// Schema version of the file
    /// </summary>
    public int? SchemaVersion { get; set; }

    #endregion // Properties
}