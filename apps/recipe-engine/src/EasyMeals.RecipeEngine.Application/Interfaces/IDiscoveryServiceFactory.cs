using EasyMeals.RecipeEngine.Domain.Interfaces;
using EasyMeals.RecipeEngine.Domain.ValueObjects;

namespace EasyMeals.RecipeEngine.Application.Interfaces;

/// <summary>
/// Factory abstraction to resolve a discovery service implementation by strategy.
/// Defined in the Application layer to avoid reverse dependency on Infrastructure.
/// Implementations live in the Infrastructure layer.
/// </summary>
public interface IDiscoveryServiceFactory
{
    /// <summary>
    /// Resolve an <see cref="IDiscoveryService"/> for the given <see cref="DiscoveryStrategy"/>.
    /// </summary>
    /// <param name="strategy">The discovery strategy to use.</param>
    /// <returns>An <see cref="IDiscoveryService"/> suitable for the strategy.</returns>
    IDiscoveryService CreateDiscoveryService(DiscoveryStrategy strategy);
}
