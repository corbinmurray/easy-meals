namespace EasyMeals.Crawler.Domain.Entities;

/// <summary>
///     Represents a recipe aggregate root containing all recipe data
/// </summary>
public record Recipe(
    string Id,
    string Title,
    string Description,
    IEnumerable<string> Ingredients,
    IEnumerable<string> Instructions,
    string ImageUrl,
    int PrepTimeMinutes,
    int CookTimeMinutes,
    int Servings,
    Dictionary<string, string> NutritionInfo,
    IEnumerable<string> Tags,
    string SourceUrl);