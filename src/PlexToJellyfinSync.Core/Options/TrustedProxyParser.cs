using System.Net;

namespace PlexToJellyfinSync.Core.Options;

/// <summary>
/// Parser for the entries of <c>Dashboard:TrustedProxies</c>
/// </summary>
public static class TrustedProxyParser
{
    #region Static methods

    /// <summary>
    /// Try to parse an IP address or CIDR network entry
    /// </summary>
    /// <param name="entry">Entry text</param>
    /// <param name="network">Parsed network when the entry is valid</param>
    /// <returns>True when the entry is valid</returns>
    public static bool TryParse(string? entry, out IPNetwork network)
    {
        throw new NotImplementedException();
    }

    #endregion // Static methods
}