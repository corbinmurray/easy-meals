using EasyMeals.Shared.Data;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EasyMeals.RecipeEngine.Infrastructure.Documents;

/// <summary>
/// MongoDB document for tracking processed recipes via content-based fingerprints.
/// </summary>
[BsonCollection("recipe_fingerprints")]
public class RecipeFingerprintDocument : BaseDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    [BsonElement("fingerprintHash")]
    [BsonRequired]
    public string FingerprintHash { get; set; } = string.Empty;

    [BsonElement("providerId")]
    [BsonRequired]
    public string ProviderId { get; set; } = string.Empty;

    [BsonElement("recipeUrl")]
    [BsonRequired]
    public string RecipeUrl { get; set; } = string.Empty;

    [BsonElement("recipeId")]
    [BsonRequired]
    [BsonRepresentation(BsonType.String)]
    public Guid RecipeId { get; set; }

    [BsonElement("processedAt")]
    [BsonRequired]
    public DateTime ProcessedAt { get; set; }
}
