using MongoDB.Bson.Serialization.Attributes;

namespace EasyMeals.RecipeEngine.Infrastructure.Documents;

/// <summary>
///     Embedded document for discovery settings for a provider.
///     Maps to Domain.ValueObjects.DiscoveryConfig
/// </summary>
public class DiscoveryConfigDocument
{
    // Discovery strategy stored as string for flexibility and simple migrations
    [BsonElement("strategy")]
    [BsonRequired]
    public string Strategy { get; set; } = string.Empty;

    [BsonElement("recipeUrlPattern")]
    public string? RecipeUrlPattern { get; set; }

    [BsonElement("categoryUrlPattern")]
    public string? CategoryUrlPattern { get; set; }
}
