using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using EasyMeals.RecipeEngine.Application.Interfaces;
using EasyMeals.RecipeEngine.Domain.Interfaces;
using EasyMeals.RecipeEngine.Domain.ValueObjects;
using EasyMeals.RecipeEngine.Domain.ValueObjects.Discovery;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace EasyMeals.RecipeEngine.Infrastructure.Discovery;

/// <summary>
///     T109: Static crawl discovery service using HtmlAgilityPack for static HTML parsing
///     Implements discovery strategy for sites that don't require JavaScript rendering
/// </summary>
public class StaticCrawlDiscoveryService(
	ILogger<StaticCrawlDiscoveryService> logger,
	HttpClient httpClient,
	IProviderConfigurationLoader configLoader)
	: IDiscoveryService
{
	private readonly ILogger<StaticCrawlDiscoveryService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
	private readonly IProviderConfigurationLoader _configLoader = configLoader ?? throw new ArgumentNullException(nameof(configLoader));

	// Cache compiled regex patterns for performance
	private readonly ConcurrentDictionary<string, Regex?> _recipePatternCache = new();
	private readonly ConcurrentDictionary<string, Regex?> _categoryPatternCache = new();

	/// <summary>
    ///     Discovers recipe URLs from a provider's base URL using static HTML parsing
    /// </summary>
    public async Task<IEnumerable<DiscoveredUrl>> DiscoverRecipeUrlsAsync(
		string baseUrl,
		string provider,
		int maxDepth = 3,
		int maxUrls = 1000,
		CancellationToken cancellationToken = default)
	{
		_logger.LogInformation(
			"Starting static crawl discovery for provider {Provider} from {BaseUrl} (maxDepth: {MaxDepth}, maxUrls: {MaxUrls})",
			provider, baseUrl, maxDepth, maxUrls);

		var discoveredUrls = new List<DiscoveredUrl>();
		var visitedUrls = new HashSet<string>();

		try
		{
			await DiscoverRecursiveAsync(
				baseUrl,
				provider,
				0,
				maxDepth,
				maxUrls,
				discoveredUrls,
				visitedUrls,
				cancellationToken);

			_logger.LogInformation(
				"Static crawl discovery completed for provider {Provider}. Discovered {Count} URLs",
				provider, discoveredUrls.Count);

			return discoveredUrls;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex,
				"Static crawl discovery failed for provider {Provider} at {BaseUrl}",
				provider, baseUrl);
			throw new DiscoveryException(
				$"Static crawl discovery failed for provider {provider}",
				provider,
				baseUrl,
				ex);
		}
	}

    /// <summary>
    ///     Recursive discovery of recipe URLs from HTML pages
    /// </summary>
    private async Task DiscoverRecursiveAsync(
		string currentUrl,
		string provider,
		int currentDepth,
		int maxDepth,
		int maxUrls,
		List<DiscoveredUrl> discoveredUrls,
		HashSet<string> visitedUrls,
		CancellationToken cancellationToken)
	{
		// Check limits
		if (currentDepth > maxDepth || discoveredUrls.Count >= maxUrls) return;

		// Avoid revisiting URLs
		if (!visitedUrls.Add(currentUrl)) return;

		try
		{
			// Fetch HTML content
			HttpResponseMessage response = await _httpClient.GetAsync(currentUrl, cancellationToken);

			// Ensure success status code - throws HttpRequestException on error
			response.EnsureSuccessStatusCode();

			string html = await response.Content.ReadAsStringAsync(cancellationToken);

			// Parse HTML with HtmlAgilityPack
			var htmlDoc = new HtmlDocument();
			htmlDoc.LoadHtml(html);

			// Extract all links
			HtmlNodeCollection? linkNodes = htmlDoc.DocumentNode.SelectNodes("//a[@href]");
			if (linkNodes is null || linkNodes.Count == 0)
			{
				_logger.LogDebug("No links found on page {Url}", currentUrl);
				return;
			}

			// Track category/listing pages to crawl recursively
			var categoryUrlsToCrawl = new List<string>();

			foreach (string href in linkNodes
				         .TakeWhile(_ => discoveredUrls.Count < maxUrls)
				         .Select(linkNode => linkNode.GetAttributeValue("href", string.Empty))
				         .Where(href => !string.IsNullOrWhiteSpace(href)))
			{
				// Convert relative URLs to absolute
				if (!Uri.TryCreate(new Uri(currentUrl), href, out Uri? absoluteUri)) continue;

				var absoluteUrl = absoluteUri.ToString();

				// Check if this is a recipe URL
				if (IsRecipeUrl(absoluteUrl, provider))
				{
					// Calculate confidence based on URL patterns
					decimal confidence = CalculateConfidence(absoluteUrl);

					var discoveredUrl = DiscoveredUrl.CreateDiscovered(
						absoluteUrl,
						provider,
						currentUrl,
						currentDepth,
						confidence);

					// Avoid duplicates
					if (discoveredUrls.Any(u => u.Url == absoluteUrl)) continue;

					discoveredUrls.Add(discoveredUrl);
					_logger.LogDebug(
						"Discovered recipe URL: {Url} (depth: {Depth}, confidence: {Confidence:P0})",
						absoluteUrl, currentDepth, confidence);
				}
				// Check if this is a category/listing page we should crawl
				else if (IsCategoryUrl(absoluteUrl, provider) && currentDepth < maxDepth)
				{
					_logger.LogDebug("Discovered category URL: {Url}", absoluteUrl);
					categoryUrlsToCrawl.Add(absoluteUrl);
				}
			}

			// Recursively crawl category pages to find more recipes
			foreach (string categoryUrl in categoryUrlsToCrawl.TakeWhile(_ => discoveredUrls.Count < maxUrls))
			{
				await DiscoverRecursiveAsync(
					categoryUrl,
					provider,
					currentDepth + 1,
					maxDepth,
					maxUrls,
					discoveredUrls,
					visitedUrls,
					cancellationToken);
			}
		}
		catch (HttpRequestException ex)
		{
			_logger.LogWarning(ex,
				"Failed to fetch URL {Url} at depth {Depth}",
				currentUrl, currentDepth);

			// Re-throw for the base URL (depth 0) to fail fast
			if (currentDepth == 0) throw;
		}
		catch (TaskCanceledException ex)
		{
			_logger.LogWarning(ex,
				"Request timeout for URL {Url} at depth {Depth}",
				currentUrl, currentDepth);

			// Re-throw for the base URL (depth 0) to fail fast
			if (currentDepth == 0) throw;
		}
	}

    /// <summary>
    ///     Discovers recipe URLs from multiple seed URLs
    /// </summary>
    public async Task<IEnumerable<DiscoveredUrl>> DiscoverFromSeedUrlsAsync(
		IEnumerable<string> seedUrls,
		string provider,
		DiscoveryOptions discoveryOptions,
		CancellationToken cancellationToken = default)
	{
		var allDiscoveredUrls = new List<DiscoveredUrl>();

		foreach (string seedUrl in seedUrls)
		{
			if (cancellationToken.IsCancellationRequested) break;

			IEnumerable<DiscoveredUrl> urls = await DiscoverRecipeUrlsAsync(
				seedUrl,
				provider,
				discoveryOptions.MaxDepth,
				discoveryOptions.MaxUrls - allDiscoveredUrls.Count,
				cancellationToken);

			allDiscoveredUrls.AddRange(urls);

			if (allDiscoveredUrls.Count >= discoveryOptions.MaxUrls) break;
		}

		return allDiscoveredUrls;
	}

    /// <summary>
    ///     Checks if a URL is likely to be a recipe page based on provider patterns
    /// </summary>
    public bool IsRecipeUrl(string url, string provider)
	{
		if (string.IsNullOrWhiteSpace(url)) return false;

		// Try to get provider-specific regex pattern
		Regex? recipeRegex = GetRecipePatternForProvider(provider);

		if (recipeRegex == null) return IsRecipeUrlDefaultPatterns(url);

		try
		{
			return recipeRegex.IsMatch(url);
		}
		catch (RegexMatchTimeoutException ex)
		{
			_logger.LogWarning(ex, "Regex timeout checking recipe URL pattern for provider {Provider}", provider);
			// Fall through to default patterns
		}

		// Fall back to default patterns if no provider-specific pattern or pattern failed
		return IsRecipeUrlDefaultPatterns(url);
	}

    /// <summary>
    ///     Default recipe URL pattern matching (fallback when no provider-specific pattern)
    /// </summary>
    private static bool IsRecipeUrlDefaultPatterns(string url)
	{
		// Common recipe URL patterns
		var recipePatterns = new[]
		{
			"/recipe/",
			"/recipes/",
			"/food/recipe",
			"/cooking/recipe",
			"/r/",
			"/dish/"
		};

		// Exclude common non-recipe patterns (pages we don't want as results)
		var excludePatterns = new[]
		{
			"/about",
			"/contact",
			"/privacy",
			"/terms",
			"/login",
			"/signup",
			"/cart",
			"/checkout",
			"/account",
			"/search"
		};

		string lowerUrl = url.ToLowerInvariant();

		// Check if URL matches exclude patterns
		return !excludePatterns.Any(pattern => lowerUrl.Contains(pattern)) &&
		       // Check if URL matches recipe patterns
		       recipePatterns.Any(pattern => lowerUrl.Contains(pattern));
	}

    /// <summary>
    ///     Checks if a URL is a category/listing page that should be crawled for recipes
    /// </summary>
    private bool IsCategoryUrl(string url, string provider)
	{
		if (string.IsNullOrWhiteSpace(url)) return false;

		// Try to get provider-specific regex pattern
		Regex? categoryRegex = GetCategoryPatternForProvider(provider);

		if (categoryRegex == null) return IsCategoryUrlDefaultPatterns(url);

		try
		{
			return categoryRegex.IsMatch(url);
		}
		catch (RegexMatchTimeoutException ex)
		{
			_logger.LogWarning(ex, "Regex timeout checking category URL pattern for provider {Provider}", provider);
			// Fall through to default patterns
		}

		// Fall back to default patterns if no provider-specific pattern or pattern failed
		return IsCategoryUrlDefaultPatterns(url);
	}

    /// <summary>
    ///     Default category URL pattern matching (fallback when no provider-specific pattern)
    /// </summary>
    private static bool IsCategoryUrlDefaultPatterns(string url)
	{
		// Category/listing page patterns (pages we should crawl but not return as recipes)
		var categoryPatterns = new[]
		{
			"/category",
			"/categories",
			"/tag",
			"/tags",
			"/collection",
			"/cuisine",
			"/meal-type",
			"/recipes" // Main listings page
		};

		// Exclude patterns that shouldn't be crawled
		var excludePatterns = new[]
		{
			"/about",
			"/contact",
			"/privacy",
			"/terms",
			"/login",
			"/signup",
			"/cart",
			"/checkout",
			"/account",
			"/search"
		};

		string lowerUrl = url.ToLowerInvariant();

		// Don't crawl excluded pages
		return !excludePatterns.Any(pattern => lowerUrl.Contains(pattern)) &&
		       // Check if URL matches category patterns
		       categoryPatterns.Any(pattern => lowerUrl.Contains(pattern));
	}

    /// <summary>
    ///     Gets discovery statistics for monitoring and optimization
    /// </summary>
    public Task<DiscoveryStatistics> GetDiscoveryStatisticsAsync(
		string provider,
		TimeRange timeRange) =>
		// For static discovery, we don't track detailed statistics
		// This would typically be implemented with a separate metrics service
		Task.FromResult(new DiscoveryStatistics(
			0,
			0,
			0,
			0m,
			TimeSpan.Zero,
			new Dictionary<int, int>(),
			DateTime.UtcNow));

    /// <summary>
    ///     Gets and caches the recipe URL pattern for a provider
    /// </summary>
    private Regex? GetRecipePatternForProvider(string provider) => _recipePatternCache.GetOrAdd(provider, LoadRecipePattern);

    /// <summary>
    ///     Gets and caches the category URL pattern for a provider
    /// </summary>
    private Regex? GetCategoryPatternForProvider(string provider) => _categoryPatternCache.GetOrAdd(provider, LoadCategoryPattern);

    /// <summary>
    ///     Loads and compiles recipe URL pattern from provider configuration
    /// </summary>
    private Regex? LoadRecipePattern(string provider)
	{
		try
		{
			ProviderConfiguration? config = _configLoader.GetByProviderIdAsync(provider).GetAwaiter().GetResult();

			if (config?.RecipeUrlPattern == null || string.IsNullOrWhiteSpace(config.RecipeUrlPattern))
				return null;

			return new Regex(
				config.RecipeUrlPattern,
				RegexOptions.Compiled | RegexOptions.IgnoreCase,
				TimeSpan.FromSeconds(1));
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Failed to load recipe URL pattern for provider {Provider}", provider);
			return null;
		}
	}

    /// <summary>
    ///     Loads and compiles category URL pattern from provider configuration
    /// </summary>
    private Regex? LoadCategoryPattern(string provider)
	{
		try
		{
			ProviderConfiguration? config = _configLoader.GetByProviderIdAsync(provider).GetAwaiter().GetResult();

			if (config?.CategoryUrlPattern == null || string.IsNullOrWhiteSpace(config.CategoryUrlPattern))
				return null;

			return new Regex(
				config.CategoryUrlPattern,
				RegexOptions.Compiled | RegexOptions.IgnoreCase,
				TimeSpan.FromSeconds(1));
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Failed to load category URL pattern for provider {Provider}", provider);
			return null;
		}
	}

    /// <summary>
    ///     Calculates confidence score based on URL patterns
    /// </summary>
    private static decimal CalculateConfidence(string url)
	{
		string lowerUrl = url.ToLowerInvariant();

		// High confidence patterns (0.9)
		if (lowerUrl.Contains("/recipe/") || lowerUrl.Contains("/recipes/")) return 0.9m;

		// Medium confidence patterns (0.7)
		if (lowerUrl.Contains("/food/") || lowerUrl.Contains("/cooking/")) return 0.7m;

		// Low confidence patterns (0.5)
		return 0.5m;
	}
}

/// <summary>
///     Exception thrown when discovery fails
/// </summary>
public class DiscoveryException(string message, string providerId, string baseUrl, Exception? innerException = null)
	: Exception(message, innerException)
{
	public string ProviderId { get; } = providerId;
	public string BaseUrl { get; } = baseUrl;
}