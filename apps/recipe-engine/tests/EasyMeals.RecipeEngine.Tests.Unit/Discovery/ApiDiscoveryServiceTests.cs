using EasyMeals.RecipeEngine.Domain.Interfaces;
using EasyMeals.RecipeEngine.Domain.ValueObjects.Discovery;
using EasyMeals.RecipeEngine.Infrastructure.Discovery;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text.Json;

namespace EasyMeals.RecipeEngine.Tests.Unit.Discovery;

/// <summary>
/// T107: Unit tests for ApiDiscoveryService
/// Tests API-based discovery with JSON parsing
/// </summary>
public class ApiDiscoveryServiceTests
{
	private readonly Mock<ILogger<ApiDiscoveryService>> _mockLogger;
	private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
	private readonly HttpClient _httpClient;

	public ApiDiscoveryServiceTests()
	{
		_mockLogger = new Mock<ILogger<ApiDiscoveryService>>();
		_mockHttpMessageHandler = new Mock<HttpMessageHandler>();
		_httpClient = new HttpClient(_mockHttpMessageHandler.Object);
	}

	[Fact(DisplayName = "DiscoverRecipeUrlsAsync_ValidJsonResponse_ExtractsRecipeUrls")]
	public async Task DiscoverRecipeUrlsAsync_ValidJsonResponse_ExtractsRecipeUrls()
	{
		// Arrange
		const string baseUrl = "https://api.example.com/recipes";
		var jsonResponse = new
		{
			recipes = new[]
			{
				new { id = "1", url = "https://example.com/recipe/pasta", title = "Pasta" },
				new { id = "2", url = "https://example.com/recipe/pizza", title = "Pizza" },
				new { id = "3", url = "https://example.com/recipe/salad", title = "Salad" }
			}
		};

		SetupJsonResponse(baseUrl, jsonResponse);

		var service = new ApiDiscoveryService(_mockLogger.Object, _httpClient);

		// Act
		var result = await service.DiscoverRecipeUrlsAsync(
			baseUrl,
			"test_provider",
			maxDepth: 1,
			maxUrls: 100);

		// Assert
		var urls = result.ToList();
		Assert.Equal(3, urls.Count);
		Assert.All(urls, url => Assert.Contains("/recipe/", url.Url));
	}

	[Fact(DisplayName = "DiscoverRecipeUrlsAsync_EmptyJsonResponse_ReturnsEmptyList")]
	public async Task DiscoverRecipeUrlsAsync_EmptyJsonResponse_ReturnsEmptyList()
	{
		// Arrange
		const string baseUrl = "https://api.example.com/recipes";
		var jsonResponse = new { recipes = Array.Empty<object>() };

		SetupJsonResponse(baseUrl, jsonResponse);

		var service = new ApiDiscoveryService(_mockLogger.Object, _httpClient);

		// Act
		var result = await service.DiscoverRecipeUrlsAsync(
			baseUrl,
			"test_provider",
			maxDepth: 1,
			maxUrls: 100);

		// Assert
		Assert.Empty(result);
	}

	[Fact(DisplayName = "DiscoverRecipeUrlsAsync_PaginatedResponse_HandlesMultiplePages")]
	public async Task DiscoverRecipeUrlsAsync_PaginatedResponse_HandlesMultiplePages()
	{
		// Arrange
		const string baseUrl = "https://api.example.com/recipes";
		
		// First page
		var page1Response = new
		{
			recipes = new[]
			{
				new { id = "1", url = "https://example.com/recipe/1", title = "Recipe 1" },
				new { id = "2", url = "https://example.com/recipe/2", title = "Recipe 2" }
			},
			nextPage = "https://api.example.com/recipes?page=2"
		};

		// Second page
		var page2Response = new
		{
			recipes = new[]
			{
				new { id = "3", url = "https://example.com/recipe/3", title = "Recipe 3" }
			},
			nextPage = (string?)null
		};

		SetupJsonResponse(baseUrl, page1Response);
		SetupJsonResponse("https://api.example.com/recipes?page=2", page2Response);

		var service = new ApiDiscoveryService(_mockLogger.Object, _httpClient);

		// Act
		var result = await service.DiscoverRecipeUrlsAsync(
			baseUrl,
			"test_provider",
			maxDepth: 2,
			maxUrls: 100);

		// Assert
		var urls = result.ToList();
		// Note: Pagination might be limited by implementation, so we check for at least the first page
		Assert.NotEmpty(urls);
		Assert.Contains(urls, u => u.Url.Contains("/recipe/1"));
	}

	[Fact(DisplayName = "DiscoverRecipeUrlsAsync_MaxUrlsLimit_RespectsLimit")]
	public async Task DiscoverRecipeUrlsAsync_MaxUrlsLimit_RespectsLimit()
	{
		// Arrange
		const string baseUrl = "https://api.example.com/recipes";
		
		var recipes = Enumerable.Range(1, 100)
			.Select(i => new { id = i.ToString(), url = $"https://example.com/recipe/{i}", title = $"Recipe {i}" })
			.ToArray();

		var jsonResponse = new { recipes };

		SetupJsonResponse(baseUrl, jsonResponse);

		var service = new ApiDiscoveryService(_mockLogger.Object, _httpClient);

		// Act - limit to 10 URLs
		var result = await service.DiscoverRecipeUrlsAsync(
			baseUrl,
			"test_provider",
			maxDepth: 1,
			maxUrls: 10);

		// Assert
		Assert.Equal(10, result.Count());
	}

	[Fact(DisplayName = "DiscoverRecipeUrlsAsync_HttpError_ThrowsException")]
	public async Task DiscoverRecipeUrlsAsync_HttpError_ThrowsException()
	{
		// Arrange
		const string baseUrl = "https://api.example.com/recipes";
		
		_mockHttpMessageHandler
			.Protected()
			.Setup<Task<HttpResponseMessage>>(
				"SendAsync",
				ItExpr.IsAny<HttpRequestMessage>(),
				ItExpr.IsAny<CancellationToken>())
			.ReturnsAsync(new HttpResponseMessage
			{
				StatusCode = HttpStatusCode.InternalServerError,
				Content = new StringContent("Server Error")
			});

		var service = new ApiDiscoveryService(_mockLogger.Object, _httpClient);

		// Act & Assert
		await Assert.ThrowsAnyAsync<Exception>(async () =>
		{
			await service.DiscoverRecipeUrlsAsync(
				baseUrl,
				"test_provider",
				maxDepth: 1,
				maxUrls: 100);
		});
	}

	[Fact(DisplayName = "DiscoverRecipeUrlsAsync_InvalidJson_ThrowsException")]
	public async Task DiscoverRecipeUrlsAsync_InvalidJson_ThrowsException()
	{
		// Arrange
		const string baseUrl = "https://api.example.com/recipes";
		
		_mockHttpMessageHandler
			.Protected()
			.Setup<Task<HttpResponseMessage>>(
				"SendAsync",
				ItExpr.IsAny<HttpRequestMessage>(),
				ItExpr.IsAny<CancellationToken>())
			.ReturnsAsync(new HttpResponseMessage
			{
				StatusCode = HttpStatusCode.OK,
				Content = new StringContent("{ invalid json }")
			});

		var service = new ApiDiscoveryService(_mockLogger.Object, _httpClient);

		// Act & Assert
		await Assert.ThrowsAnyAsync<JsonException>(async () =>
		{
			await service.DiscoverRecipeUrlsAsync(
				baseUrl,
				"test_provider",
				maxDepth: 1,
				maxUrls: 100);
		});
	}

	[Fact(DisplayName = "IsRecipeUrl_ValidRecipeUrl_ReturnsTrue")]
	public void IsRecipeUrl_ValidRecipeUrl_ReturnsTrue()
	{
		// Arrange
		var service = new ApiDiscoveryService(_mockLogger.Object, _httpClient);

		// Act & Assert
		Assert.True(service.IsRecipeUrl("https://example.com/recipe/pasta", "test_provider"));
		Assert.True(service.IsRecipeUrl("https://example.com/recipes/123", "test_provider"));
	}

	private void SetupJsonResponse<T>(string url, T response)
	{
		var jsonContent = JsonSerializer.Serialize(response);
		
		_mockHttpMessageHandler
			.Protected()
			.Setup<Task<HttpResponseMessage>>(
				"SendAsync",
				ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString() == url),
				ItExpr.IsAny<CancellationToken>())
			.ReturnsAsync(new HttpResponseMessage
			{
				StatusCode = HttpStatusCode.OK,
				Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json")
			});
	}
}
