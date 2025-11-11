using System.Net;
using EasyMeals.RecipeEngine.Application.Interfaces;
using EasyMeals.RecipeEngine.Domain.ValueObjects.Discovery;
using EasyMeals.RecipeEngine.Infrastructure.Discovery;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;

namespace EasyMeals.RecipeEngine.Tests.Unit.Discovery;

/// <summary>
///     T105: Unit tests for StaticCrawlDiscoveryService
///     Tests HtmlAgilityPack parsing and CSS selector extraction
/// </summary>
public class StaticCrawlDiscoveryServiceTests
{
    private readonly HttpClient _httpClient;
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly Mock<ILogger<StaticCrawlDiscoveryService>> _mockLogger;
    private readonly Mock<IProviderConfigurationLoader> _mockConfigLoader;

    public StaticCrawlDiscoveryServiceTests()
    {
        _mockLogger = new Mock<ILogger<StaticCrawlDiscoveryService>>();
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
        _mockConfigLoader = new Mock<IProviderConfigurationLoader>();

        // Setup default behavior - return null config (no custom patterns)
        _mockConfigLoader
            .Setup(x => x.GetByProviderIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Domain.ValueObjects.ProviderConfiguration?)null);
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

    [Fact(DisplayName = "DiscoverRecipeUrlsAsync_EmptyHtml_ReturnsEmptyList")]
    public async Task DiscoverRecipeUrlsAsync_EmptyHtml_ReturnsEmptyList()
    {
        // Arrange
        const string baseUrl = "https://example.com/recipes";
        const string htmlContent = "<html><body></body></html>";

        SetupHttpResponse(baseUrl, htmlContent);

        var service = new StaticCrawlDiscoveryService(_mockLogger.Object, _httpClient, _mockConfigLoader.Object);

        // Act
        IEnumerable<DiscoveredUrl> result = await service.DiscoverRecipeUrlsAsync(
            baseUrl,
            "test_provider",
            1,
            100);

        // Assert
        Assert.Empty(result);
    }

    [Fact(DisplayName = "DiscoverRecipeUrlsAsync_HttpError_ThrowsException")]
    public async Task DiscoverRecipeUrlsAsync_HttpError_ThrowsException()
    {
        // Arrange
        const string baseUrl = "https://example.com";

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound,
                Content = new StringContent("Not Found")
            });

        var service = new StaticCrawlDiscoveryService(_mockLogger.Object, _httpClient, _mockConfigLoader.Object);

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await service.DiscoverRecipeUrlsAsync(
                baseUrl,
                "test_provider",
                1,
                100);
        });
    }

    [Fact(DisplayName = "DiscoverRecipeUrlsAsync_MaxUrlsLimit_RespectsLimit")]
    public async Task DiscoverRecipeUrlsAsync_MaxUrlsLimit_RespectsLimit()
    {
        // Arrange
        const string baseUrl = "https://example.com";
        var htmlContent = @"
			<html>
				<body>";

        // Generate 100 recipe links
        for (var i = 1; i <= 100; i++)
        {
            htmlContent += $"<a href='/recipe/{i}'>Recipe {i}</a>\n";
        }

        htmlContent += @"
				</body>
			</html>";

        SetupHttpResponse(baseUrl, htmlContent);

        var service = new StaticCrawlDiscoveryService(_mockLogger.Object, _httpClient, _mockConfigLoader.Object);

        // Act - limit to 10 URLs
        IEnumerable<DiscoveredUrl> result = await service.DiscoverRecipeUrlsAsync(
            baseUrl,
            "test_provider",
            1,
            10);

        // Assert
        Assert.Equal(10, result.Count());
    }

    [Fact(DisplayName = "DiscoverRecipeUrlsAsync_RelativeUrls_ConvertsToAbsolute")]
    public async Task DiscoverRecipeUrlsAsync_RelativeUrls_ConvertsToAbsolute()
    {
        // Arrange
        const string baseUrl = "https://example.com";
        const string htmlContent = @"
			<html>
				<body>
					<a href='/recipe/pasta'>Pasta</a>
					<a href='recipe/pizza'>Pizza</a>
				</body>
			</html>";

        SetupHttpResponse(baseUrl, htmlContent);

        var service = new StaticCrawlDiscoveryService(_mockLogger.Object, _httpClient, _mockConfigLoader.Object);

        // Act
        IEnumerable<DiscoveredUrl> result = await service.DiscoverRecipeUrlsAsync(
            baseUrl,
            "test_provider",
            1,
            100);

        // Assert
        List<DiscoveredUrl> urls = result.ToList();
        Assert.All(urls, url => Assert.StartsWith("https://", url.Url));
        Assert.All(urls, url => Assert.Contains("example.com", url.Url));
    }

    [Fact(DisplayName = "DiscoverRecipeUrlsAsync_ValidHtml_ExtractsRecipeLinks")]
    public async Task DiscoverRecipeUrlsAsync_ValidHtml_ExtractsRecipeLinks()
    {
        // Arrange
        const string baseUrl = "https://example.com/recipes";
        const string htmlContent = @"
			<html>
				<body>
					<div class='recipe-list'>
						<a href='/recipe/chicken-pasta'>Chicken Pasta</a>
						<a href='/recipe/beef-stew'>Beef Stew</a>
						<a href='/recipe/vegetable-soup'>Vegetable Soup</a>
						<a href='/about'>About Us</a>
					</div>
				</body>
			</html>";

        SetupHttpResponse(baseUrl, htmlContent);

        var service = new StaticCrawlDiscoveryService(_mockLogger.Object, _httpClient, _mockConfigLoader.Object);

        // Act
        IEnumerable<DiscoveredUrl> result = await service.DiscoverRecipeUrlsAsync(
            baseUrl,
            "test_provider",
            1,
            100);

        // Assert
        List<DiscoveredUrl> urls = result.ToList();
        Assert.NotEmpty(urls);
        Assert.All(urls, url => Assert.Contains("/recipe/", url.Url));
        Assert.DoesNotContain(urls, url => url.Url.Contains("/about"));
    }

    [Fact(DisplayName = "IsRecipeUrl_NonRecipeUrl_ReturnsFalse")]
    public void IsRecipeUrl_NonRecipeUrl_ReturnsFalse()
    {
        // Arrange
        var service = new StaticCrawlDiscoveryService(_mockLogger.Object, _httpClient, _mockConfigLoader.Object);

        // Act & Assert
        Assert.False(service.IsRecipeUrl("https://example.com/about", "test_provider"));
        Assert.False(service.IsRecipeUrl("https://example.com/contact", "test_provider"));
        Assert.False(service.IsRecipeUrl("https://example.com/privacy", "test_provider"));
    }

    [Fact(DisplayName = "IsRecipeUrl_ValidRecipeUrl_ReturnsTrue")]
    public void IsRecipeUrl_ValidRecipeUrl_ReturnsTrue()
    {
        // Arrange
        var service = new StaticCrawlDiscoveryService(_mockLogger.Object, _httpClient, _mockConfigLoader.Object);

        // Act & Assert
        Assert.True(service.IsRecipeUrl("https://example.com/recipe/pasta", "test_provider"));
        Assert.True(service.IsRecipeUrl("https://example.com/recipes/123", "test_provider"));
        Assert.True(service.IsRecipeUrl("https://example.com/food/recipe-pasta", "test_provider"));
    }
}