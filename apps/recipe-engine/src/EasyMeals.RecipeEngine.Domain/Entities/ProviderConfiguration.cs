using EasyMeals.RecipeEngine.Domain.ValueObjects;

namespace EasyMeals.RecipeEngine.Domain.Entities;

/// <summary>
///     Provider configuration aggregate root that allows for provider-specific configurations.
/// </summary>
public sealed class ProviderConfiguration
{
	public ProviderConfiguration(
		string providerName,
		DiscoveryStrategy discoveryStrategy,
		string recipeRootUrl,
		)
	{
		
	}
}