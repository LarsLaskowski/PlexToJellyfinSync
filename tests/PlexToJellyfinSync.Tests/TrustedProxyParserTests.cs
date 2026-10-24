using System.Net;

using PlexToJellyfinSync.Core.Options;

namespace PlexToJellyfinSync.Tests;

/// <summary>
/// Tests for <see cref="TrustedProxyParser"/>
/// </summary>
[TestClass]
public sealed class TrustedProxyParserTests
{
    #region Methods

    /// <summary>
    /// A bare address becomes a single-host network
    /// </summary>
    /// <param name="entry">Entry text</param>
    /// <param name="address">Expected base address</param>
    /// <param name="prefix">Expected prefix length</param>
    [TestMethod]
    [DataRow("192.0.2.10", "192.0.2.10", 32)]
    [DataRow("2001:db8::1", "2001:db8::1", 128)]
    [DataRow(" 192.0.2.10 ", "192.0.2.10", 32)]
    public void TrustedProxyParserAddressReturnsSingleHostNetwork(string entry, string address, int prefix)
    {
        var parsed = TrustedProxyParser.TryParse(entry, out var network);

        Assert.IsTrue(parsed, "A valid address should be accepted!");
        Assert.AreEqual(IPAddress.Parse(address), network.BaseAddress, "The base address should be the address itself!");
        Assert.AreEqual(prefix, network.PrefixLength, "The prefix should cover the single host!");
    }

    /// <summary>
    /// A network entry keeps its prefix and clears the host bits
    /// </summary>
    /// <param name="entry">Entry text</param>
    /// <param name="address">Expected base address</param>
    /// <param name="prefix">Expected prefix length</param>
    [TestMethod]
    [DataRow("172.18.0.0/16", "172.18.0.0", 16)]
    [DataRow("172.18.0.1/16", "172.18.0.0", 16)]
    [DataRow("2001:db8::/32", "2001:db8::", 32)]
    public void TrustedProxyParserNetworkReturnsNetwork(string entry, string address, int prefix)
    {
        var parsed = TrustedProxyParser.TryParse(entry, out var network);

        Assert.IsTrue(parsed, "A valid network should be accepted!");
        Assert.AreEqual(IPAddress.Parse(address), network.BaseAddress, "The host bits should be cleared!");
        Assert.AreEqual(prefix, network.PrefixLength, "The prefix should be kept!");
    }

    /// <summary>
    /// Everything that is not a canonical address or network is rejected
    /// </summary>
    /// <param name="entry">Entry text</param>
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("proxy.local")]
    [DataRow("10.0.0.0/33")]
    [DataRow("2001:db8::/129")]
    [DataRow("10.0.0.0/")]
    [DataRow("10.0.0.0/-1")]
    [DataRow("10.0.0.0/+8")]
    [DataRow("10.0.0.0/08")]
    [DataRow("10.0.0.0/8/8")]
    [DataRow("192.168.001.010")]
    [DataRow("010.0.0.0/8")]
    [DataRow("167772160/8")]
    [DataRow("0x0a000000/8")]
    [DataRow("10.1")]
    [DataRow("10/8")]
    [DataRow("10.0.0.1:80")]
    [DataRow("fe80::1%eth0/64")]
    [DataRow("[::1]/128")]
    [DataRow("::ffff:10.0.0.0/104")]
    public void TrustedProxyParserInvalidEntryReturnsFalse(string? entry)
    {
        var parsed = TrustedProxyParser.TryParse(entry, out _);

        Assert.IsFalse(parsed, "An entry that is not a canonical address or network should be rejected!");
    }

    #endregion // Methods
}