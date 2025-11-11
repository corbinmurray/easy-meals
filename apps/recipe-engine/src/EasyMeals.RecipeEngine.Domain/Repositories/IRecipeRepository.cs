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

    /// <summary>
    ///     Save multiple recipes in batch (for performance).
    /// </summary>
    Task SaveBatchAsync(
        IEnumerable<Recipe> recipes,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Count recipes for a provider (useful for stats).
    /// </summary>
    Task<int> CountByProviderAsync(string providerId, CancellationToken cancellationToken = default);
}