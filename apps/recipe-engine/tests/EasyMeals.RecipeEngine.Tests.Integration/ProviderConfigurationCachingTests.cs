using EasyMeals.RecipeEngine.Domain.ValueObjects;
using EasyMeals.RecipeEngine.Infrastructure.Documents;
using EasyMeals.RecipeEngine.Infrastructure.Services;
using EasyMeals.Shared.Data.Repositories;
using FluentAssertions;
using MongoDB.Driver;
using Testcontainers.MongoDb;

namespace EasyMeals.RecipeEngine.Tests.Integration;

/// <summary>
///     Integration tests for provider configuration caching.
///     Verifies load from MongoDB, cache in memory, and refresh on TTL.
/// </summary>
public class ProviderConfigurationCachingTests : IAsyncLifetime
{
	private IMongoDatabase? _database;
	private ProviderConfigurationLoader? _loader;
	private IMongoClient? _mongoClient;
	private MongoDbContainer? _mongoContainer;
	private IMongoRepository<ProviderConfigurationDocument>? _repository;

	public async Task DisposeAsync()
	{
		if (_mongoContainer != null) await _mongoContainer.DisposeAsync();
	}

	public async Task InitializeAsync()
	{
		_mongoContainer = new MongoDbBuilder()
			.WithImage("mongo:8.0")
			.Build();

		await _mongoContainer.StartAsync();

		_mongoClient = new MongoClient(_mongoContainer.GetConnectionString());
		_database = _mongoClient.GetDatabase("easymeals_test");
		_repository = new MongoRepository<ProviderConfigurationDocument>(_database);
		_loader = new ProviderConfigurationLoader(_repository);
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

	[Fact(DisplayName = "LoadConfigurationsAsync succeeds when providers exist")]
	public async Task LoadConfigurationsAsync_SucceedsWhenProvidersExist()
	{
		// Arrange
		ProviderConfigurationDocument config = CreateProviderDocument("provider_001", true);
		await _repository!.InsertOneAsync(config);

		// Act & Assert
		await _loader!.Invoking(async l => await l.LoadConfigurationsAsync())
			.Should().NotThrowAsync();
	}

	[Fact(DisplayName = "LoadConfigurationsAsync throws when no enabled providers")]
	public async Task LoadConfigurationsAsync_ThrowsWhenNoEnabledProviders()
	{
		// Arrange - No providers inserted

		// Act & Assert
		await _loader!.Invoking(async l => await l.LoadConfigurationsAsync())
			.Should().ThrowAsync<InvalidOperationException>()
			.WithMessage("*No enabled provider configurations found*");
	}

	[Fact(DisplayName = "Loader filters out disabled providers")]
	public async Task Loader_FiltersOutDisabledProviders()
	{
		// Arrange
		ProviderConfigurationDocument enabledConfig = CreateProviderDocument("provider_enabled", true);
		ProviderConfigurationDocument disabledConfig = CreateProviderDocument("provider_disabled", false);

		await _repository!.InsertOneAsync(enabledConfig);
		await _repository.InsertOneAsync(disabledConfig);

		// Act
		IEnumerable<ProviderConfiguration> configs = await _loader!.GetAllEnabledAsync();
		List<ProviderConfiguration> configList = configs.ToList();

		// Assert
		configList.Should().HaveCount(1);
		configList.Should().Contain(c => c.ProviderId == "provider_enabled");
		configList.Should().NotContain(c => c.ProviderId == "provider_disabled");
	}

	// NOTE: Caching with TTL will be tested after T087 implementation
	// This test will verify that subsequent calls use cached data
	[Fact(DisplayName = "Loader handles invalid discovery strategy gracefully")]
	public async Task Loader_HandlesInvalidDiscoveryStrategy()
	{
		// Arrange
		var invalidDoc = new ProviderConfigurationDocument
		{
			ProviderId = "invalid_provider",
			Enabled = true,
			DiscoveryStrategy = "InvalidStrategy", // Invalid enum value
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

		await _repository!.InsertOneAsync(invalidDoc);

		// Act & Assert
		await _loader!.Invoking(async l => await l.GetAllEnabledAsync())
			.Should().ThrowAsync<InvalidOperationException>()
			.WithMessage("*Invalid DiscoveryStrategy*");
	}

	[Fact(DisplayName = "Loader handles multiple concurrent reads")]
	public async Task Loader_HandlesMultipleConcurrentReads()
	{
		// Arrange
		ProviderConfigurationDocument config = CreateProviderDocument("provider_concurrent", true);
		await _repository!.InsertOneAsync(config);

		// Act - Multiple concurrent reads
		List<Task<ProviderConfiguration?>> tasks = Enumerable.Range(0, 10)
			.Select(_ => _loader!.GetByProviderIdAsync("provider_concurrent"))
			.ToList();

		ProviderConfiguration?[] results = await Task.WhenAll(tasks);

		// Assert - All should succeed
		results.Should().AllSatisfy(r => r.Should().NotBeNull());
		results.Should().AllSatisfy(r => r!.ProviderId.Should().Be("provider_concurrent"));
	}

	[Fact(DisplayName = "Loader loads configurations from MongoDB successfully")]
	public async Task Loader_LoadsConfigurationsFromMongoDB()
	{
		// Arrange
		ProviderConfigurationDocument config1 = CreateProviderDocument("provider_001", true);
		ProviderConfigurationDocument config2 = CreateProviderDocument("provider_002", true);

		await _repository!.InsertOneAsync(config1);
		await _repository.InsertOneAsync(config2);

		// Act
		IEnumerable<ProviderConfiguration> configs = await _loader!.GetAllEnabledAsync();
		List<ProviderConfiguration> configList = configs.ToList();

		// Assert
		configList.Should().HaveCount(2);
		configList.Should().Contain(c => c.ProviderId == "provider_001");
		configList.Should().Contain(c => c.ProviderId == "provider_002");
	}

	[Fact(DisplayName = "Loader returns null for non-existent provider")]
	public async Task Loader_ReturnsNullForNonExistentProvider()
	{
		// Arrange & Act
		ProviderConfiguration? result = await _loader!.GetByProviderIdAsync("nonexistent");

		// Assert
		result.Should().BeNull();
	}

	[Fact(DisplayName = "Loader returns specific provider by ID")]
	public async Task Loader_ReturnsSpecificProviderById()
	{
		// Arrange
		ProviderConfigurationDocument config = CreateProviderDocument("provider_specific", true);
		await _repository!.InsertOneAsync(config);

		// Act
		ProviderConfiguration? result = await _loader!.GetByProviderIdAsync("provider_specific");

		// Assert
		result.Should().NotBeNull();
		result!.ProviderId.Should().Be("provider_specific");
		result.BatchSize.Should().Be(10);
		result.RecipeRootUrl.Should().Be("https://example.com/recipes");
	}
}