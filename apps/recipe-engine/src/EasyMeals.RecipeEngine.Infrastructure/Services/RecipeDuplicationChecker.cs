using EasyMeals.RecipeEngine.Domain.Entities;
using EasyMeals.RecipeEngine.Domain.Repositories;
using EasyMeals.RecipeEngine.Domain.Services;

namespace EasyMeals.RecipeEngine.Infrastructure.Services;

/// <summary>
///     SHA256-based fingerprinting service for duplicate detection.
/// </summary>
public class RecipeDuplicationChecker : IRecipeDuplicationChecker
{
	private readonly IRecipeFingerprintRepository _fingerprintRepository;

	public RecipeDuplicationChecker(IRecipeFingerprintRepository fingerprintRepository) =>
		_fingerprintRepository = fingerprintRepository ?? throw new ArgumentNullException(nameof(fingerprintRepository));

	public async Task<bool> IsDuplicateAsync(
		string url,
		string fingerprintHash,
		string providerId,
		CancellationToken cancellationToken = default) =>
		await _fingerprintRepository.ExistsAsync(url, providerId, cancellationToken);

	public async Task<RecipeFingerprint?> GetExistingFingerprintAsync(
		string url,
		string providerId,
		CancellationToken cancellationToken = default) =>
		await _fingerprintRepository.GetByUrlAsync(url, providerId, cancellationToken);
}