using EasyMeals.RecipeEngine.Domain.Entities;

namespace EasyMeals.RecipeEngine.Domain.Services;

/// <summary>
///     Domain service for checking recipe duplicates across aggregates.
/// </summary>
public interface IRecipeDuplicationChecker
{
    /// <summary>
    ///     Check if a recipe is a duplicate based on URL and fingerprint.
    /// </summary>
    Task<bool> IsDuplicateAsync(
        string url,
        string fingerprintHash,
        string providerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get existing fingerprint for a recipe URL.
    /// </summary>
    Task<RecipeFingerprint?> GetExistingFingerprintAsync(
        string url,
        string providerId,
        CancellationToken cancellationToken = default);
}