using EasyMeals.RecipeEngine.Domain.Interfaces;
using EasyMeals.RecipeEngine.Domain.ValueObjects.Discovery;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace EasyMeals.RecipeEngine.Infrastructure.Discovery;

/// <summary>
/// T111: API-based discovery service for providers with REST APIs
/// Implements discovery strategy for sites that expose recipe data via API
/// </summary>
public class ApiDiscoveryService : IDiscoveryService
{
	private readonly ILogger<ApiDiscoveryService> _logger;
	private readonly HttpClient _httpClient;

	public ApiDiscoveryService(
		ILogger<ApiDiscoveryService> logger,
		HttpClient httpClient)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
	}

	/// <summary>
	/// Discovers recipe URLs from a provider's API endpoint
	/// </summary>
	public async Task<IEnumerable<DiscoveredUrl>> DiscoverRecipeUrlsAsync(
		string baseUrl,
		string provider,
		int maxDepth = 3,
		int maxUrls = 1000,
		CancellationToken cancellationToken = default)
	{
		_logger.LogInformation(
			"Starting API discovery for provider {Provider} from {BaseUrl} (maxUrls: {MaxUrls})",
			provider, baseUrl, maxUrls);

		var discoveredUrls = new List<DiscoveredUrl>();

		try
		{
			// Fetch JSON response from API
			var response = await _httpClient.GetAsync(baseUrl, cancellationToken);
			response.EnsureSuccessStatusCode();

			var json = await response.Content.ReadAsStringAsync(cancellationToken);

			// Parse JSON response
			var jsonDocument = JsonDocument.Parse(json);
			var root = jsonDocument.RootElement;

			// Try to find recipes array in common locations
			JsonElement recipesElement;
			if (root.TryGetProperty("recipes", out recipesElement))
			{
				// Standard format: { "recipes": [...] }
				discoveredUrls.AddRange(ParseRecipesArray(recipesElement, provider, baseUrl, maxUrls));
			}
			else if (root.TryGetProperty("data", out var dataElement) &&
			         dataElement.TryGetProperty("recipes", out recipesElement))
			{
				// Nested format: { "data": { "recipes": [...] } }
				discoveredUrls.AddRange(ParseRecipesArray(recipesElement, provider, baseUrl, maxUrls));
			}
			else if (root.TryGetProperty("items", out var itemsElement))
			{
				// Alternative format: { "items": [...] }
				discoveredUrls.AddRange(ParseRecipesArray(itemsElement, provider, baseUrl, maxUrls));
			}
			else if (root.ValueKind == JsonValueKind.Array)
			{
				// Array at root level: [...]
				discoveredUrls.AddRange(ParseRecipesArray(root, provider, baseUrl, maxUrls));
			}
			else
			{
				_logger.LogWarning(
					"No recipes found in API response from {BaseUrl}. Unexpected JSON structure.",
					baseUrl);
			}

			_logger.LogInformation(
				"API discovery completed for provider {Provider}. Discovered {Count} URLs",
				provider, discoveredUrls.Count);

			return discoveredUrls.Take(maxUrls);
		}
		catch (HttpRequestException ex)
		{
			_logger.LogError(ex,
				"HTTP error during API discovery for provider {Provider} at {BaseUrl}",
				provider, baseUrl);
			throw new DiscoveryException(
				$"API discovery failed for provider {provider}",
				provider,
				baseUrl,
				ex);
		}
		catch (JsonException ex)
		{
			_logger.LogError(ex,
				"JSON parsing error during API discovery for provider {Provider} at {BaseUrl}",
				provider, baseUrl);
			throw new DiscoveryException(
				$"API discovery failed for provider {provider} - invalid JSON response",
				provider,
				baseUrl,
				ex);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex,
				"API discovery failed for provider {Provider} at {BaseUrl}",
				provider, baseUrl);
			throw new DiscoveryException(
				$"API discovery failed for provider {provider}",
				provider,
				baseUrl,
				ex);
		}
	}

	/// <summary>
	/// Parses recipes array from JSON and extracts URLs
	/// </summary>
	private List<DiscoveredUrl> ParseRecipesArray(
		JsonElement recipesElement,
		string provider,
		string baseUrl,
		int maxUrls)
	{
		var discoveredUrls = new List<DiscoveredUrl>();

		if (recipesElement.ValueKind != JsonValueKind.Array)
		{
			_logger.LogWarning("Expected array but got {ValueKind}", recipesElement.ValueKind);
			return discoveredUrls;
		}

		foreach (var recipeElement in recipesElement.EnumerateArray())
		{
			if (discoveredUrls.Count >= maxUrls)
			{
				break;
			}

			// Try to extract URL from common property names
			string? recipeUrl = null;

			if (recipeElement.TryGetProperty("url", out var urlElement))
			{
				recipeUrl = urlElement.GetString();
			}
			else if (recipeElement.TryGetProperty("link", out var linkElement))
			{
				recipeUrl = linkElement.GetString();
			}
			else if (recipeElement.TryGetProperty("href", out var hrefElement))
			{
				recipeUrl = hrefElement.GetString();
			}
			else if (recipeElement.TryGetProperty("permalink", out var permalinkElement))
			{
				recipeUrl = permalinkElement.GetString();
			}
			else if (recipeElement.TryGetProperty("slug", out var slugElement))
			{
				// Build URL from slug
				var slug = slugElement.GetString();
				if (!string.IsNullOrWhiteSpace(slug))
				{
					var baseUri = new Uri(baseUrl);
					recipeUrl = $"{baseUri.Scheme}://{baseUri.Host}/recipe/{slug}";
				}
			}

			if (string.IsNullOrWhiteSpace(recipeUrl))
			{
				_logger.LogDebug("No URL found in recipe object: {Recipe}", recipeElement.GetRawText());
				continue;
			}

			// Ensure absolute URL
			if (!Uri.TryCreate(recipeUrl, UriKind.Absolute, out var absoluteUri))
			{
				// Try to make it absolute relative to base URL
				if (!Uri.TryCreate(new Uri(baseUrl), recipeUrl, out absoluteUri))
				{
					_logger.LogDebug("Invalid URL: {Url}", recipeUrl);
					continue;
				}
			}

			var absoluteUrl = absoluteUri.ToString();

			// Check if this is a recipe URL
			if (IsRecipeUrl(absoluteUrl, provider))
			{
				var confidence = CalculateConfidence(absoluteUrl);

				var discoveredUrl = DiscoveredUrl.CreateDiscovered(
					absoluteUrl,
					provider,
					baseUrl,
					0, // API discovery doesn't have depth
					confidence,
					ExtractMetadata(recipeElement));

				discoveredUrls.Add(discoveredUrl);
				_logger.LogDebug(
					"Discovered recipe URL from API: {Url} (confidence: {Confidence:P0})",
					absoluteUrl, confidence);
			}
		}

		return discoveredUrls;
	}

	/// <summary>
	/// Extracts metadata from recipe JSON element
	/// </summary>
	private Dictionary<string, object>? ExtractMetadata(JsonElement recipeElement)
	{
		var metadata = new Dictionary<string, object>();

		if (recipeElement.TryGetProperty("id", out var idElement))
		{
			metadata["apiId"] = idElement.ToString();
		}

		if (recipeElement.TryGetProperty("title", out var titleElement))
		{
			var title = titleElement.GetString();
			if (!string.IsNullOrWhiteSpace(title))
			{
				metadata["title"] = title;
			}
		}

		if (recipeElement.TryGetProperty("name", out var nameElement))
		{
			var name = nameElement.GetString();
			if (!string.IsNullOrWhiteSpace(name))
			{
				metadata["name"] = name;
			}
		}

		return metadata.Count > 0 ? metadata : null;
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

		var lowerUrl = url.ToLowerInvariant();

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
		// For API discovery, we don't track detailed statistics
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

		// High confidence patterns (0.95 for API-sourced URLs)
		if (lowerUrl.Contains("/recipe/") || lowerUrl.Contains("/recipes/"))
		{
			return 0.95m;
		}

		// Medium-high confidence for other patterns (0.8)
		return 0.8m;
	}
}
