using Microsoft.Extensions.Logging;

using PlexToJellyfinSync.Core.Models;

namespace PlexToJellyfinSync.Components.Pages;

/// <summary>
/// Filters log entries for display on the <see cref="Logs"/> page
/// </summary>
public static class LogFiltering
{
    #region Static methods

    /// <summary>
    /// Filter the given entries by minimum level and message text, in reverse chronological order
    /// </summary>
    /// <param name="entries">The entries to filter, in chronological order</param>
    /// <param name="minLevel">The minimum level an entry must have to be included</param>
    /// <param name="filter">Optional case-insensitive text an entry's message must contain to be included</param>
    /// <returns>The matching entries, newest first</returns>
    public static List<LogEntry> Apply(IReadOnlyList<LogEntry> entries, LogLevel minLevel, string? filter)
    {
        var result = new List<LogEntry>(entries.Count);

        for (var index = entries.Count - 1; index >= 0; index--)
        {
            var entry = entries[index];

            if (entry.Level < minLevel)
            {
                continue;
            }

            if (string.IsNullOrEmpty(filter) == false && entry.Message.Contains(filter, StringComparison.OrdinalIgnoreCase) == false)
            {
                continue;
            }

            result.Add(entry);
        }

        return result;
    }

    #endregion // Static methods
}