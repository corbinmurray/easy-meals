using EasyMeals.Persistence.Mongo.Attributes;
using MongoDB.Bson.Serialization.Attributes;

namespace EasyMeals.Persistence.Mongo.Documents.Recipes;

/// <summary>
///     MongoDB document for Recipe data with embedded structures.
///     This is the canonical recipe document shared across all applications.
///     Optimized for NoSQL document storage and query performance.
/// </summary>
[BsonCollection("recipes")]
public class RecipeDocument : BaseSoftDeletableDocument
{
    /// <summary>
    ///     Recipe title
    /// </summary>
    [BsonElement("title")]
    [BsonRequired]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    ///     Recipe description
    /// </summary>
    [BsonElement("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    ///     List of ingredients with embedded structure
    /// </summary>
    [BsonElement("ingredients")]
    public List<IngredientDocument> Ingredients { get; set; } = [];

    /// <summary>
    ///     List of cooking instructions with embedded structure
    /// </summary>
    [BsonElement("instructions")]
    public List<InstructionDocument> Instructions { get; set; } = [];

    /// <summary>
    ///     URL to the main recipe image
    /// </summary>
    [BsonElement("imageUrl")]
    public string ImageUrl { get; set; } = string.Empty;

    /// <summary>
    ///     Preparation time in minutes
    /// </summary>
    [BsonElement("prepTimeMinutes")]
    public int PrepTimeMinutes { get; set; }

    /// <summary>
    ///     Cooking time in minutes
    /// </summary>
    [BsonElement("cookTimeMinutes")]
    public int CookTimeMinutes { get; set; }

    /// <summary>
    ///     Number of servings this recipe makes
    /// </summary>
    [BsonElement("servings")]
    public int Servings { get; set; }

    /// <summary>
    ///     Embedded nutritional information
    /// </summary>
    [BsonElement("nutritionInfo")]
    [BsonIgnoreIfNull]
    public NutritionalInfoDocument? NutritionInfo { get; set; }

    /// <summary>
    ///     List of tags for categorization and filtering
    /// </summary>
    [BsonElement("tags")]
    public List<string> Tags { get; set; } = [];

    /// <summary>
    ///     Original source URL where the recipe was found
    /// </summary>
    [BsonElement("sourceUrl")]
    [BsonRequired]
    public string SourceUrl { get; set; } = string.Empty;

    /// <summary>
    ///     Source provider (e.g., "HelloFresh", "AllRecipes", etc.)
    /// </summary>
    [BsonElement("providerName")]
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>
    ///     Indicates if this recipe is active/published
    /// </summary>
    [BsonElement("isActive")]
    public bool IsActive { get; set; } = true;

    /// <summary>
    ///     Cuisine type (e.g., "Italian", "Mexican", "Asian")
    /// </summary>
    [BsonElement("cuisine")]
    [BsonIgnoreIfNull]
    public string? Cuisine { get; set; }

    /// <summary>
    ///     Difficulty level (e.g., "Easy", "Medium", "Hard")
    /// </summary>
    [BsonElement("difficulty")]
    [BsonIgnoreIfNull]
    public string? Difficulty { get; set; }

    /// <summary>
    ///     Recipe rating from 1-5 stars
    /// </summary>
    [BsonElement("rating")]
    [BsonIgnoreIfNull]
    public decimal? Rating { get; set; }

    /// <summary>
    ///     Number of reviews/ratings
    /// </summary>
    [BsonElement("reviewCount")]
    public int ReviewCount { get; set; }
}
