using System.Net;
using System.Net.Sockets;

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
        network = default;

        if (string.IsNullOrWhiteSpace(entry))
        {
            return false;
        }

        var text = entry.Trim();

        if (text.IndexOfAny(['%', '[', ']']) >= 0)
        {
            return false;
        }

        var parts = text.Split('/');

        if (parts.Length > 2 || IPAddress.TryParse(parts[0], out var address) == false)
        {
            return false;
        }

        var isIPv4 = address.AddressFamily == AddressFamily.InterNetwork;

        // Only the canonical dotted-decimal form is accepted: the lenient short, integer, hex and octal forms would
        // trust a network the operator never wrote
        if (isIPv4 ? address.ToString() != parts[0] : parts[0].Contains('.'))
        {
            return false;
        }

        var maxPrefix = isIPv4 ? 32 : 128;
        var prefix = maxPrefix;

        if (parts.Length == 2 && TryParsePrefix(parts[1], maxPrefix, out prefix) == false)
        {
            return false;
        }

        network = new IPNetwork(ClearHostBits(address, prefix), prefix);

        return true;
    }

    /// <summary>
    /// Parse a prefix length consisting of decimal digits without sign or leading zero
    /// </summary>
    /// <param name="text">Prefix text</param>
    /// <param name="maxPrefix">Largest allowed prefix length</param>
    /// <param name="prefix">Parsed prefix length</param>
    /// <returns>True when the prefix is valid</returns>
    private static bool TryParsePrefix(string text, int maxPrefix, out int prefix)
    {
        prefix = 0;

        if (text.Length == 0 || text.Length > 3 || text.Any(character => character is < '0' or > '9') || (text.Length > 1 && text[0] == '0'))
        {
            return false;
        }

        prefix = int.Parse(text, System.Globalization.CultureInfo.InvariantCulture);

        return prefix <= maxPrefix;
    }

    /// <summary>
    /// Clear the bits of an address beyond the prefix length
    /// </summary>
    /// <param name="address">Address</param>
    /// <param name="prefix">Prefix length</param>
    /// <returns>The network base address</returns>
    private static IPAddress ClearHostBits(IPAddress address, int prefix)
    {
        var bytes = address.GetAddressBytes();

        for (var index = 0; index < bytes.Length; index++)
        {
            var bitsInByte = Math.Clamp(prefix - (index * 8), 0, 8);
            var mask = (byte)(0xFF << (8 - bitsInByte));

            bytes[index] &= mask;
        }

        return new IPAddress(bytes);
    }

    #endregion // Static methods
}