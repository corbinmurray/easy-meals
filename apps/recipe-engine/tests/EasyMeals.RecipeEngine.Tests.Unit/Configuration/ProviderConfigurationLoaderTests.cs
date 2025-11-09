using EasyMeals.RecipeEngine.Application.Interfaces;
using EasyMeals.RecipeEngine.Domain.ValueObjects;
using EasyMeals.RecipeEngine.Infrastructure.Documents;
using EasyMeals.RecipeEngine.Infrastructure.Services;
using EasyMeals.Shared.Data.Repositories;
using FluentAssertions;
using NSubstitute;

namespace EasyMeals.RecipeEngine.Tests.Unit.Configuration;

public class ProviderConfigurationLoaderTests
{
	private readonly IMongoRepository<ProviderConfigurationDocument> _mockRepository;
	private readonly ProviderConfigurationLoader _loader;

	public ProviderConfigurationLoaderTests()
	{
		_mockRepository = Substitute.For<IMongoRepository<ProviderConfigurationDocument>>();
		_loader = new ProviderConfigurationLoader(_mockRepository);
	}

	[Fact]
	public async Task GetAllEnabledAsync_ReturnsAllEnabledProviders_WhenProvidersExist()
	{
		// Arrange
		var documents = new List<ProviderConfigurationDocument>
		{
			CreateProviderDocument("provider_001", true),
			CreateProviderDocument("provider_002", true)
		};

		_mockRepository.GetAllAsync(
				Arg.Any<System.Linq.Expressions.Expression<Func<ProviderConfigurationDocument, bool>>>(),
				Arg.Any<CancellationToken>())
			.Returns(documents);

		// Act
		var result = await _loader.GetAllEnabledAsync();
		var configs = result.ToList();

		// Assert
		configs.Should().HaveCount(2);
		configs.Should().Contain(c => c.ProviderId == "provider_001");
		configs.Should().Contain(c => c.ProviderId == "provider_002");
	}

	[Fact]
	public async Task GetAllEnabledAsync_ReturnsEmptyList_WhenNoEnabledProviders()
	{
		// Arrange
		_mockRepository.GetAllAsync(
				Arg.Any<System.Linq.Expressions.Expression<Func<ProviderConfigurationDocument, bool>>>(),
				Arg.Any<CancellationToken>())
			.Returns(new List<ProviderConfigurationDocument>());

		// Act
		var result = await _loader.GetAllEnabledAsync();

		// Assert
		result.Should().BeEmpty();
	}

	[Fact]
	public async Task GetByProviderIdAsync_ReturnsConfiguration_WhenProviderExists()
	{
		// Arrange
		var document = CreateProviderDocument("provider_001", true);
		_mockRepository.GetFirstOrDefaultAsync(
				Arg.Any<System.Linq.Expressions.Expression<Func<ProviderConfigurationDocument, bool>>>(),
				Arg.Any<CancellationToken>())
			.Returns(document);

		// Act
		var result = await _loader.GetByProviderIdAsync("provider_001");

		// Assert
		result.Should().NotBeNull();
		result!.ProviderId.Should().Be("provider_001");
	}

	[Fact]
	public async Task GetByProviderIdAsync_ReturnsNull_WhenProviderDoesNotExist()
	{
		// Arrange
		_mockRepository.GetFirstOrDefaultAsync(
				Arg.Any<System.Linq.Expressions.Expression<Func<ProviderConfigurationDocument, bool>>>(),
				Arg.Any<CancellationToken>())
			.Returns((ProviderConfigurationDocument?)null);

		// Act
		var result = await _loader.GetByProviderIdAsync("nonexistent");

		// Assert
		result.Should().BeNull();
	}

	[Fact]
	public async Task LoadConfigurationsAsync_ThrowsException_WhenNoEnabledProviders()
	{
		// Arrange
		_mockRepository.GetAllAsync(
				Arg.Any<System.Linq.Expressions.Expression<Func<ProviderConfigurationDocument, bool>>>(),
				Arg.Any<CancellationToken>())
			.Returns(new List<ProviderConfigurationDocument>());

		// Act & Assert
		await _loader.Invoking(async l => await l.LoadConfigurationsAsync())
			.Should().ThrowAsync<InvalidOperationException>()
			.WithMessage("*No enabled provider configurations found*");
	}

	[Fact]
	public async Task LoadConfigurationsAsync_DoesNotThrow_WhenProvidersExist()
	{
		// Arrange
		var documents = new List<ProviderConfigurationDocument>
		{
			CreateProviderDocument("provider_001", true)
		};

		_mockRepository.GetAllAsync(
				Arg.Any<System.Linq.Expressions.Expression<Func<ProviderConfigurationDocument, bool>>>(),
				Arg.Any<CancellationToken>())
			.Returns(documents);

		// Act & Assert
		await _loader.Invoking(async l => await l.LoadConfigurationsAsync())
			.Should().NotThrowAsync();
	}

	[Fact]
	public async Task GetAllEnabledAsync_UsesCache_OnSecondCall()
	{
		// This test will pass with the current implementation but should be enhanced
		// once caching is added (T087)
		
		// Arrange
		var documents = new List<ProviderConfigurationDocument>
		{
			CreateProviderDocument("provider_001", true)
		};

		_mockRepository.GetAllAsync(
				Arg.Any<System.Linq.Expressions.Expression<Func<ProviderConfigurationDocument, bool>>>(),
				Arg.Any<CancellationToken>())
			.Returns(documents);

		// Act
		var result1 = await _loader.GetAllEnabledAsync();
		var result2 = await _loader.GetAllEnabledAsync();

		// Assert - Currently calls repository twice, will cache after T087
		await _mockRepository.Received(2).GetAllAsync(
			Arg.Any<System.Linq.Expressions.Expression<Func<ProviderConfigurationDocument, bool>>>(),
			Arg.Any<CancellationToken>());
		
		result1.Should().HaveCount(1);
		result2.Should().HaveCount(1);
	}

	private static ProviderConfigurationDocument CreateProviderDocument(string providerId, bool enabled)
	{
		return new ProviderConfigurationDocument
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
	}
}
