using EasyMeals.RecipeEngine.Domain.Entities;

namespace EasyMeals.RecipeEngine.Domain.Repositories;

/// <summary>
/// Repository contract for RecipeFingerprint aggregate persistence.
/// </summary>
public interface IRecipeFingerprintRepository
{
    /// <summary>
    /// Get fingerprint by recipe URL and provider.
    /// </summary>
    Task<RecipeFingerprint?> GetByUrlAsync(
        string url,
        string providerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a fingerprint exists for a URL and provider.
    /// </summary>
    Task<bool> ExistsAsync(
        string url,
        string providerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Save a fingerprint.
    /// </summary>
    Task SaveAsync(RecipeFingerprint fingerprint, CancellationToken cancellationToken = default);

    /// <summary>
    /// Save multiple fingerprints in batch (for performance).
    /// </summary>
    Task SaveBatchAsync(
        IEnumerable<RecipeFingerprint> fingerprints,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Count fingerprints for a provider (useful for stats).
    /// </summary>
    Task<int> CountByProviderAsync(string providerId, CancellationToken cancellationToken = default);
}
