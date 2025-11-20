using EasyMeals.RecipeEngine.Application.Interfaces;
using EasyMeals.RecipeEngine.Application.Sagas;
using EasyMeals.RecipeEngine.Domain.Entities;
using DomainInterfaces = EasyMeals.RecipeEngine.Domain.Interfaces;
using EasyMeals.RecipeEngine.Domain.Repositories;
using EasyMeals.RecipeEngine.Domain.ValueObjects;
using EasyMeals.RecipeEngine.Domain.ValueObjects.Discovery;
using EasyMeals.RecipeEngine.Domain.ValueObjects.Provider;
using EasyMeals.RecipeEngine.Infrastructure.Documents;
using EasyMeals.RecipeEngine.Infrastructure.Repositories;
using EasyMeals.RecipeEngine.Infrastructure.Services;
using EasyMeals.Shared.Data.Repositories;
using Shouldly;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Moq;
using Testcontainers.MongoDb;

namespace EasyMeals.RecipeEngine.Tests.Integration;

/// <summary>
///     Integration tests for ProviderConfiguration with nested value objects.
///     Tests the new configuration model with a real recipe provider.
/// </summary>
public class ProviderConfigurationIntegrationTests : IAsyncLifetime
{
    private MongoDbContainer? _mongoContainer;
    private IMongoDatabase? _mongoDatabase;
    private IMongoRepository<ProviderConfigurationDocument>? _repository;
    private IProviderConfigurationLoader? _configLoader;

    public async Task DisposeAsync()
    {
        if (_mongoContainer != null) await _mongoContainer.DisposeAsync();
    }

    public async Task InitializeAsync()
    {
        // Start MongoDB container for integration testing
        _mongoContainer = new MongoDbBuilder()
            .WithImage("mongo:8.0")
            .Build();

        await _mongoContainer.StartAsync();

        // Connect to MongoDB
        var mongoClient = new MongoClient(_mongoContainer.GetConnectionString());
        _mongoDatabase = mongoClient.GetDatabase("recipe-engine-test");

        _repository = new MongoRepository<ProviderConfigurationDocument>(_mongoDatabase);
        _configLoader = new ProviderConfigurationLoader(_repository);
    }

    [Fact(DisplayName = "ProviderConfiguration with nested value objects loads from MongoDB")]
    public async Task ProviderConfiguration_WithNestedValueObjects_LoadsFromMongoDB()
    {
        // Arrange - Create a configuration document for a recipe provider
        var configDocument = new ProviderConfigurationDocument
        {
            ProviderId = "recipe-provider-001",
            Enabled = true,
            Endpoint = new EndpointInfoDocument { RecipeRootUrl = "https://www.example-recipes.com/recipes" },
            Discovery = new DiscoveryConfigDocument { Strategy = "Static", RecipeUrlPattern = @"\/recipes\/[\w-]+", CategoryUrlPattern = @"\/recipes$" },
            Batching = new BatchingConfigDocument { BatchSize = 10, TimeWindowMinutes = 60 },
            RateLimit = new RateLimitConfigDocument { MinDelaySeconds = 2.0, MaxRequestsPerMinute = 20, RetryCount = 3, RequestTimeoutSeconds = 30 }
        };

        await _repository!.InsertOneAsync(configDocument);

        // Act - Load the configuration through the loader
        var config = await _configLoader!.GetByProviderIdAsync("recipe-provider-001");

        // Assert - Verify the configuration was loaded correctly
        config.ShouldNotBeNull();
        config.ProviderId.ShouldBe("recipe-provider-001");
        config.Enabled.ShouldBeTrue();

        // Verify nested value objects
        config.Endpoint.ShouldNotBeNull();
        config.Endpoint.RecipeRootUrl.ShouldBe("https://www.example-recipes.com/recipes");

        config.Discovery.ShouldNotBeNull();
        config.Discovery.Strategy.ShouldBe(DiscoveryStrategy.Static);
        config.Discovery.RecipeUrlPattern.ShouldBe(@"\/recipes\/[\w-]+");
        config.Discovery.CategoryUrlPattern.ShouldBe(@"\/recipes$");

        config.Batching.ShouldNotBeNull();
        config.Batching.BatchSize.ShouldBe(10);
        config.Batching.TimeWindow.ShouldBe(TimeSpan.FromMinutes(60));

        config.RateLimit.ShouldNotBeNull();
        config.RateLimit.MinDelay.ShouldBe(TimeSpan.FromSeconds(2.0));
        config.RateLimit.MaxRequestsPerMinute.ShouldBe(20);
        config.RateLimit.RetryCount.ShouldBe(3);
        config.RateLimit.RequestTimeout.ShouldBe(TimeSpan.FromSeconds(30));

        // Verify backward compatibility properties still work
        config.RecipeRootUrl.ShouldBe("https://www.example-recipes.com/recipes");
        config.DiscoveryStrategy.ShouldBe(DiscoveryStrategy.Static);
        config.BatchSize.ShouldBe(10);
        config.TimeWindow.ShouldBe(TimeSpan.FromMinutes(60));
    }

    [Fact(DisplayName = "ProviderConfiguration can be created with nested value objects directly")]
    public async Task ProviderConfiguration_CanBeCreated_WithNestedValueObjects()
    {
        // Arrange - Create configuration using new nested value objects pattern
        var endpoint = new EndpointInfo("https://www.example-recipes.com/recipes");
        var discovery = new DiscoveryConfig(
            DiscoveryStrategy.Static,
            recipeUrlPattern: @"\/recipes\/[\w-]+",
            categoryUrlPattern: @"\/recipes$");
        var batching = new BatchingConfig(batchSize: 10, timeWindowMinutes: 60);
        var rateLimit = new RateLimitConfig(
            minDelaySeconds: 2.0,
            maxRequestsPerMinute: 20,
            retryCount: 3,
            requestTimeoutSeconds: 30);

        // Act - Create the configuration
        var config = new ProviderConfiguration(
            "recipe-provider-002",
            enabled: true,
            endpoint,
            discovery,
            batching,
            rateLimit);

        // Assert - Verify all nested objects are properly set
        config.ProviderId.ShouldBe("recipe-provider-002");
        config.Enabled.ShouldBeTrue();
        config.Endpoint.ShouldBe(endpoint);
        config.Discovery.ShouldBe(discovery);
        config.Batching.ShouldBe(batching);
        config.RateLimit.ShouldBe(rateLimit);

        // Verify convenience properties
        config.RecipeRootUrl.ShouldBe("https://www.example-recipes.com/recipes");
        config.DiscoveryStrategy.ShouldBe(DiscoveryStrategy.Static);
        config.BatchSize.ShouldBe(10);
    }

    [Fact(DisplayName = "Multiple provider configurations can be loaded and cached")]
    public async Task MultipleProviderConfigurations_CanBeLoadedAndCached()
    {
        // Arrange - Create multiple provider configurations
        var configs = new[]
        {
            new ProviderConfigurationDocument
            {
                ProviderId = "provider-a",
                Enabled = true,
                Endpoint = new EndpointInfoDocument { RecipeRootUrl = "https://www.provider-a.com/recipes" },
                Discovery = new DiscoveryConfigDocument { Strategy = "Static" },
                Batching = new BatchingConfigDocument { BatchSize = 10, TimeWindowMinutes = 60 },
                RateLimit = new RateLimitConfigDocument { MinDelaySeconds = 2.0, MaxRequestsPerMinute = 20, RetryCount = 3, RequestTimeoutSeconds = 30 }
            },
            new ProviderConfigurationDocument
            {
                ProviderId = "provider-b",
                Enabled = true,
                Endpoint = new EndpointInfoDocument { RecipeRootUrl = "https://www.provider-b.com/recipes" },
                Discovery = new DiscoveryConfigDocument { Strategy = "Dynamic" },
                Batching = new BatchingConfigDocument { BatchSize = 5, TimeWindowMinutes = 30 },
                RateLimit = new RateLimitConfigDocument { MinDelaySeconds = 1.0, MaxRequestsPerMinute = 30, RetryCount = 2, RequestTimeoutSeconds = 45 }
            }
        };

        foreach (var config in configs)
        {
            await _repository!.InsertOneAsync(config);
        }

        // Act - Load all configurations
        await _configLoader!.LoadConfigurationsAsync();

        // Assert - Verify both configurations are loaded
        var configA = await _configLoader.GetByProviderIdAsync("provider-a");
        var configB = await _configLoader.GetByProviderIdAsync("provider-b");

        configA.ShouldNotBeNull();
        configA.ProviderId.ShouldBe("provider-a");
        configA.Discovery.Strategy.ShouldBe(DiscoveryStrategy.Static);

        configB.ShouldNotBeNull();
        configB.ProviderId.ShouldBe("provider-b");
        configB.Discovery.Strategy.ShouldBe(DiscoveryStrategy.Dynamic);
    }

    [Fact(DisplayName = "ProviderConfiguration validates nested value objects on construction")]
    public void ProviderConfiguration_ValidatesNestedValueObjects_OnConstruction()
    {
        // Arrange
        var endpoint = new EndpointInfo("https://www.example-recipes.com/recipes");
        var discovery = new DiscoveryConfig(DiscoveryStrategy.Static);
        var batching = new BatchingConfig(10, 60);
        var rateLimit = new RateLimitConfig(2.0, 20, 3, 30);

        // Act & Assert - Null endpoint should throw
        Should.Throw<ArgumentNullException>(() =>
            new ProviderConfiguration("test", true, null!, discovery, batching, rateLimit));

        // Act & Assert - Null discovery should throw
        Should.Throw<ArgumentNullException>(() =>
            new ProviderConfiguration("test", true, endpoint, null!, batching, rateLimit));

        // Act & Assert - Null batching should throw
        Should.Throw<ArgumentNullException>(() =>
            new ProviderConfiguration("test", true, endpoint, discovery, null!, rateLimit));

        // Act & Assert - Null rateLimit should throw
        Should.Throw<ArgumentNullException>(() =>
            new ProviderConfiguration("test", true, endpoint, discovery, batching, null!));
    }

    [Fact(DisplayName = "EndpointInfo validates HTTPS requirement")]
    public void EndpointInfo_ValidatesHttpsRequirement()
    {
        // Act & Assert - HTTP should throw
        var ex = Should.Throw<ArgumentException>(() => new EndpointInfo("http://www.example.com/recipes"));
        ex.Message.ShouldContain("HTTPS");

        // Act & Assert - Valid HTTPS should work
        var endpoint = new EndpointInfo("https://www.example.com/recipes");
        endpoint.RecipeRootUrl.ShouldBe("https://www.example.com/recipes");
    }

    [Fact(DisplayName = "DiscoveryConfig validates regex patterns")]
    public void DiscoveryConfig_ValidatesRegexPatterns()
    {
        // Act & Assert - Invalid recipe pattern should throw
        var ex = Should.Throw<ArgumentException>(() =>
            new DiscoveryConfig(DiscoveryStrategy.Static, recipeUrlPattern: "[invalid"));
        ex.Message.ShouldContain("not a valid regex");

        // Act & Assert - Invalid category pattern should throw
        ex = Should.Throw<ArgumentException>(() =>
            new DiscoveryConfig(DiscoveryStrategy.Static, categoryUrlPattern: "(?<invalid"));
        ex.Message.ShouldContain("not a valid regex");

        // Act & Assert - Valid patterns should work
        var config = new DiscoveryConfig(
            DiscoveryStrategy.Static,
            recipeUrlPattern: @"\/recipes\/[\w-]+",
            categoryUrlPattern: @"\/recipes$");
        config.RecipeUrlPattern.ShouldBe(@"\/recipes\/[\w-]+");
    }

    [Fact(DisplayName = "BatchingConfig validates positive values")]
    public void BatchingConfig_ValidatesPositiveValues()
    {
        // Act & Assert - Zero batch size should throw
        Should.Throw<ArgumentException>(() => new BatchingConfig(0, 60));

        // Act & Assert - Negative batch size should throw
        Should.Throw<ArgumentException>(() => new BatchingConfig(-1, 60));

        // Act & Assert - Zero time window should throw
        Should.Throw<ArgumentException>(() => new BatchingConfig(10, 0));

        // Act & Assert - Valid values should work
        var config = new BatchingConfig(10, 60);
        config.BatchSize.ShouldBe(10);
        config.TimeWindow.ShouldBe(TimeSpan.FromMinutes(60));
    }

    [Fact(DisplayName = "RateLimitConfig validates constraints")]
    public void RateLimitConfig_ValidatesConstraints()
    {
        // Act & Assert - Negative min delay should throw
        Should.Throw<ArgumentException>(() => new RateLimitConfig(-1.0, 20, 3, 30));

        // Act & Assert - Zero max requests should throw
        Should.Throw<ArgumentException>(() => new RateLimitConfig(2.0, 0, 3, 30));

        // Act & Assert - Negative retry count should throw
        Should.Throw<ArgumentException>(() => new RateLimitConfig(2.0, 20, -1, 30));

        // Act & Assert - Zero timeout should throw
        Should.Throw<ArgumentException>(() => new RateLimitConfig(2.0, 20, 3, 0));

        // Act & Assert - Valid values should work
        var config = new RateLimitConfig(2.0, 20, 3, 30);
        config.MinDelay.ShouldBe(TimeSpan.FromSeconds(2.0));
        config.MaxRequestsPerMinute.ShouldBe(20);
        config.RetryCount.ShouldBe(3);
        config.RequestTimeout.ShouldBe(TimeSpan.FromSeconds(30));
    }
}
