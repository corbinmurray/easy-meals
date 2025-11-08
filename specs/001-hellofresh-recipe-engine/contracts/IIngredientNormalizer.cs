namespace EasyMeals.RecipeEngine.Application.Interfaces;

/// <summary>
/// Service interface for normalizing provider-specific ingredient codes to canonical forms.
/// Uses a MongoDB mapping database to perform lookups.
/// </summary>
public interface IIngredientNormalizer
{
    /// <summary>
    /// Normalizes a provider-specific ingredient code to a canonical form.
    /// Returns null if no mapping exists (caller should log warning and continue processing).
    /// </summary>
    /// <param name="providerId">Provider identifier (e.g., "hellofresh")</param>
    /// <param name="providerCode">Provider-specific ingredient code (e.g., "HF-BROCCOLI-FROZEN-012")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Canonical ingredient form (e.g., "broccoli, frozen") or null if unmapped</returns>
    Task<string?> NormalizeAsync(
        string providerId,
        string providerCode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Batch normalizes multiple ingredient codes to reduce database round-trips.
    /// Returns a dictionary mapping provider codes to canonical forms (null values for unmapped codes).
    /// </summary>
    /// <param name="providerId">Provider identifier</param>
    /// <param name="providerCodes">Collection of provider-specific ingredient codes</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary of provider code → canonical form (null for unmapped)</returns>
    Task<IDictionary<string, string?>> NormalizeBatchAsync(
        string providerId,
        IEnumerable<string> providerCodes,
        CancellationToken cancellationToken = default);
}
