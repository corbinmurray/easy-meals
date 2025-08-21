using MongoDB.Bson.Serialization.Attributes;

namespace EasyMeals.RecipeEngine.Infrastructure.Documents.Recipe;

/// <summary>
///     Embedded document representing an ingredient in a recipe
///     Optimized for MongoDB document structure and query performance
/// </summary>
public class IngredientDocument
{
	/// <summary>
	///     Name of the ingredient (e.g., "flour", "chicken breast")
	/// </summary>
	[BsonElement("name")]
	public string Name { get; set; } = string.Empty;

	/// <summary>
	///     Amount/quantity of the ingredient (e.g., "2", "1.5")
	/// </summary>
	[BsonElement("amount")]
	public string Amount { get; set; } = string.Empty;

	/// <summary>
	///     Unit of measurement (e.g., "cups", "lbs", "tbsp")
	/// </summary>
	[BsonElement("unit")]
	public string Unit { get; set; } = string.Empty;

	/// <summary>
	///     Additional notes or preparation instructions (e.g., "diced", "room temperature")
	/// </summary>
	[BsonElement("notes")]
	[BsonIgnoreIfNull]
	public string? Notes { get; set; }

	/// <summary>
	///     Whether this ingredient is optional in the recipe
	/// </summary>
	[BsonElement("isOptional")]
	public bool IsOptional { get; set; } = false;

	/// <summary>
	///     Display order in the ingredient list
	/// </summary>
	[BsonElement("order")]
	public int Order { get; set; }

	/// <summary>
	///     Full display text combining amount, unit, and name
	///     Calculated property for UI display
	/// </summary>
	[BsonIgnore]
	public string DisplayText => $"{Amount} {Unit} {Name}".Trim();
}