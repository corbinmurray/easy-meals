using EasyMeals.RecipeEngine.Domain.Entities;

namespace EasyMeals.RecipeEngine.Domain.Interfaces;

/// <summary>
///     Domain service for extracting structured recipe data from raw HTML content
///     Encapsulates the complex business logic of parsing recipes from web content
/// </summary>
public interface IRecipeExtractor
{
    /// <summary>
    ///     Extracts a structured recipe from raw HTML content and fingerprint metadata
    /// </summary>
    /// <param name="rawContent">Raw HTML content to extract from</param>
    /// <param name="fingerprint">Fingerprint containing metadata about the scraped content</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Extracted recipe or null if extraction failed</returns>
    Task<Recipe?> ExtractRecipeAsync(string rawContent, Fingerprint fingerprint, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Determines if raw content contains extractable recipe content
    /// </summary>
    /// <param name="rawContent">Raw HTML content to analyze</param>
    /// <returns>True if content appears to contain a recipe</returns>
    bool CanExtractRecipe(string rawContent);

    /// <summary>
    ///     Gets extraction confidence score for raw content
    /// </summary>
    /// <param name="rawContent">Raw HTML content to analyze</param>
    /// <returns>Confidence score between 0.0 and 1.0</returns>
    decimal GetExtractionConfidence(string rawContent);
}