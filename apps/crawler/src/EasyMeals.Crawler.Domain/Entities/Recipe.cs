namespace EasyMeals.Crawler.Domain.Entities;

/// <summary>
///     Represents a recipe aggregate root containing all recipe data
/// </summary>
public class Recipe
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Ingredients { get; set; } = new();
    public List<string> Instructions { get; set; } = new();
    public string ImageUrl { get; set; } = string.Empty;
    public int PrepTimeMinutes { get; set; }
    public int CookTimeMinutes { get; set; }
    public int Servings { get; set; }
    public Dictionary<string, string> NutritionInfo { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public string SourceUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}