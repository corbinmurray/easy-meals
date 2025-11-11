using System.Linq.Expressions;
using EasyMeals.RecipeEngine.Domain.ValueObjects;
using EasyMeals.RecipeEngine.Infrastructure.Documents;
using EasyMeals.RecipeEngine.Infrastructure.Services;
using EasyMeals.Shared.Data.Repositories;
using FluentAssertions;
using Moq;

namespace EasyMeals.RecipeEngine.Tests.Unit.Configuration;

public class ProviderConfigurationLoaderTests
{
	private readonly ProviderConfigurationLoader _loader;
	private readonly Mock<IMongoRepository<ProviderConfigurationDocument>> _repositoryMock;

	public ProviderConfigurationLoaderTests()
	{
		_repositoryMock = new Mock<IMongoRepository<ProviderConfigurationDocument>>();
		_loader = new ProviderConfigurationLoader(_repositoryMock.Object);
	}

	private static ProviderConfigurationDocument CreateProviderDocument(string providerId, bool enabled) =>
		new()
		{
			ProviderId = providerId,
			Enabled = enabled,
			DiscoveryStrategy = "Dynamic",
			RecipeRootUrl = "https://example.com/recipes",
			BatchSize = 10,
			TimeWindowMinutes = 10,
			MinDelaySeconds = 2,
			MaxRequestsPerMinute = 10,
			RetryCount = 3,
			RequestTimeoutSeconds = 30,
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		};

	[Fact]
	public async Task GetAllEnabledAsync_ReturnsAllEnabledProviders_WhenProvidersExist()
	{
		// Arrange
		var documents = new List<ProviderConfigurationDocument>
		{
			CreateProviderDocument("provider_001", true),
			CreateProviderDocument("provider_002", true)
		};

		_repositoryMock
			.Setup(repository => repository.GetAllAsync(
				It.IsAny<Expression<Func<ProviderConfigurationDocument, bool>>>(),
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(documents);

		// Act
		IEnumerable<ProviderConfiguration> result = await _loader.GetAllEnabledAsync();
		List<ProviderConfiguration> configs = result.ToList();

		// Assert
		configs.Should().HaveCount(2);
		configs.Should().Contain(c => c.ProviderId == "provider_001");
		configs.Should().Contain(c => c.ProviderId == "provider_002");
	}

	[Fact]
	public async Task GetAllEnabledAsync_ReturnsEmptyList_WhenNoEnabledProviders()
	{
		// Arrange
		_repositoryMock
			.Setup(repository => repository.GetAllAsync(
				It.IsAny<Expression<Func<ProviderConfigurationDocument, bool>>>(),
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(new List<ProviderConfigurationDocument>());

		// Act
		IEnumerable<ProviderConfiguration> result = await _loader.GetAllEnabledAsync();

		// Assert
		result.Should().BeEmpty();
	}

	[Fact]
	public async Task GetAllEnabledAsync_UsesCache_OnSecondCallAfterLoadConfigurations()
	{
		// Arrange
		var documents = new List<ProviderConfigurationDocument>
		{
			CreateProviderDocument("provider_001", true)
		};

		_repositoryMock
			.Setup(repository => repository.GetAllAsync(
				It.IsAny<Expression<Func<ProviderConfigurationDocument, bool>>>(),
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(documents);

		// Act - First load configurations (populates cache)
		await _loader.LoadConfigurationsAsync();

		// Clear the mock to verify cache is used for subsequent calls
		_repositoryMock.Invocations.Clear();

		// Second call should use cache for individual provider lookup
		ProviderConfiguration? result = await _loader.GetByProviderIdAsync("provider_001");

		// Assert - Should not query repository (using cache instead)
		_repositoryMock.Verify(repository => repository.GetFirstOrDefaultAsync(
				It.IsAny<Expression<Func<ProviderConfigurationDocument, bool>>>(),
				It.IsAny<CancellationToken>()),
			Times.Never());

		result.Should().NotBeNull();
		result!.ProviderId.Should().Be("provider_001");
	}

	[Fact]
	public async Task GetByProviderIdAsync_ReturnsConfiguration_WhenProviderExists()
	{
		// Arrange
		ProviderConfigurationDocument document = CreateProviderDocument("provider_001", true);
		_repositoryMock
			.Setup(repository => repository.GetFirstOrDefaultAsync(
				It.IsAny<Expression<Func<ProviderConfigurationDocument, bool>>>(),
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(document);

		// Act
		ProviderConfiguration? result = await _loader.GetByProviderIdAsync("provider_001");

		// Assert
		result.Should().NotBeNull();
		result!.ProviderId.Should().Be("provider_001");
	}

	[Fact]
	public async Task GetByProviderIdAsync_ReturnsNull_WhenProviderDoesNotExist()
	{
		// Arrange
		_repositoryMock
			.Setup(repository => repository.GetFirstOrDefaultAsync(
				It.IsAny<Expression<Func<ProviderConfigurationDocument, bool>>>(),
				It.IsAny<CancellationToken>()))
			.ReturnsAsync((ProviderConfigurationDocument?)null);

		// Act
		ProviderConfiguration? result = await _loader.GetByProviderIdAsync("nonexistent");

		// Assert
		result.Should().BeNull();
	}

	[Fact]
	public async Task LoadConfigurationsAsync_DoesNotThrow_WhenProvidersExist()
	{
		// Arrange
		var documents = new List<ProviderConfigurationDocument>
		{
			CreateProviderDocument("provider_001", true)
		};

		_repositoryMock
			.Setup(repository => repository.GetAllAsync(
				It.IsAny<Expression<Func<ProviderConfigurationDocument, bool>>>(),
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(documents);

		// Act & Assert
		await _loader.Invoking(async l => await l.LoadConfigurationsAsync())
			.Should().NotThrowAsync();
	}

	[Fact]
	public async Task LoadConfigurationsAsync_ThrowsException_WhenNoEnabledProviders()
	{
		// Arrange
		_repositoryMock
			.Setup(repository => repository.GetAllAsync(
				It.IsAny<Expression<Func<ProviderConfigurationDocument, bool>>>(),
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(new List<ProviderConfigurationDocument>());

		// Act & Assert
		await _loader.Invoking(async l => await l.LoadConfigurationsAsync())
			.Should().ThrowAsync<InvalidOperationException>()
			.WithMessage("*No enabled provider configurations found*");
	}
}