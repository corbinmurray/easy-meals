using EasyMeals.RecipeEngine.Domain.Interfaces;
using EasyMeals.RecipeEngine.Domain.ValueObjects.Discovery;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace EasyMeals.RecipeEngine.Infrastructure.Discovery;

/// <summary>
/// T109: Static crawl discovery service using HtmlAgilityPack for static HTML parsing
/// Implements discovery strategy for sites that don't require JavaScript rendering
/// </summary>
public class StaticCrawlDiscoveryService : IDiscoveryService
{
	private readonly ILogger<StaticCrawlDiscoveryService> _logger;
	private readonly HttpClient _httpClient;

	public StaticCrawlDiscoveryService(
		ILogger<StaticCrawlDiscoveryService> logger,
		HttpClient httpClient)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
	}

	/// <summary>
	/// Discovers recipe URLs from a provider's base URL using static HTML parsing
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
	/// Recursive discovery of recipe URLs from HTML pages
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
		if (currentDepth > maxDepth || discoveredUrls.Count >= maxUrls)
		{
			return;
		}

		// Avoid revisiting URLs
		if (!visitedUrls.Add(currentUrl))
		{
			return;
		}

		try
		{
			// Fetch HTML content
			var response = await _httpClient.GetAsync(currentUrl, cancellationToken);
			
			// Ensure success status code - throws HttpRequestException on error
			response.EnsureSuccessStatusCode();

			var html = await response.Content.ReadAsStringAsync(cancellationToken);

			// Parse HTML with HtmlAgilityPack
			var htmlDoc = new HtmlDocument();
			htmlDoc.LoadHtml(html);

			// Extract all links
			var linkNodes = htmlDoc.DocumentNode.SelectNodes("//a[@href]");
			if (linkNodes == null)
			{
				_logger.LogDebug("No links found on page {Url}", currentUrl);
				return;
			}

			foreach (var linkNode in linkNodes)
			{
				if (discoveredUrls.Count >= maxUrls)
				{
					break;
				}

				var href = linkNode.GetAttributeValue("href", string.Empty);
				if (string.IsNullOrWhiteSpace(href))
				{
					continue;
				}

				// Convert relative URLs to absolute
				if (!Uri.TryCreate(new Uri(currentUrl), href, out var absoluteUri))
				{
					continue;
				}

				var absoluteUrl = absoluteUri.ToString();

				// Check if this is a recipe URL
				if (IsRecipeUrl(absoluteUrl, provider))
				{
					// Calculate confidence based on URL patterns
					var confidence = CalculateConfidence(absoluteUrl);

					var discoveredUrl = DiscoveredUrl.CreateDiscovered(
						absoluteUrl,
						provider,
						currentUrl,
						currentDepth,
						confidence);

					// Avoid duplicates
					if (!discoveredUrls.Any(u => u.Url == absoluteUrl))
					{
						discoveredUrls.Add(discoveredUrl);
						_logger.LogDebug(
							"Discovered recipe URL: {Url} (depth: {Depth}, confidence: {Confidence:P0})",
							absoluteUrl, currentDepth, confidence);
					}
				}
			}
		}
		catch (HttpRequestException ex)
		{
			_logger.LogWarning(ex,
				"Failed to fetch URL {Url} at depth {Depth}",
				currentUrl, currentDepth);
			
			// Re-throw for the base URL (depth 0) to fail fast
			if (currentDepth == 0)
			{
				throw;
			}
		}
		catch (TaskCanceledException ex)
		{
			_logger.LogWarning(ex,
				"Request timeout for URL {Url} at depth {Depth}",
				currentUrl, currentDepth);
			
			// Re-throw for the base URL (depth 0) to fail fast
			if (currentDepth == 0)
			{
				throw;
			}
		}
	}

	/// <summary>
	/// Discovers recipe URLs from multiple seed URLs
	/// </summary>
	public async Task<IEnumerable<DiscoveredUrl>> DiscoverFromSeedUrlsAsync(
		IEnumerable<string> seedUrls,
		string provider,
		DiscoveryOptions discoveryOptions,
		CancellationToken cancellationToken = default)
	{
		var allDiscoveredUrls = new List<DiscoveredUrl>();

		foreach (var seedUrl in seedUrls)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				break;
			}

			var urls = await DiscoverRecipeUrlsAsync(
				seedUrl,
				provider,
				discoveryOptions.MaxDepth,
				discoveryOptions.MaxUrls - allDiscoveredUrls.Count,
				cancellationToken);

			allDiscoveredUrls.AddRange(urls);

			if (allDiscoveredUrls.Count >= discoveryOptions.MaxUrls)
			{
				break;
			}
		}

		return allDiscoveredUrls;
	}

	/// <summary>
	/// Checks if a URL is likely to be a recipe page based on provider patterns
	/// </summary>
	public bool IsRecipeUrl(string url, string provider)
	{
		if (string.IsNullOrWhiteSpace(url))
		{
			return false;
		}

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

		// Exclude common non-recipe patterns
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
			"/search",
			"/category",
			"/tag",
			"/author"
		};

		var lowerUrl = url.ToLowerInvariant();

		// Check if URL matches exclude patterns
		if (excludePatterns.Any(pattern => lowerUrl.Contains(pattern)))
		{
			return false;
		}

		// Check if URL matches recipe patterns
		return recipePatterns.Any(pattern => lowerUrl.Contains(pattern));
	}

	/// <summary>
	/// Gets discovery statistics for monitoring and optimization
	/// </summary>
	public Task<DiscoveryStatistics> GetDiscoveryStatisticsAsync(
		string provider,
		TimeRange timeRange)
	{
		// For static discovery, we don't track detailed statistics
		// This would typically be implemented with a separate metrics service
		return Task.FromResult(new DiscoveryStatistics(
			TotalUrlsDiscovered: 0,
			RecipeUrlsFound: 0,
			FailedRequests: 0,
			AverageConfidence: 0m,
			AverageDiscoveryTime: TimeSpan.Zero,
			UrlsByDepth: new Dictionary<int, int>(),
			GeneratedAt: DateTime.UtcNow));
	}

	/// <summary>
	/// Calculates confidence score based on URL patterns
	/// </summary>
	private decimal CalculateConfidence(string url)
	{
		var lowerUrl = url.ToLowerInvariant();

		// High confidence patterns (0.9)
		if (lowerUrl.Contains("/recipe/") || lowerUrl.Contains("/recipes/"))
		{
			return 0.9m;
		}

		// Medium confidence patterns (0.7)
		if (lowerUrl.Contains("/food/") || lowerUrl.Contains("/cooking/"))
		{
			return 0.7m;
		}

		// Low confidence patterns (0.5)
		return 0.5m;
	}
}

/// <summary>
/// Exception thrown when discovery fails
/// </summary>
public class DiscoveryException : Exception
{
	public string ProviderId { get; }
	public string BaseUrl { get; }

	public DiscoveryException(string message, string providerId, string baseUrl, Exception? innerException = null)
		: base(message, innerException)
	{
		ProviderId = providerId;
		BaseUrl = baseUrl;
	}
}
