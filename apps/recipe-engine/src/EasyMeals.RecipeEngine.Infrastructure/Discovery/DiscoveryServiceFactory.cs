using EasyMeals.RecipeEngine.Domain.Interfaces;
using EasyMeals.RecipeEngine.Domain.ValueObjects;
using EasyMeals.RecipeEngine.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace EasyMeals.RecipeEngine.Infrastructure.Discovery;

/// <summary>
///     Default implementation of the application-layer IDiscoveryServiceFactory.
///     Resolves discovery services from DI container based on strategy.
/// </summary>
public class DiscoveryServiceFactory(IServiceProvider serviceProvider) : IDiscoveryServiceFactory
{
    private readonly IServiceProvider _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

    /// <summary>
    ///     Creates a discovery service instance for the specified strategy
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