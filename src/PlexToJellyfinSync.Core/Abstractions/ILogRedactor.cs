using System.Diagnostics.CodeAnalysis;

namespace PlexToJellyfinSync.Core.Abstractions;

/// <summary>
/// Masks known secrets out of text before it reaches the log store
/// </summary>
public interface ILogRedactor
{
    #region Methods

    /// <summary>
    /// Replace every occurrence of a known secret with a placeholder
    /// </summary>
    /// <param name="text">Text to redact, or <c>null</c></param>
    /// <returns>The redacted text; <c>null</c> or empty input is returned unchanged</returns>
    [return : NotNullIfNotNull(nameof(text))]
    string? Redact(string? text);

    #endregion // Methods
}