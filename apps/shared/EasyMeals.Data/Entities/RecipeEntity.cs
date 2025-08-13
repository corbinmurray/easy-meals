using System.ComponentModel.DataAnnotations;

namespace EasyMeals.Data.Entities;

/// <summary>
/// EF Core entity for Recipe data
/// This is the data layer representation - kept separate from domain entities for Clean Architecture
/// </summary>
public class RecipeEntity
{
    [Key]
    public string Id { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(500)]
    public string Title { get; set; } = string.Empty;
    
    [MaxLength(2000)]
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// JSON serialized list of ingredients
    /// </summary>
    public string IngredientsJson { get; set; } = "[]";
    
    /// <summary>
    /// JSON serialized list of cooking instructions
    /// </summary>
    public string InstructionsJson { get; set; } = "[]";
    
    [MaxLength(1000)]
    public string ImageUrl { get; set; } = string.Empty;
    
    public int PrepTimeMinutes { get; set; }
    public int CookTimeMinutes { get; set; }
    public int Servings { get; set; }
    
    /// <summary>
    /// JSON serialized nutrition information
    /// </summary>
    public string NutritionInfoJson { get; set; } = "{}";
    
    /// <summary>
    /// JSON serialized list of tags
    /// </summary>
    public string TagsJson { get; set; } = "[]";
    
    [Required]
    [MaxLength(1000)]
    public string SourceUrl { get; set; } = string.Empty;
    
    /// <summary>
    /// Source provider (e.g., "HelloFresh", "AllRecipes", etc.)
    /// </summary>
    [MaxLength(100)]
    public string SourceProvider { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Indicates if this recipe is active/published
    /// </summary>
    public bool IsActive { get; set; } = true;
}
