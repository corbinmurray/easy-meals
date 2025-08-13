using EasyMeals.Crawler.Domain.ValueObjects;

namespace EasyMeals.Crawler.Domain.Entities;

/// <summary>
/// Recipe aggregate root representing a complete meal recipe with all associated data.
/// Maintains consistency boundaries for recipe information including ingredients, steps, and nutrition.
/// </summary>
public class Recipe
{
    /// <summary>
    /// Unique identifier for the recipe, typically derived from the source URL or site-specific ID.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Display name of the recipe.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Collection of ingredients required for this recipe.
    /// </summary>
    public required IReadOnlyList<Ingredient> Ingredients { get; init; }

    /// <summary>
    /// Ordered list of cooking/preparation steps.
    /// </summary>
    public required IReadOnlyList<RecipeStep> Steps { get; init; }

    /// <summary>
    /// Nutritional information for the recipe, if available.
    /// </summary>
    public NutritionInfo? Nutrition { get; init; }

    /// <summary>
    /// Collection of image URLs associated with this recipe.
    /// </summary>
    public IReadOnlyList<string> ImageUrls { get; init; } = [];

    /// <summary>
    /// Recipe tags for categorization (dietary info, cuisine type, etc.).
    /// </summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>
    /// Preparation time in minutes.
    /// </summary>
    public int? PrepTimeMinutes { get; init; }

    /// <summary>
    /// Cooking time in minutes.
    /// </summary>
    public int? CookTimeMinutes { get; init; }

    /// <summary>
    /// Number of servings this recipe yields.
    /// </summary>
    public int? Servings { get; init; }

    /// <summary>
    /// Source URL where this recipe was crawled from.
    /// </summary>
    public required string SourceUrl { get; init; }

    /// <summary>
    /// Timestamp when this recipe was last crawled.
    /// </summary>
    public DateTimeOffset CrawledAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Validates that the recipe contains all required information.
    /// Domain business rule: Recipe must have at least a title, one ingredient, and one step.
    /// </summary>
    /// <returns>True if the recipe is valid for persistence.</returns>
    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(Title) &&
               Ingredients.Count > 0 &&
               Steps.Count > 0 &&
               !string.IsNullOrWhiteSpace(SourceUrl);
    }
}
