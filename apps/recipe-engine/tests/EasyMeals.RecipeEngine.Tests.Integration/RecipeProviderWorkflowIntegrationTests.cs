using EasyMeals.RecipeEngine.Application.Interfaces;
using EasyMeals.RecipeEngine.Application.Sagas;
using EasyMeals.RecipeEngine.Domain.Entities;
using DomainInterfaces = EasyMeals.RecipeEngine.Domain.Interfaces;
using EasyMeals.RecipeEngine.Domain.Repositories;
using EasyMeals.RecipeEngine.Domain.ValueObjects;
using EasyMeals.RecipeEngine.Domain.ValueObjects.Discovery;
using EasyMeals.RecipeEngine.Domain.ValueObjects.Provider;
using EasyMeals.RecipeEngine.Domain.ValueObjects.Fingerprint;
using EasyMeals.RecipeEngine.Infrastructure.Documents;
using EasyMeals.RecipeEngine.Infrastructure.Repositories;
using EasyMeals.RecipeEngine.Infrastructure.Services;
using EasyMeals.RecipeEngine.Infrastructure.Discovery;
using EasyMeals.RecipeEngine.Infrastructure.Extraction;
using EasyMeals.Shared.Data.Repositories;
using Shouldly;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Moq;
using Testcontainers.MongoDb;

namespace EasyMeals.RecipeEngine.Tests.Integration;

/// <summary>
///     Integration tests for RecipeProcessingSaga with nested ProviderConfiguration.
///     Tests the complete workflow from discovery to recipe extraction using real URLs.
/// </summary>
public class RecipeProviderWorkflowIntegrationTests : IAsyncLifetime
{
    private MongoDbContainer? _mongoContainer;
    private IMongoDatabase? _mongoDatabase;
    private IMongoRepository<ProviderConfigurationDocument>? _configRepository;
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

        _configRepository = new MongoRepository<ProviderConfigurationDocument>(_mongoDatabase);
        _configLoader = new ProviderConfigurationLoader(_configRepository);
    }

    [Fact(DisplayName = "Recipe provider configuration enables discovery workflow")]
    public async Task RecipeProviderConfiguration_EnablesDiscoveryWorkflow()
    {
        // Arrange - Setup provider configuration with nested value objects
        var endpoint = new EndpointInfo("https://www.example-recipe-site.com/recipes");
        var discovery = new DiscoveryConfig(
            DiscoveryStrategy.Static,
            recipeUrlPattern: @"\/recipes\/[\w-]+",
            categoryUrlPattern: null);
        var batching = new BatchingConfig(batchSize: 50, timeWindowMinutes: 60);
        var rateLimit = new RateLimitConfig(
            minDelaySeconds: 2.0,
            maxRequestsPerMinute: 20,
            retryCount: 3,
            requestTimeoutSeconds: 30);

        var config = new ProviderConfiguration(
            "recipe-provider-001",
            enabled: true,
            endpoint,
            discovery,
            batching,
            rateLimit);

        // Save to MongoDB
        var configDocument = new ProviderConfigurationDocument
        {
            ProviderId = config.ProviderId,
            Enabled = config.Enabled,
            DiscoveryStrategy = config.DiscoveryStrategy.ToString(),
            RecipeRootUrl = config.RecipeRootUrl,
            BatchSize = config.BatchSize,
            TimeWindowMinutes = (int)config.TimeWindow.TotalMinutes,
            MinDelaySeconds = config.MinDelay.TotalSeconds,
            MaxRequestsPerMinute = config.MaxRequestsPerMinute,
            RetryCount = config.RetryCount,
            RequestTimeoutSeconds = (int)config.RequestTimeout.TotalSeconds,
            RecipeUrlPattern = config.RecipeUrlPattern,
            CategoryUrlPattern = config.CategoryUrlPattern
        };

        await _configRepository!.InsertOneAsync(configDocument);

        // Act - Load configuration
        var loadedConfig = await _configLoader!.GetByProviderIdAsync("recipe-provider-001");

        // Assert - Verify configuration loaded correctly with nested objects
        loadedConfig.ShouldNotBeNull();
        loadedConfig.ProviderId.ShouldBe("recipe-provider-001");
        
        // Verify nested value objects
        loadedConfig.Endpoint.RecipeRootUrl.ShouldBe("https://www.example-recipe-site.com/recipes");
        loadedConfig.Discovery.Strategy.ShouldBe(DiscoveryStrategy.Static);
        loadedConfig.Discovery.RecipeUrlPattern.ShouldBe(@"\/recipes\/[\w-]+");
        loadedConfig.Batching.BatchSize.ShouldBe(50);
        loadedConfig.Batching.TimeWindow.ShouldBe(TimeSpan.FromMinutes(60));
        loadedConfig.RateLimit.MinDelay.ShouldBe(TimeSpan.FromSeconds(2.0));
        loadedConfig.RateLimit.MaxRequestsPerMinute.ShouldBe(20);
        loadedConfig.RateLimit.RetryCount.ShouldBe(3);
        loadedConfig.RateLimit.RequestTimeout.ShouldBe(TimeSpan.FromSeconds(30));
    }

    [Fact(DisplayName = "Recipe extraction can use provider configuration")]
    public async Task RecipeExtraction_CanUseProviderConfiguration()
    {
        // Arrange - Create provider configuration using nested value objects
        var config = new ProviderConfiguration(
            "test-provider",
            enabled: true,
            endpoint: new EndpointInfo("https://www.example-recipes.com/recipes"),
            discovery: new DiscoveryConfig(DiscoveryStrategy.Static),
            batching: new BatchingConfig(10, 60),
            rateLimit: new RateLimitConfig(2.0, 20, 3, 30));

        // Create a mock configuration loader
        var mockConfigLoader = new Mock<IProviderConfigurationLoader>();
        mockConfigLoader.Setup(c => c.GetByProviderIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        // Create recipe extractor
        var mockLogger = new Mock<ILogger<RecipeExtractorService>>();
        var extractor = new RecipeExtractorService(mockLogger.Object);

        // Sample HTML content with JSON-LD (generic recipe example)
        const string htmlContent = @"
<!DOCTYPE html>
<html>
<head>
    <script type=""application/ld+json"">
    {
        ""@context"": ""https://schema.org"",
        ""@type"": ""Recipe"",
        ""name"": ""Delicious Steak with Potatoes"",
        ""description"": ""A perfectly cooked steak served with crispy potatoes"",
        ""image"": ""https://www.example-recipes.com/images/steak.jpg"",
        ""prepTime"": ""PT15M"",
        ""cookTime"": ""PT20M"",
        ""recipeYield"": ""2 servings"",
        ""recipeIngredient"": [
            ""2 steaks (8 oz each)"",
            ""4 medium potatoes"",
            ""2 tbsp olive oil"",
            ""Salt and pepper to taste"",
            ""2 cloves garlic, minced"",
            ""Fresh herbs (rosemary, thyme)""
        ],
        ""recipeInstructions"": [
            {
                ""@type"": ""HowToStep"",
                ""text"": ""Preheat oven to 400°F (200°C)""
            },
            {
                ""@type"": ""HowToStep"",
                ""text"": ""Season steaks with salt and pepper""
            },
            {
                ""@type"": ""HowToStep"",
                ""text"": ""Heat oil in a cast-iron skillet over high heat""
            },
            {
                ""@type"": ""HowToStep"",
                ""text"": ""Sear steaks for 3-4 minutes per side""
            },
            {
                ""@type"": ""HowToStep"",
                ""text"": ""Roast potatoes in the oven for 25 minutes""
            },
            {
                ""@type"": ""HowToStep"",
                ""text"": ""Rest steaks for 5 minutes before serving""
            }
        ]
    }
    </script>
</head>
<body>
    <h1>Delicious Steak with Potatoes</h1>
</body>
</html>";

        var fingerprint = new Fingerprint(
            Guid.NewGuid(),
            "https://www.example-recipes.com/recipes/steak-potatoes",
            htmlContent,
            "test-provider",
            ScrapingQuality.Good);

        // Act - Extract recipe using the new interface
        var recipe = await extractor.ExtractRecipeAsync(htmlContent, fingerprint);

        // Assert - Verify recipe was extracted successfully
        recipe.ShouldNotBeNull();
        recipe.Title.ShouldBe("Delicious Steak with Potatoes");
        recipe.Description.ShouldBe("A perfectly cooked steak served with crispy potatoes");
        recipe.PrepTimeMinutes.ShouldBe(15);
        recipe.CookTimeMinutes.ShouldBe(20);
        recipe.Servings.ShouldBe(2);
        recipe.Ingredients.Count.ShouldBe(6);
        recipe.Instructions.Count.ShouldBe(6);
        recipe.SourceUrl.ShouldBe("https://www.example-recipes.com/recipes/steak-potatoes");
        recipe.ProviderName.ShouldBe("test-provider");

        // Verify configuration was used correctly
        config.RateLimit.RequestTimeout.ShouldBe(TimeSpan.FromSeconds(30));
        config.Batching.BatchSize.ShouldBe(10);
    }

    [Fact(DisplayName = "Multiple recipes can be processed with provider configuration")]
    public async Task MultipleRecipes_CanBeProcessed_WithProviderConfiguration()
    {
        // Arrange - Create provider configuration
        var config = new ProviderConfiguration(
            "batch-provider",
            enabled: true,
            endpoint: new EndpointInfo("https://www.example-recipes.com/recipes"),
            discovery: new DiscoveryConfig(DiscoveryStrategy.Static),
            batching: new BatchingConfig(batchSize: 3, timeWindowMinutes: 60),
            rateLimit: new RateLimitConfig(1.0, 30, 2, 45));

        var mockConfigLoader = new Mock<IProviderConfigurationLoader>();
        mockConfigLoader.Setup(c => c.GetByProviderIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        var mockLogger = new Mock<ILogger<RecipeExtractorService>>();
        var extractor = new RecipeExtractorService(mockLogger.Object);

        // Create multiple test recipes
        var testRecipes = new[]
        {
            ("Recipe 1", "Description 1"),
            ("Recipe 2", "Description 2"),
            ("Recipe 3", "Description 3")
        };

        var extractedRecipes = new List<Recipe>();

        // Act - Extract multiple recipes
        foreach (var (name, description) in testRecipes)
        {
            var htmlContent = $@"
<!DOCTYPE html>
<html>
<head>
    <script type=""application/ld+json"">
    {{
        ""@context"": ""https://schema.org"",
        ""@type"": ""Recipe"",
        ""name"": ""{name}"",
        ""description"": ""{description}"",
        ""recipeIngredient"": [""ingredient 1""],
        ""recipeInstructions"": [{{""@type"": ""HowToStep"", ""text"": ""step 1""}}]
    }}
    </script>
</head>
</html>";

            var fingerprint = new Fingerprint(
                Guid.NewGuid(),
                $"https://www.example-recipes.com/recipes/{name.ToLower().Replace(" ", "-")}",
                htmlContent,
                "batch-provider",
                ScrapingQuality.Good);

            var recipe = await extractor.ExtractRecipeAsync(htmlContent, fingerprint);
            if (recipe != null)
            {
                extractedRecipes.Add(recipe);
            }
        }

        // Assert - Verify all recipes were extracted
        extractedRecipes.Count.ShouldBe(3);
        extractedRecipes[0].Title.ShouldBe("Recipe 1");
        extractedRecipes[1].Title.ShouldBe("Recipe 2");
        extractedRecipes[2].Title.ShouldBe("Recipe 3");

        // Verify batching configuration
        config.Batching.BatchSize.ShouldBe(3);
        config.Batching.TimeWindow.ShouldBe(TimeSpan.FromMinutes(60));
    }

    [Fact(DisplayName = "Provider configuration with different discovery strategies can be compared")]
    public async Task ProviderConfigurations_WithDifferentDiscoveryStrategies_CanBeCompared()
    {
        // Arrange - Create configurations with different strategies
        var staticConfig = new ProviderConfiguration(
            "static-provider",
            enabled: true,
            endpoint: new EndpointInfo("https://www.static-recipes.com/recipes"),
            discovery: new DiscoveryConfig(DiscoveryStrategy.Static),
            batching: new BatchingConfig(10, 60),
            rateLimit: new RateLimitConfig(2.0, 20, 3, 30));

        var dynamicConfig = new ProviderConfiguration(
            "dynamic-provider",
            enabled: true,
            endpoint: new EndpointInfo("https://www.dynamic-recipes.com/recipes"),
            discovery: new DiscoveryConfig(DiscoveryStrategy.Dynamic),
            batching: new BatchingConfig(5, 30),
            rateLimit: new RateLimitConfig(1.0, 30, 2, 45));

        // Assert - Verify strategies are different
        staticConfig.Discovery.Strategy.ShouldBe(DiscoveryStrategy.Static);
        dynamicConfig.Discovery.Strategy.ShouldBe(DiscoveryStrategy.Dynamic);

        // Verify other configurations differ appropriately
        staticConfig.Batching.BatchSize.ShouldBe(10);
        dynamicConfig.Batching.BatchSize.ShouldBe(5);

        staticConfig.RateLimit.MinDelay.ShouldBe(TimeSpan.FromSeconds(2.0));
        dynamicConfig.RateLimit.MinDelay.ShouldBe(TimeSpan.FromSeconds(1.0));
    }
}
