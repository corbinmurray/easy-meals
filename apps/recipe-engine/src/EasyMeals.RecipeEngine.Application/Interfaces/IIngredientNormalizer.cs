namespace EasyMeals.RecipeEngine.Application.Interfaces;

/// <summary>
///     Service contract for normalizing provider-specific ingredient codes to canonical forms.
/// </summary>
public interface IIngredientNormalizer
{
    /// <summary>
    ///     Normalize a single provider ingredient code to its canonical form.
    ///     Returns null if no mapping exists.
    /// </summary>
    /// <param name="providerId">The provider identifier</param>
    /// <param name="providerCode">The provider-specific ingredient code</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The canonical form if mapping exists, otherwise null</returns>
    Task<string?> NormalizeAsync(
		string providerId,
		string providerCode,
		CancellationToken cancellationToken = default);

    /// <summary>
    ///     Normalize a batch of provider ingredient codes to their canonical forms.
    ///     Returns a dictionary mapping provider codes to canonical forms (null for unmapped).
    /// </summary>
    /// <param name="providerId">The provider identifier</param>
    /// <param name="providerCodes">Collection of provider-specific ingredient codes</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary mapping provider codes to canonical forms (null for unmapped)</returns>
    Task<IDictionary<string, string?>> NormalizeBatchAsync(
		string providerId,
		IEnumerable<string> providerCodes,
		CancellationToken cancellationToken = default);
}