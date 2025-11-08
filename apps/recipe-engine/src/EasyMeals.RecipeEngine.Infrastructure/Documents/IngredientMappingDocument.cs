using EasyMeals.Shared.Data.Attributes;
using EasyMeals.Shared.Data.Documents;
using MongoDB.Bson.Serialization.Attributes;

namespace EasyMeals.RecipeEngine.Infrastructure.Documents;

/// <summary>
///     MongoDB document for mapping provider-specific ingredient codes to canonical forms.
/// </summary>
[BsonCollection("ingredient_mappings")]
public class IngredientMappingDocument : BaseDocument
{
	[BsonElement("providerId")]
	[BsonRequired]
	public string ProviderId { get; set; } = string.Empty;

	[BsonElement("providerCode")]
	[BsonRequired]
	public string ProviderCode { get; set; } = string.Empty;

	[BsonElement("canonicalForm")]
	[BsonRequired]
	public string CanonicalForm { get; set; } = string.Empty;

	[BsonElement("notes")] public string? Notes { get; set; }
}