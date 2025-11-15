using EasyMeals.RecipeEngine.Domain.Entities;

namespace EasyMeals.RecipeEngine.Domain.Repositories;

/// <summary>
///     Repository contract for Recipe aggregate persistence.
/// </summary>
public interface IRecipeRepository
{
    /// <summary>
    ///     Get recipe by unique identifier.
    /// </summary>
    Task<Recipe?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get recipe by URL and provider.
    /// </summary>
    Task<Recipe?> GetByUrlAsync(
        string url,
        string providerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Save or update a recipe.
    /// </summary>
    Task SaveAsync(Recipe recipe, CancellationToken cancellationToken = default);
}