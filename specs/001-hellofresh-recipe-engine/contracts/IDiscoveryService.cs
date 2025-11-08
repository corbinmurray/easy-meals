using EasyMeals.RecipeEngine.Domain.ValueObjects;

namespace EasyMeals.RecipeEngine.Application.Interfaces;

/// <summary>
/// Service interface for discovering recipe URLs from a provider.
/// Implementations use different strategies (static crawl, dynamic crawl, API) based on provider configuration.
/// </summary>
public interface IDiscoveryService
{
    /// <summary>
    /// Discovers recipe URLs from a provider using the configured discovery strategy.
    /// </summary>
    /// <param name="providerConfiguration">Provider-specific configuration (strategy, root URL, etc.)</param>
    /// <param name="cancellationToken">Cancellation token for graceful shutdown</param>
    /// <returns>Collection of discovered recipe URLs (absolute HTTPS URLs)</returns>
    /// <exception cref="DiscoveryException">Thrown when discovery fails (network error, invalid configuration, etc.)</exception>
    Task<IEnumerable<string>> DiscoverRecipeUrlsAsync(
        ProviderConfiguration providerConfiguration,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Exception thrown when recipe discovery fails.
/// </summary>
public class DiscoveryException : Exception
{
    public string ProviderId { get; }
    public string RecipeRootUrl { get; }

    public DiscoveryException(string message, string providerId, string recipeRootUrl, Exception? innerException = null)
        : base(message, innerException)
    {
        ProviderId = providerId;
        RecipeRootUrl = recipeRootUrl;
    }
}
