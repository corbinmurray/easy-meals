using EasyMeals.RecipeEngine.Domain.ValueObjects.Discovery;

namespace EasyMeals.RecipeEngine.Domain.Interfaces;

/// <summary>
///     Domain service for discovering recipe URLs from provider sites
///     Follows DDD principles by encapsulating complex discovery business logic
/// </summary>
public interface IDiscoveryService
{
    /// <summary>
    ///     Discovers recipe URLs from a provider's base URL using their specific discovery strategy
    /// </summary>
    /// <param name="baseUrl">Base URL of the provider (e.g., "https://allrecipes.com")</param>
    /// <param name="provider">Provider name for strategy selection</param>
    /// <param name="maxDepth">Maximum recursion depth for discovery</param>
    /// <param name="maxUrls">Maximum number of URLs to discover</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of discovered recipe URLs with metadata</returns>
    Task<IEnumerable<DiscoveredUrl>> DiscoverRecipeUrlsAsync(
		string baseUrl,
		string provider,
		int maxDepth = 3,
		int maxUrls = 1000,
		CancellationToken cancellationToken = default);

    /// <summary>
    ///     Discovers recipe URLs from multiple pages/categories
    /// </summary>
    /// <param name="seedUrls">Starting URLs for discovery</param>
    /// <param name="provider">Provider name for strategy selection</param>
    /// <param name="discoveryOptions">Discovery configuration options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of discovered recipe URLs</returns>
    Task<IEnumerable<DiscoveredUrl>> DiscoverFromSeedUrlsAsync(
		IEnumerable<string> seedUrls,
		string provider,
		DiscoveryOptions discoveryOptions,
		CancellationToken cancellationToken = default);

    /// <summary>
    ///     Checks if a URL is likely to be a recipe page based on provider patterns
    /// </summary>
    /// <param name="url">URL to validate</param>
    /// <param name="provider">Provider name for validation rules</param>
    /// <returns>True if URL matches recipe patterns</returns>
    bool IsRecipeUrl(string url, string provider);

    /// <summary>
    ///     Gets discovery statistics for monitoring and optimization
    /// </summary>
    /// <param name="provider">Provider name</param>
    /// <param name="timeRange">Time range for statistics</param>
    /// <returns>Discovery performance metrics</returns>
    Task<DiscoveryStatistics> GetDiscoveryStatisticsAsync(
		string provider,
		TimeRange timeRange);
}