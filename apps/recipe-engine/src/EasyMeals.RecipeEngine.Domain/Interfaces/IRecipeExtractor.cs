using EasyMeals.RecipeEngine.Domain.Entities;

namespace EasyMeals.RecipeEngine.Domain.Interfaces;

/// <summary>
///     Interface for extracting recipe data from HTML content
/// </summary>
public interface IRecipeExtractor
{
    /// <summary>
    ///     Extracts recipe data from HTML content
    /// </summary>
    /// <param name="htmlContent">The HTML content to parse</param>
    /// <param name="sourceUrl">The source URL of the recipe</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The extracted recipe or null if extraction fails</returns>
    Task<Recipe?> ExtractRecipeAsync(string htmlContent, string sourceUrl, CancellationToken cancellationToken = default);
}