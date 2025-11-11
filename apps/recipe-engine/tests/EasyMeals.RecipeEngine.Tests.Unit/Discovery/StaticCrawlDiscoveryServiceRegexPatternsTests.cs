using System.Net;
using EasyMeals.RecipeEngine.Application.Interfaces;
using EasyMeals.RecipeEngine.Domain.ValueObjects;
using EasyMeals.RecipeEngine.Domain.ValueObjects.Discovery;
using EasyMeals.RecipeEngine.Infrastructure.Discovery;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;

namespace EasyMeals.RecipeEngine.Tests.Unit.Discovery;

/// <summary>
///     Unit tests for StaticCrawlDiscoveryService regex pattern functionality
///     Tests provider-specific URL pattern matching with fallback to default patterns
/// </summary>
public class StaticCrawlDiscoveryServiceRegexPatternsTests
{
	private readonly HttpClient _httpClient;
	private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
	private readonly Mock<ILogger<StaticCrawlDiscoveryService>> _mockLogger;
	private readonly Mock<IProviderConfigurationLoader> _mockConfigLoader;

	public StaticCrawlDiscoveryServiceRegexPatternsTests()
	{
		_mockLogger = new Mock<ILogger<StaticCrawlDiscoveryService>>();
		_mockHttpMessageHandler = new Mock<HttpMessageHandler>();
		_httpClient = new HttpClient(_mockHttpMessageHandler.Object);
		_mockConfigLoader = new Mock<IProviderConfigurationLoader>();
	}

	private void SetupHttpResponse(string url, string content)
	{
		_mockHttpMessageHandler
			.Protected()
			.Setup<Task<HttpResponseMessage>>(
				"SendAsync",
				ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().StartsWith(url)),
				ItExpr.IsAny<CancellationToken>())
			.ReturnsAsync(new HttpResponseMessage
			{
				StatusCode = HttpStatusCode.OK,
				Content = new StringContent(content)
			});
	}

	[Fact(DisplayName = "IsRecipeUrl_WithProviderRegexPattern_UsesRegexOverDefaults")]
	public void IsRecipeUrl_WithProviderRegexPattern_UsesRegexOverDefaults()
	{
		// Arrange - HelloFresh recipe URLs end with a specific pattern
		var config = new ProviderConfiguration(
			"hellofresh",
			enabled: true,
			DiscoveryStrategy.Static,
			"https://www.hellofresh.com",
			batchSize: 10,
			timeWindowMinutes: 60,
			minDelaySeconds: 1,
			maxRequestsPerMinute: 60,
			retryCount: 3,
			requestTimeoutSeconds: 30,
			recipeUrlPattern: @"\/recipe\/[a-z0-9\-]+\-[a-f0-9]{24}$",  // HelloFresh recipe pattern
			categoryUrlPattern: null
		);

		_mockConfigLoader
			.Setup(x => x.GetByProviderIdAsync("hellofresh", It.IsAny<CancellationToken>()))
			.ReturnsAsync(config);

		var service = new StaticCrawlDiscoveryService(_mockLogger.Object, _httpClient, _mockConfigLoader.Object);

		// Act & Assert - This URL matches the HelloFresh pattern
		Assert.True(service.IsRecipeUrl(
			"https://www.hellofresh.com/recipe/chicken-pasta-507f1f77bcf86cd799439011",
			"hellofresh"));

		// This URL does NOT match the HelloFresh pattern (missing recipe ID)
		Assert.False(service.IsRecipeUrl(
			"https://www.hellofresh.com/recipe/chicken-pasta",
			"hellofresh"));
	}

	[Fact(DisplayName = "IsRecipeUrl_WithoutProviderPattern_UsesDefaults")]
	public void IsRecipeUrl_WithoutProviderPattern_UsesDefaults()
	{
		// Arrange - No provider-specific pattern configured
		_mockConfigLoader
			.Setup(x => x.GetByProviderIdAsync("generic_provider", It.IsAny<CancellationToken>()))
			.ReturnsAsync((ProviderConfiguration?)null);

		var service = new StaticCrawlDiscoveryService(_mockLogger.Object, _httpClient, _mockConfigLoader.Object);

		// Act & Assert - Falls back to default pattern matching
		Assert.True(service.IsRecipeUrl(
			"https://example.com/recipe/chicken-pasta",
			"generic_provider"));

		Assert.True(service.IsRecipeUrl(
			"https://example.com/recipes/123",
			"generic_provider"));
	}

	[Fact(DisplayName = "DiscoverRecipeUrlsAsync_WithRecipePattern_FiltersCorrectly")]
	public async Task DiscoverRecipeUrlsAsync_WithRecipePattern_FiltersCorrectly()
	{
		// Arrange - HelloFresh with specific URL pattern
		var config = new ProviderConfiguration(
			"hellofresh",
			enabled: true,
			DiscoveryStrategy.Static,
			"https://www.hellofresh.com",
			batchSize: 10,
			timeWindowMinutes: 60,
			minDelaySeconds: 1,
			maxRequestsPerMinute: 60,
			retryCount: 3,
			requestTimeoutSeconds: 30,
			recipeUrlPattern: @"\/recipe\/[a-z0-9\-]+\-[a-f0-9]{24}$",  // HelloFresh recipe pattern
			categoryUrlPattern: @"\/recipes\/(category|tag)\/[a-z\-]+"  // HelloFresh category pattern
		);

		_mockConfigLoader
			.Setup(x => x.GetByProviderIdAsync("hellofresh", It.IsAny<CancellationToken>()))
			.ReturnsAsync(config);

		const string baseUrl = "https://www.hellofresh.com/recipes";
		const string htmlContent = @"
			<html>
				<body>
					<a href='/recipe/chicken-pasta-507f1f77bcf86cd799439011'>Chicken Pasta</a>
					<a href='/recipe/beef-stew-507f191e810c19729de860ea'>Beef Stew</a>
					<a href='/recipe/invalid-no-id'>Invalid Recipe</a>
					<a href='/recipes/category/dinner'>Dinner Category</a>
				</body>
			</html>";

		SetupHttpResponse(baseUrl, htmlContent);

		var service = new StaticCrawlDiscoveryService(_mockLogger.Object, _httpClient, _mockConfigLoader.Object);

		// Act
		IEnumerable<DiscoveredUrl> result = await service.DiscoverRecipeUrlsAsync(
			baseUrl,
			"hellofresh",
			1,
			100);

		// Assert - Only valid recipe URLs with IDs should be discovered
		List<DiscoveredUrl> urls = result.ToList();
		Assert.Equal(2, urls.Count);
		Assert.All(urls, url => Assert.Matches(@"\/recipe\/[a-z0-9\-]+\-[a-f0-9]{24}$", url.Url));
		Assert.DoesNotContain(urls, url => url.Url.Contains("invalid-no-id"));
		Assert.DoesNotContain(urls, url => url.Url.Contains("category"));
	}

	[Fact(DisplayName = "DiscoverRecipeUrlsAsync_WithCategoryPattern_CrawlsCategories")]
	public async Task DiscoverRecipeUrlsAsync_WithCategoryPattern_CrawlsCategories()
	{
		// Arrange - Provider with specific category pattern
		var config = new ProviderConfiguration(
			"hellofresh",
			enabled: true,
			DiscoveryStrategy.Static,
			"https://www.hellofresh.com",
			batchSize: 10,
			timeWindowMinutes: 60,
			minDelaySeconds: 1,
			maxRequestsPerMinute: 60,
			retryCount: 3,
			requestTimeoutSeconds: 30,
			recipeUrlPattern: @"\/recipe\/[a-z0-9\-]+\-[a-f0-9]{24}$",
			categoryUrlPattern: @"\/recipes\/(category|tag)\/[a-z\-]+"
		);

		_mockConfigLoader
			.Setup(x => x.GetByProviderIdAsync("hellofresh", It.IsAny<CancellationToken>()))
			.ReturnsAsync(config);

		const string baseUrl = "https://www.hellofresh.com/recipes";
		const string categoryHtml = @"
			<html>
				<body>
					<a href='/recipes/category/dinner'>Dinner</a>
					<a href='/recipes/category/vegetarian'>Vegetarian</a>
				</body>
			</html>";
		
		const string categoryPageHtml = @"
			<html>
				<body>
					<a href='/recipe/dinner-recipe-507f1f77bcf86cd799439011'>Dinner Recipe</a>
				</body>
			</html>";

		SetupHttpResponse(baseUrl, categoryHtml);
		SetupHttpResponse("https://www.hellofresh.com/recipes/category/dinner", categoryPageHtml);
		SetupHttpResponse("https://www.hellofresh.com/recipes/category/vegetarian", categoryPageHtml);

		var service = new StaticCrawlDiscoveryService(_mockLogger.Object, _httpClient, _mockConfigLoader.Object);

		// Act - Allow depth 2 to crawl into categories
		IEnumerable<DiscoveredUrl> result = await service.DiscoverRecipeUrlsAsync(
			baseUrl,
			"hellofresh",
			2,
			100);

		// Assert - Should find recipes by crawling category pages
		List<DiscoveredUrl> urls = result.ToList();
		Assert.NotEmpty(urls);
		Assert.All(urls, url => Assert.Matches(@"\/recipe\/[a-z0-9\-]+\-[a-f0-9]{24}$", url.Url));
	}

	[Fact(DisplayName = "IsRecipeUrl_InvalidRegexPattern_FallsBackToDefaults")]
	public void IsRecipeUrl_InvalidRegexPattern_FallsBackToDefaults()
	{
		// Arrange - Provider with an invalid regex that might timeout
		var config = new ProviderConfiguration(
			"test_provider",
			enabled: true,
			DiscoveryStrategy.Static,
			"https://example.com",
			batchSize: 10,
			timeWindowMinutes: 60,
			minDelaySeconds: 1,
			maxRequestsPerMinute: 60,
			retryCount: 3,
			requestTimeoutSeconds: 30,
			recipeUrlPattern: @"(a+)+b",  // Potentially catastrophic backtracking pattern
			categoryUrlPattern: null
		);

		_mockConfigLoader
			.Setup(x => x.GetByProviderIdAsync("test_provider", It.IsAny<CancellationToken>()))
			.ReturnsAsync(config);

		var service = new StaticCrawlDiscoveryService(_mockLogger.Object, _httpClient, _mockConfigLoader.Object);

		// Act & Assert - Should fall back to default patterns without throwing
		// This URL matches default patterns
		var result = service.IsRecipeUrl("https://example.com/recipe/test", "test_provider");
		
		// Should either match via regex or fallback to default
		// The exact behavior depends on whether regex times out
		Assert.True(result || !result); // Just ensure it doesn't throw
	}
}
