using Microsoft.Extensions.Options;

using PlexToJellyfinSync.Core.Options;
using PlexToJellyfinSync.Service;

namespace PlexToJellyfinSync.Tests;

/// <summary>
/// Tests for <see cref="PathMapper"/>
/// </summary>
[TestClass]
public sealed class PathMapperTests
{
    #region Methods

    /// <summary>
    /// Mapping a matching plex path returns the translated local path
    /// </summary>
    [TestMethod]
    public void PathMapperMapKnownPathReturnsLocalPath()
    {
        var mapper = CreateMapper(new PathMapping
                                  {
                                      Plex = "/data/Movies",
                                      Local = "/media/Movies"
                                  });

        var result = mapper.MapToLocal("/data/Movies/Heat (1995)/Heat.mkv");

        Assert.AreEqual("/media/Movies/Heat (1995)/Heat.mkv", result, "Path was not mapped correctly!");
    }

    /// <summary>
    /// The longest matching prefix is used
    /// </summary>
    [TestMethod]
    public void PathMapperOverlappingPrefixesUsesLongestMatch()
    {
        var mapper = CreateMapper(new PathMapping
                                  {
                                      Plex = "/data",
                                      Local = "/a"
                                  },
                                  new PathMapping
                                  {
                                      Plex = "/data/Movies",
                                      Local = "/b"
                                  });

        var result = mapper.MapToLocal("/data/Movies/x.mkv");

        Assert.AreEqual("/b/x.mkv", result, "Longest prefix was not selected!");
    }

    /// <summary>
    /// An unmapped path returns null
    /// </summary>
    [TestMethod]
    public void PathMapperUnknownPathReturnsNull()
    {
        var mapper = CreateMapper(new PathMapping
                                  {
                                      Plex = "/data/Movies",
                                      Local = "/media/Movies"
                                  });

        var result = mapper.MapToLocal("/other/path/x.mkv");

        Assert.IsNull(result, "Unmapped path should return null!");
    }

    /// <summary>
    /// Backslash paths are normalized before matching
    /// </summary>
    [TestMethod]
    public void PathMapperBackslashInputIsNormalized()
    {
        var mapper = CreateMapper(new PathMapping
                                  {
                                      Plex = "/data/Movies",
                                      Local = "/media/Movies"
                                  });

        var result = mapper.MapToLocal("\\data\\Movies\\x.mkv");

        Assert.AreEqual("/media/Movies/x.mkv", result, "Backslashes were not normalized!");
    }

    /// <summary>
    /// A path containing a traversal sequence in the middle is rejected
    /// </summary>
    [TestMethod]
    public void PathMapperTraversalSequenceInMiddleReturnsNull()
    {
        var mapper = CreateMapper(new PathMapping
                                  {
                                      Plex = "/data/Movies",
                                      Local = "/media/Movies"
                                  });

        var result = mapper.MapToLocal("/data/Movies/../../../etc/passwd");

        Assert.IsNull(result, "Path with traversal sequence should return null!");
    }

    /// <summary>
    /// A path ending with a traversal sequence is rejected
    /// </summary>
    [TestMethod]
    public void PathMapperTraversalSequenceAtEndReturnsNull()
    {
        var mapper = CreateMapper(new PathMapping
                                  {
                                      Plex = "/data/Movies",
                                      Local = "/media/Movies"
                                  });

        var result = mapper.MapToLocal("/data/Movies/Heat/..");

        Assert.IsNull(result, "Path ending with traversal sequence should return null!");
    }

    /// <summary>
    /// Create a path mapper for the given mappings
    /// </summary>
    /// <param name="mappings">Mappings</param>
    /// <returns>Path mapper</returns>
    private static PathMapper CreateMapper(params PathMapping[] mappings)
    {
        return new PathMapper(Options.Create(mappings.ToList()));
    }

    #endregion // Methods
}