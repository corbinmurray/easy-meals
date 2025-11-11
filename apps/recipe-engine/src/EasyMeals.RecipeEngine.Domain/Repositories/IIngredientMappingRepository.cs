using EasyMeals.RecipeEngine.Domain.Entities;

namespace EasyMeals.RecipeEngine.Domain.Repositories;

/// <summary>
///     Repository contract for IngredientMapping aggregate persistence.
/// </summary>
public interface IIngredientMappingRepository
{
	/// <summary>
	///     Get ingredient mapping by provider ID and provider code.
	/// </summary>
	Task<IngredientMapping?> GetByCodeAsync(
		string providerId,
		string providerCode,
		CancellationToken cancellationToken = default);

	/// <summary>
	///     Get all ingredient mappings for a provider.
	/// </summary>
	Task<IEnumerable<IngredientMapping>> GetAllByProviderAsync(
		string providerId,
		CancellationToken cancellationToken = default);

	/// <summary>
	///     Save or update an ingredient mapping.
	/// </summary>
	Task SaveAsync(IngredientMapping mapping, CancellationToken cancellationToken = default);

	/// <summary>
	///     Get unmapped ingredient codes (codes that appear in recipes but have no mapping).
	/// </summary>
	Task<IEnumerable<IngredientMapping>> GetUnmappedCodesAsync(
		string providerId,
		CancellationToken cancellationToken = default);
}