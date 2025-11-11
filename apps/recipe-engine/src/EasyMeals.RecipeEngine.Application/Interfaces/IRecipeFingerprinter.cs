namespace EasyMeals.RecipeEngine.Application.Interfaces;

/// <summary>
///     Service interface for generating content-based fingerprints for recipes.
///     Uses SHA256 hash of URL + title + description for duplicate detection.
/// </summary>
public interface IRecipeFingerprinter
{
    /// <summary>
    ///     Generates a content-based fingerprint (SHA256 hash) for a recipe.
    ///     The fingerprint is computed from URL, title, and description to enable robust duplicate detection.
    /// </summary>
    /// <param name="url">Recipe URL (normalized)</param>
    /// <param name="title">Recipe title (trimmed, lowercased)</param>
    /// <param name="description">Recipe description (first 200 chars, trimmed, lowercased)</param>
    /// <returns>SHA256 hash as hex string (64 characters)</returns>
    string GenerateFingerprint(string url, string title, string description);

    /// <summary>
    ///     Checks if a recipe with the given fingerprint has already been processed.
    ///     Queries the recipe_fingerprints MongoDB collection.
    /// </summary>
    /// <param name="fingerprintHash">SHA256 fingerprint hash</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if duplicate exists, false otherwise</returns>
    Task<bool> IsDuplicateAsync(
        string fingerprintHash,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Persists a recipe fingerprint to MongoDB after successful recipe processing.
    ///     Prevents duplicate processing on future runs.
    /// </summary>
    /// <param name="fingerprintHash">SHA256 fingerprint hash</param>
    /// <param name="providerId">Provider identifier</param>
    /// <param name="recipeUrl">Recipe URL</param>
    /// <param name="recipeId">Recipe entity ID (reference to recipes collection)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task StoreFingerprintAsync(
        string fingerprintHash,
        string providerId,
        string recipeUrl,
        Guid recipeId,
        CancellationToken cancellationToken = default);
}