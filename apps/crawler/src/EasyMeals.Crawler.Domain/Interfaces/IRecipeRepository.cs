using EasyMeals.Crawler.Domain.Entities;

namespace EasyMeals.Crawler.Domain.Interfaces;

/// <summary>
/// Repository interface for persisting recipes
/// Abstracts the persistence mechanism following Clean Architecture principles
/// </summary>
public interface IRecipeRepository
{
    /// <summary>
    /// Saves a single recipe to the persistence store
    /// </summary>
    /// <param name="recipe">The recipe to save</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if saved successfully, false otherwise</returns>
    Task<bool> SaveRecipeAsync(Recipe recipe, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves multiple recipes in a batch operation
    /// </summary>
    /// <param name="recipes">The recipes to save</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of recipes successfully saved</returns>
    Task<int> SaveRecipesAsync(IEnumerable<Recipe> recipes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a recipe with the given ID already exists
    /// </summary>
    /// <param name="recipeId">The recipe ID to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the recipe exists, false otherwise</returns>
    Task<bool> RecipeExistsAsync(string recipeId, CancellationToken cancellationToken = default);
}
