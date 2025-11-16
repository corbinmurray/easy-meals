using EasyMeals.RecipeEngine.Domain.ValueObjects;

namespace EasyMeals.RecipeEngine.Domain.Repositories;

public interface IProviderConfigurationRepository
{
	/// <summary>
	///     Get configuration for a specific provider.
	/// </summary>
	Task<ProviderConfiguration?> GetByProviderIdAsync(
		string providerId,
		CancellationToken cancellationToken = default);

	/// <summary>
	///     Get all enabled provider configurations.
	/// </summary>
	Task<IReadOnlyCollection<ProviderConfiguration>> GetAllEnabledAsync(
		CancellationToken cancellationToken = default);
}