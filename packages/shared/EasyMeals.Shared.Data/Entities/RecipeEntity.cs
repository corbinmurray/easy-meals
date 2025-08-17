using System.ComponentModel.DataAnnotations;

namespace EasyMeals.Shared.Data.Entities;

/// <summary>
/// EF Core entity for Recipe data
/// This is the data layer representation - kept separate from domain entities for Clean Architecture
/// Follows DDD principles with proper aggregate boundaries and audit trail support
/// </summary>
public class RecipeEntity : BaseSoftDeletableEntity
{
    [Required]
    [MaxLength(500)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// JSON serialized list of ingredients
    /// Using JSON for flexibility while maintaining ACID properties
    /// </summary>
    public string IngredientsJson { get; set; } = "[]";

    /// <summary>
    /// JSON serialized list of cooking instructions
    /// Supports complex instruction objects with timing and media
    /// </summary>
    public string InstructionsJson { get; set; } = "[]";

    [MaxLength(1000)]
    public string ImageUrl { get; set; } = string.Empty;

    public int PrepTimeMinutes { get; set; }
    public int CookTimeMinutes { get; set; }
    public int Servings { get; set; }

    /// <summary>
    /// JSON serialized nutrition information
    /// Flexible schema for different nutrition data formats
    /// </summary>
    public string NutritionInfoJson { get; set; } = "{}";

    /// <summary>
    /// JSON serialized list of tags
    /// Supports categorization and filtering business requirements
    /// </summary>
    public string TagsJson { get; set; } = "[]";

    [Required]
    [MaxLength(1000)]
    public string SourceUrl { get; set; } = string.Empty;

    /// <summary>
    /// Source provider (e.g., "HelloFresh", "AllRecipes", etc.)
    /// Supports multi-provider architecture and data lineage tracking
    /// </summary>
    [MaxLength(100)]
    public string SourceProvider { get; set; } = string.Empty;

    /// <summary>
    /// Indicates if this recipe is active/published
    /// Supports business workflow for recipe approval and publication
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Calculated total time for the recipe
    /// Business logic exposed as computed property
    /// </summary>
    public int TotalTimeMinutes => PrepTimeMinutes + CookTimeMinutes;
}
