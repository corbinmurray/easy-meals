using EasyMeals.RecipeEngine.Infrastructure.Documents;
using EasyMeals.RecipeEngine.Infrastructure.Services;
using EasyMeals.Shared.Data.Repositories;
using FluentAssertions;
using MongoDB.Driver;
using Testcontainers.MongoDb;

namespace EasyMeals.RecipeEngine.Tests.Integration;

/// <summary>
/// Integration tests for multi-provider configuration processing.
/// Verifies that multiple providers can be loaded with different settings.
/// </summary>
public class MultiProviderProcessingTests : IAsyncLifetime
{
	private MongoDbContainer? _mongoContainer;
	private IMongoClient? _mongoClient;
	private IMongoDatabase? _database;
	private IMongoRepository<ProviderConfigurationDocument>? _repository;
	private ProviderConfigurationLoader? _loader;

	public async Task InitializeAsync()
	{
		_mongoContainer = new MongoDbBuilder()
			.WithImage("mongo:7.0")
			.Build();

		await _mongoContainer.StartAsync();

		_mongoClient = new MongoClient(_mongoContainer.GetConnectionString());
		_database = _mongoClient.GetDatabase("easymeals_test");
		_repository = new MongoRepository<ProviderConfigurationDocument>(_database);
		_loader = new ProviderConfigurationLoader(_repository);
	}

	public async Task DisposeAsync()
	{
		if (_mongoContainer != null)
		{
			await _mongoContainer.DisposeAsync();
		}
	}

	[Fact(DisplayName = "Loader handles multiple providers with different settings")]
	public async Task Loader_HandlesMultipleProvidersWithDifferentSettings()
	{
		// Arrange
		var provider1 = new ProviderConfigurationDocument
		{
			ProviderId = "provider_001",
			Enabled = true,
			DiscoveryStrategy = "Dynamic",
			RecipeRootUrl = "https://provider1.com/recipes",
			BatchSize = 10,
			TimeWindowMinutes = 10,
			MinDelaySeconds = 2,
			MaxRequestsPerMinute = 10,
			RetryCount = 3,
			RequestTimeoutSeconds = 30,
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		};

		var provider2 = new ProviderConfigurationDocument
		{
			ProviderId = "provider_002",
			Enabled = true,
			DiscoveryStrategy = "Static",
			RecipeRootUrl = "https://provider2.com/recipes",
			BatchSize = 20,  // Different batch size
			TimeWindowMinutes = 15,  // Different time window
			MinDelaySeconds = 3,  // Different delay
			MaxRequestsPerMinute = 5,  // Different rate limit
			RetryCount = 5,  // Different retry count
			RequestTimeoutSeconds = 60,  // Different timeout
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		};

		await _repository!.InsertOneAsync(provider1);
		await _repository.InsertOneAsync(provider2);

		// Act
		var configs = await _loader!.GetAllEnabledAsync();
		var configList = configs.ToList();

		// Assert - Both providers loaded
		configList.Should().HaveCount(2);

		// Verify provider_001 settings
		var config1 = configList.First(c => c.ProviderId == "provider_001");
		config1.RecipeRootUrl.Should().Be("https://provider1.com/recipes");
		config1.BatchSize.Should().Be(10);
		config1.TimeWindow.Should().Be(TimeSpan.FromMinutes(10));
		config1.MinDelay.Should().Be(TimeSpan.FromSeconds(2));
		config1.MaxRequestsPerMinute.Should().Be(10);
		config1.RetryCount.Should().Be(3);
		config1.RequestTimeout.Should().Be(TimeSpan.FromSeconds(30));

		// Verify provider_002 settings
		var config2 = configList.First(c => c.ProviderId == "provider_002");
		config2.RecipeRootUrl.Should().Be("https://provider2.com/recipes");
		config2.BatchSize.Should().Be(20);
		config2.TimeWindow.Should().Be(TimeSpan.FromMinutes(15));
		config2.MinDelay.Should().Be(TimeSpan.FromSeconds(3));
		config2.MaxRequestsPerMinute.Should().Be(5);
		config2.RetryCount.Should().Be(5);
		config2.RequestTimeout.Should().Be(TimeSpan.FromSeconds(60));
	}

	[Fact(DisplayName = "Loader processes providers sequentially without conflicts")]
	public async Task Loader_ProcessesProvidersSequentiallyWithoutConflicts()
	{
		// Arrange - Multiple providers
		var providers = new List<ProviderConfigurationDocument>
		{
			CreateProvider("provider_001", "https://provider1.com/recipes", 10),
			CreateProvider("provider_002", "https://provider2.com/recipes", 20),
			CreateProvider("provider_003", "https://provider3.com/recipes", 30)
		};

		foreach (var provider in providers)
		{
			await _repository!.InsertOneAsync(provider);
		}

		// Act - Load all providers sequentially
		var results = new List<string>();
		foreach (var provider in providers)
		{
			var config = await _loader!.GetByProviderIdAsync(provider.ProviderId);
			if (config != null)
			{
				results.Add(config.ProviderId);
			}
		}

		// Assert - All providers processed without conflicts
		results.Should().HaveCount(3);
		results.Should().Contain("provider_001");
		results.Should().Contain("provider_002");
		results.Should().Contain("provider_003");
	}

	[Fact(DisplayName = "Loader handles mixed enabled and disabled providers")]
	public async Task Loader_HandlesMixedEnabledAndDisabledProviders()
	{
		// Arrange
		var enabledProvider1 = CreateProvider("provider_enabled_1", "https://provider1.com/recipes", 10, true);
		var disabledProvider = CreateProvider("provider_disabled", "https://provider2.com/recipes", 20, false);
		var enabledProvider2 = CreateProvider("provider_enabled_2", "https://provider3.com/recipes", 30, true);

		await _repository!.InsertOneAsync(enabledProvider1);
		await _repository.InsertOneAsync(disabledProvider);
		await _repository.InsertOneAsync(enabledProvider2);

		// Act
		var configs = await _loader!.GetAllEnabledAsync();
		var configList = configs.ToList();

		// Assert - Only enabled providers returned
		configList.Should().HaveCount(2);
		configList.Should().Contain(c => c.ProviderId == "provider_enabled_1");
		configList.Should().Contain(c => c.ProviderId == "provider_enabled_2");
		configList.Should().NotContain(c => c.ProviderId == "provider_disabled");
	}

	[Fact(DisplayName = "Loader distinguishes between different discovery strategies")]
	public async Task Loader_DistinguishesBetweenDifferentDiscoveryStrategies()
	{
		// Arrange
		var staticProvider = CreateProviderWithStrategy("static_provider", "Static");
		var dynamicProvider = CreateProviderWithStrategy("dynamic_provider", "Dynamic");
		var apiProvider = CreateProviderWithStrategy("api_provider", "Api");

		await _repository!.InsertOneAsync(staticProvider);
		await _repository.InsertOneAsync(dynamicProvider);
		await _repository.InsertOneAsync(apiProvider);

		// Act
		var configs = await _loader!.GetAllEnabledAsync();
		var configList = configs.ToList();

		// Assert - All three strategies loaded correctly
		configList.Should().HaveCount(3);
		
		var staticConfig = configList.First(c => c.ProviderId == "static_provider");
		staticConfig.DiscoveryStrategy.ToString().Should().Be("Static");

		var dynamicConfig = configList.First(c => c.ProviderId == "dynamic_provider");
		dynamicConfig.DiscoveryStrategy.ToString().Should().Be("Dynamic");

		var apiConfig = configList.First(c => c.ProviderId == "api_provider");
		apiConfig.DiscoveryStrategy.ToString().Should().Be("Api");
	}

	[Fact(DisplayName = "LoadConfigurationsAsync validates all providers at startup")]
	public async Task LoadConfigurationsAsync_ValidatesAllProvidersAtStartup()
	{
		// Arrange - Multiple valid providers
		await _repository!.InsertOneAsync(CreateProvider("provider_001", "https://provider1.com/recipes", 10));
		await _repository.InsertOneAsync(CreateProvider("provider_002", "https://provider2.com/recipes", 20));
		await _repository.InsertOneAsync(CreateProvider("provider_003", "https://provider3.com/recipes", 30));

		// Act & Assert - Should not throw
		await _loader!.Invoking(async l => await l.LoadConfigurationsAsync())
			.Should().NotThrowAsync();
	}

	private static ProviderConfigurationDocument CreateProvider(
		string providerId,
		string url,
		int batchSize,
		bool enabled = true)
	{
		return new ProviderConfigurationDocument
		{
			ProviderId = providerId,
			Enabled = enabled,
			DiscoveryStrategy = "Dynamic",
			RecipeRootUrl = url,
			BatchSize = batchSize,
			TimeWindowMinutes = 10,
			MinDelaySeconds = 2,
			MaxRequestsPerMinute = 10,
			RetryCount = 3,
			RequestTimeoutSeconds = 30,
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		};
	}

	private static ProviderConfigurationDocument CreateProviderWithStrategy(
		string providerId,
		string strategy)
	{
		return new ProviderConfigurationDocument
		{
			ProviderId = providerId,
			Enabled = true,
			DiscoveryStrategy = strategy,
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
