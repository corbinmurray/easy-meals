using MongoDB.Bson.Serialization.Attributes;

namespace EasyMeals.RecipeEngine.Infrastructure.Documents.Recipe;

/// <summary>
/// Embedded document for ingredient references with provider code and canonical form mapping.
/// Used by Recipe Engine to store both raw provider data and normalized forms.
/// </summary>
public class IngredientReferenceDocument
{
    [BsonElement("providerCode")]
    [BsonRequired]
    public string ProviderCode { get; set; } = string.Empty;

    [BsonElement("canonicalForm")]
    public string? CanonicalForm { get; set; }

    [BsonElement("quantity")]
    [BsonRequired]
    public string Quantity { get; set; } = string.Empty;

    [BsonElement("displayOrder")]
    [BsonRequired]
    public int DisplayOrder { get; set; }
}
