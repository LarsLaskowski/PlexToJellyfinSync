using PlexToJellyfinSync.Service.Security;

namespace PlexToJellyfinSync.Tests;

/// <summary>
/// Tests for <see cref="TokenComparer"/>
/// </summary>
[TestClass]
public sealed class TokenComparerTests
{
    #region Methods

    /// <summary>
    /// Two identical tokens compare as equal
    /// </summary>
    [TestMethod]
    public void TokenComparerMatchingTokensReturnsTrue()
    {
        var result = TokenComparer.FixedTimeEquals("s3cr3t-token", "s3cr3t-token");

        Assert.IsTrue(result, "Identical tokens should be equal!");
    }

    /// <summary>
    /// Tokens differing only in length compare as not equal
    /// </summary>
    [TestMethod]
    public void TokenComparerDifferentLengthReturnsFalse()
    {
        var result = TokenComparer.FixedTimeEquals("s3cr3t", "s3cr3t-token");

        Assert.IsFalse(result, "Tokens of different length should not be equal!");
    }

    /// <summary>
    /// Tokens differing in content compare as not equal
    /// </summary>
    [TestMethod]
    public void TokenComparerDifferentContentReturnsFalse()
    {
        var result = TokenComparer.FixedTimeEquals("s3cr3t-token", "wr0ng-token!");

        Assert.IsFalse(result, "Tokens with different content should not be equal!");
    }

    /// <summary>
    /// A null candidate never matches a configured token
    /// </summary>
    [TestMethod]
    public void TokenComparerNullCandidateReturnsFalse()
    {
        var result = TokenComparer.FixedTimeEquals(null, "s3cr3t-token");

        Assert.IsFalse(result, "A null candidate should not match a configured token!");
    }

    #endregion // Methods
}