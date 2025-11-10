using EasyMeals.RecipeEngine.Domain.Interfaces;
using EasyMeals.RecipeEngine.Domain.ValueObjects.Discovery;
using EasyMeals.RecipeEngine.Infrastructure.Discovery;
using Microsoft.Extensions.Logging;
using Moq;

namespace EasyMeals.RecipeEngine.Tests.Unit.Discovery;

/// <summary>
/// T106: Unit tests for DynamicCrawlDiscoveryService
/// Tests Playwright integration with simplified mocks
/// Note: Due to Playwright's use of optional parameters, we use simpler unit tests
/// Integration tests with real Playwright are in T108
/// </summary>
public class DynamicCrawlDiscoveryServiceTests
{
	private readonly Mock<ILogger<DynamicCrawlDiscoveryService>> _mockLogger;

	public DynamicCrawlDiscoveryServiceTests()
	{
		_mockLogger = new Mock<ILogger<DynamicCrawlDiscoveryService>>();
	}

	[Fact(DisplayName = "IsRecipeUrl_ValidRecipeUrl_ReturnsTrue")]
	public void IsRecipeUrl_ValidRecipeUrl_ReturnsTrue()
	{
		// Arrange - Create service with null Playwright (we're only testing IsRecipeUrl)
		var service = new DynamicCrawlDiscoveryService(_mockLogger.Object, null!);

		// Act & Assert
		Assert.True(service.IsRecipeUrl("https://example.com/recipe/pasta", "test_provider"));
		Assert.True(service.IsRecipeUrl("https://example.com/recipes/123", "test_provider"));
	}

	[Fact(DisplayName = "IsRecipeUrl_NonRecipeUrl_ReturnsFalse")]
	public void IsRecipeUrl_NonRecipeUrl_ReturnsFalse()
	{
		// Arrange
		var service = new DynamicCrawlDiscoveryService(_mockLogger.Object, null!);

		// Act & Assert
		Assert.False(service.IsRecipeUrl("https://example.com/about", "test_provider"));
		Assert.False(service.IsRecipeUrl("https://example.com/contact", "test_provider"));
		Assert.False(service.IsRecipeUrl("https://example.com/privacy", "test_provider"));
	}

	[Fact(DisplayName = "IsRecipeUrl_EmptyUrl_ReturnsFalse")]
	public void IsRecipeUrl_EmptyUrl_ReturnsFalse()
	{
		// Arrange
		var service = new DynamicCrawlDiscoveryService(_mockLogger.Object, null!);

		// Act & Assert
		Assert.False(service.IsRecipeUrl("", "test_provider"));
		Assert.False(service.IsRecipeUrl(null!, "test_provider"));
	}

	// Note: Full integration tests with Playwright browser automation
	// are in the Integration test project (T108)
	// These unit tests focus on the URL validation logic
}

