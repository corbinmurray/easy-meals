using MongoDB.Bson.Serialization.Attributes;

namespace EasyMeals.Persistence.Mongo.Documents.ProviderConfiguration;

/// <summary>
/// Embedded document for crawl-specific settings.
/// </summary>
public class CrawlSettingsDocument
{
    [BsonElement("seedUrls")]
    public List<string> SeedUrls { get; set; } = [];

    [BsonElement("includePatterns")]
    public List<string> IncludePatterns { get; set; } = [];

    [BsonElement("excludePatterns")]
    public List<string> ExcludePatterns { get; set; } = [];

    [BsonElement("maxDepth")]
    public int MaxDepth { get; set; } = 3;

    [BsonElement("linkSelector")]
    public string LinkSelector { get; set; } = "a[href]";
}
