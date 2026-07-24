using System.Security.Cryptography;
using System.Text;

namespace PlexToJellyfinSync.Service.Security;

/// <summary>
/// Provides a timing-safe comparison for dashboard access tokens
/// </summary>
public static class TokenComparer
{
    #region Static methods

    /// <summary>
    /// Compare two tokens in constant time relative to their content
    /// </summary>
    /// <param name="candidate">Token supplied by the client</param>
    /// <param name="expected">Configured token to compare against</param>
    /// <returns>True when both tokens are equal</returns>
    public static bool FixedTimeEquals(string? candidate, string? expected)
    {
        // Hashing both inputs to a fixed length first keeps the comparison free of any
        // length-based side channel before the constant-time check runs.
        var candidateHash = SHA256.HashData(Encoding.UTF8.GetBytes(candidate ?? string.Empty));
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected ?? string.Empty));

        return CryptographicOperations.FixedTimeEquals(candidateHash, expectedHash);
    }

    #endregion // Static methods
}