using EasyMeals.Shared.Data.Attributes;
using EasyMeals.Shared.Data.Documents;
using MongoDB.Bson.Serialization.Attributes;

namespace EasyMeals.RecipeEngine.Infrastructure.Documents.Recipe;

/// <summary>
///     MongoDB document for Recipe data with embedded structures
///     Optimized for NoSQL document storage and query performance
///     Follows DDD principles with proper aggregate boundaries and audit trail support
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
    ///     Replaces JSON serialization with native MongoDB document embedding
    /// </summary>
    [BsonElement("ingredients")]
    public List<IngredientDocument> Ingredients { get; set; } = [];

    /// <summary>
    ///     List of cooking instructions with embedded structure
    ///     Supports complex instruction objects with timing and media
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
    ///     Replaces JSON serialization with native document structure
    /// </summary>
    [BsonElement("nutritionInfo")]
    [BsonIgnoreIfNull]
    public NutritionalInfoDocument? NutritionInfo { get; set; }

    /// <summary>
    ///     List of tags for categorization and filtering
    ///     Supports business requirements for recipe organization
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
    ///     Source provider
    ///     Supports multi-provider architecture and data lineage tracking
    /// </summary>
    [BsonElement("sourceProvider")]
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>
    ///     Indicates if this recipe is active/published
    ///     Supports business workflow for recipe approval and publication
    /// </summary>
    [BsonElement("isActive")]
    public bool IsActive { get; set; } = true;

    /// <summary>
    ///     Cuisine type (e.g., "Italian", "Mexican", "Asian")
    ///     Supports recipe categorization and filtering
    /// </summary>
    [BsonElement("cuisine")]
    [BsonIgnoreIfNull]
    public string? Cuisine { get; set; }

    /// <summary>
    ///     Difficulty level (e.g., "Easy", "Medium", "Hard")
    ///     Supports user filtering and meal planning
    /// </summary>
    [BsonElement("difficulty")]
    [BsonIgnoreIfNull]
    public string? Difficulty { get; set; }

    /// <summary>
    ///     Recipe rating from 1-5 stars
    ///     Supports user feedback and recommendation systems
    /// </summary>
    [BsonElement("rating")]
    [BsonIgnoreIfNull]
    public decimal? Rating { get; set; }

    /// <summary>
    ///     Number of reviews/ratings
    ///     Statistical support for rating calculations
    /// </summary>
    [BsonElement("reviewCount")]
    public int ReviewCount { get; set; } = 0;

    // ===== Recipe Engine-Specific Fields =====

    /// <summary>
    ///     Provider identifier
    ///     Used by Recipe Engine for multi-provider support
    /// </summary>
    [BsonElement("providerId")]
    public string? ProviderId { get; set; }

    /// <summary>
    ///     Provider's internal recipe ID
    ///     Preserves original provider reference for auditing
    /// </summary>
    [BsonElement("providerRecipeId")]
    public string? ProviderRecipeId { get; set; }

    /// <summary>
    ///     Content-based fingerprint hash (SHA256) for duplicate detection
    ///     Generated from URL + title + description
    /// </summary>
    [BsonElement("fingerprintHash")]
    public string? FingerprintHash { get; set; }

    /// <summary>
    ///     When the recipe was scraped from the provider
    /// </summary>
    [BsonElement("scrapedAt")]
    public DateTime? ScrapedAt { get; set; }

    /// <summary>
    ///     When the recipe was last updated/reprocessed
    /// </summary>
    [BsonElement("lastUpdatedAt")]
    public DateTime? LastUpdatedAt { get; set; }

    /// <summary>
    ///     Ingredient references with provider codes and canonical forms
    ///     Stores both raw provider data and normalized ingredient mappings
    /// </summary>
    [BsonElement("ingredientReferences")]
    public List<IngredientReferenceDocument> IngredientReferences { get; set; } = [];

    /// <summary>
    ///     Calculated total time for the recipe
    ///     Business logic exposed as computed property
    /// </summary>
    [BsonIgnore]
    public int TotalTimeMinutes => PrepTimeMinutes + CookTimeMinutes;

    /// <summary>
    ///     Indicates if the recipe has complete nutritional information
    ///     Business logic for data quality validation
    /// </summary>
    [BsonIgnore]
    public bool HasNutritionInfo => NutritionInfo?.Calories.HasValue == true;

    /// <summary>
    ///     Gets ingredient count for quick reference
    ///     Useful for UI display and filtering
    /// </summary>
    [BsonIgnore]
    public int IngredientCount => Ingredients.Count;

    /// <summary>
    ///     Gets instruction step count for quick reference
    ///     Useful for UI display and complexity assessment
    /// </summary>
    [BsonIgnore]
    public int InstructionCount => Instructions.Count;
}