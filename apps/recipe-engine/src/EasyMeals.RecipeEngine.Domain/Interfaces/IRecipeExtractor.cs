using EasyMeals.RecipeEngine.Domain.Entities;

namespace EasyMeals.RecipeEngine.Domain.Interfaces;

/// <summary>
///     Domain service for extracting structured recipe data from fingerprints
///     Encapsulates the complex business logic of parsing recipes from web content
/// </summary>
public interface IRecipeExtractor
{
    /// <summary>
    ///     Extracts a structured recipe from a fingerprint
    /// </summary>
    /// <param name="fingerprint">Fingerprint containing scraped content</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Extracted recipe or null if extraction failed</returns>
    Task<Recipe?> ExtractRecipeAsync(Fingerprint fingerprint, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Determines if a fingerprint contains extractable recipe content
    /// </summary>
    /// <param name="fingerprint">Fingerprint to analyze</param>
    /// <returns>True if content appears to contain a recipe</returns>
    bool CanExtractRecipe(Fingerprint fingerprint);

    /// <summary>
    ///     Gets extraction confidence score for a fingerprint
    /// </summary>
    /// <param name="fingerprint">Fingerprint to analyze</param>
    /// <returns>Confidence score between 0.0 and 1.0</returns>
    decimal GetExtractionConfidence(Fingerprint fingerprint);
}