using EasyMeals.RecipeEngine.Domain.Interfaces;
using EasyMeals.RecipeEngine.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace EasyMeals.RecipeEngine.Infrastructure.Discovery;

/// <summary>
/// T112: Factory for creating discovery service instances based on DiscoveryStrategy
/// Enables runtime selection of discovery implementation
/// </summary>
public interface IDiscoveryServiceFactory
{
	/// <summary>
	/// Creates a discovery service instance for the specified strategy
	/// </summary>
	/// <param name="strategy">The discovery strategy to use</param>
	/// <returns>Discovery service implementation</returns>
	IDiscoveryService CreateDiscoveryService(DiscoveryStrategy strategy);
}

/// <summary>
/// Default implementation of IDiscoveryServiceFactory
/// Resolves discovery services from DI container based on strategy
/// </summary>
public class DiscoveryServiceFactory : IDiscoveryServiceFactory
{
	private readonly IServiceProvider _serviceProvider;

	public DiscoveryServiceFactory(IServiceProvider serviceProvider)
	{
		_serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
	}

	/// <summary>
	/// Creates a discovery service instance for the specified strategy
	/// </summary>
	public IDiscoveryService CreateDiscoveryService(DiscoveryStrategy strategy)
	{
		return strategy switch
		{
			DiscoveryStrategy.Static => _serviceProvider.GetRequiredService<StaticCrawlDiscoveryService>(),
			DiscoveryStrategy.Dynamic => _serviceProvider.GetRequiredService<DynamicCrawlDiscoveryService>(),
			DiscoveryStrategy.Api => _serviceProvider.GetRequiredService<ApiDiscoveryService>(),
			_ => throw new ArgumentException($"Unsupported discovery strategy: {strategy}", nameof(strategy))
		};
	}
}
