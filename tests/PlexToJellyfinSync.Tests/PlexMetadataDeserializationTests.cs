using System.Text.Json;

using PlexToJellyfinSync.Data.Plex;
using PlexToJellyfinSync.Service;

namespace PlexToJellyfinSync.Tests;

/// <summary>
/// Tests for deserializing Plex API responses with <see cref="PlexJsonOptions"/>
/// </summary>
[TestClass]
public sealed class PlexMetadataDeserializationTests
{
    #region Constants

    private const string MetadataJson = """
                                        {
                                          "MediaContainer": {
                                            "Metadata": [
                                              {
                                                "ratingKey": "12345",
                                                "type": "movie",
                                                "title": "Heat",
                                                "year": 1995,
                                                "guid": "com.plexapp.agents.imdb://tt0113277?lang=en",
                                                "Guid": [
                                                  { "id": "imdb://tt0113277" },
                                                  { "id": "tmdb://949" }
                                                ],
                                                "Media": [ { "Part": [ { "file": "/data/Movies/Heat (1995)/Heat.mkv" } ] } ],
                                                "viewCount": 2,
                                                "lastViewedAt": 1700000000
                                              }
                                            ]
                                          }
                                        }
                                        """;

    #endregion // Constants

    #region Methods

    /// <summary>
    /// The lowercase legacy <c>guid</c> string does not collide with the uppercase <c>Guid</c> array
    /// </summary>
    [TestMethod]
    public void PlexMetadataResponseWithLowercaseGuidDeserializesGuidArray()
    {
        var response = JsonSerializer.Deserialize<PlexMetadataResponse>(MetadataJson, PlexJsonOptions.Default);

        Assert.IsNotNull(response, "Response should not be null!");
        Assert.IsNotNull(response.MediaContainer, "Media container should not be null!");
        Assert.IsNotNull(response.MediaContainer.Metadata, "Metadata should not be null!");
        Assert.HasCount(1, response.MediaContainer.Metadata, "Expected exactly one metadata entry!");

        var metadata = response.MediaContainer.Metadata[0];

        Assert.IsNotNull(metadata.Guid, "Guid array should not be null!");
        Assert.HasCount(2, metadata.Guid, "Expected two external identifiers!");
        Assert.AreEqual("imdb://tt0113277", metadata.Guid[0].Id, "First identifier mismatch!");
        Assert.AreEqual(1995, metadata.Year, "Year should be parsed!");
        Assert.AreEqual("/data/Movies/Heat (1995)/Heat.mkv", metadata.Media?[0].Part?[0].File, "File path should be parsed!");
    }

    #endregion // Methods
}