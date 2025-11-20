using EasyMeals.RecipeEngine.Application.Interfaces;
using EasyMeals.RecipeEngine.Application.Sagas;
using EasyMeals.RecipeEngine.Domain.Entities;
using EasyMeals.RecipeEngine.Domain.Events;
using DomainInterfaces = EasyMeals.RecipeEngine.Domain.Interfaces;
using EasyMeals.RecipeEngine.Domain.Repositories;
using EasyMeals.RecipeEngine.Domain.ValueObjects;
using EasyMeals.RecipeEngine.Domain.ValueObjects.Discovery;
using EasyMeals.RecipeEngine.Domain.ValueObjects.Provider;
using EasyMeals.RecipeEngine.Domain.ValueObjects.Fingerprint;
using EasyMeals.RecipeEngine.Infrastructure.Documents;
using EasyMeals.RecipeEngine.Infrastructure.Repositories;
using EasyMeals.RecipeEngine.Infrastructure.Services;
using EasyMeals.RecipeEngine.Infrastructure.Extraction;
using EasyMeals.Shared.Data.Repositories;
using Shouldly;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Moq;
using Testcontainers.MongoDb;

namespace EasyMeals.RecipeEngine.Tests.Integration;

/// <summary>
///     End-to-end integration tests for RecipeProcessingSaga.
///     Tests the complete workflow: discovery → fingerprinting → extraction → persistence.
///     Verifies that a valid parsed recipe exists at the end.
/// </summary>
public class RecipeProcessingSagaEndToEndTests : IAsyncLifetime
{
    private MongoDbContainer? _mongoContainer;
    private IMongoDatabase? _mongoDatabase;
    private ISagaStateRepository? _sagaRepository;
    private IRecipeRepository? _recipeRepository;
    private IFingerprintRepository? _fingerprintRepository;

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

        _sagaRepository = new SagaStateRepository(_mongoDatabase);
        _recipeRepository = new RecipeRepository(_mongoDatabase);
        _fingerprintRepository = new FingerprintRepository(_mongoDatabase);
    }

    [Fact(DisplayName = "End-to-end: Discovery through recipe creation produces valid recipe")]
    public async Task EndToEnd_DiscoveryThroughRecipeCreation_ProducesValidRecipe()
    {
        // Arrange - Setup provider configuration
        var config = new ProviderConfiguration(
            "e2e-provider",
            enabled: true,
            endpoint: new EndpointInfo("https://www.example-recipes.com/recipes"),
            discovery: new DiscoveryConfig(DiscoveryStrategy.Static),
            batching: new BatchingConfig(10, 60),
            rateLimit: new RateLimitConfig(1.0, 30, 3, 30));

        var mockConfigLoader = new Mock<IProviderConfigurationLoader>();
        mockConfigLoader.Setup(c => c.GetByProviderIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        // Mock discovery service to return test URLs
        var mockDiscoveryService = new Mock<DomainInterfaces.IDiscoveryService>();
        var discoveredUrls = new List<DiscoveredUrl>
        {
            new("https://www.example-recipes.com/recipes/pasta-primavera", "e2e-provider", DateTime.UtcNow)
        };

        mockDiscoveryService.Setup(d => d.DiscoverRecipeUrlsAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(discoveredUrls);

        var mockFactory = new Mock<IDiscoveryServiceFactory>();
        mockFactory.Setup(f => f.CreateDiscoveryService(It.IsAny<DiscoveryStrategy>()))
            .Returns(mockDiscoveryService.Object);

        // Mock HTTP client to return recipe HTML
        // This mimics a real recipe site structure with schema.org JSON-LD markup
        const string recipeHtml = @"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Seared Steak with Roasted Potatoes - Recipe Site</title>
    <meta name=""description"" content=""Juicy seared steak paired with crispy roasted potatoes and fresh vegetables"">
    
    <script type=""application/ld+json"">
    {
        ""@context"": ""https://schema.org"",
        ""@type"": ""Recipe"",
        ""name"": ""Seared Steak with Roasted Potatoes"",
        ""description"": ""Juicy seared steak paired with crispy roasted potatoes and fresh vegetables"",
        ""image"": [
            ""https://www.example-recipes.com/images/steak-potatoes-1.jpg"",
            ""https://www.example-recipes.com/images/steak-potatoes-2.jpg""
        ],
        ""author"": {
            ""@type"": ""Person"",
            ""name"": ""Chef Demo""
        },
        ""datePublished"": ""2024-01-15"",
        ""prepTime"": ""PT15M"",
        ""cookTime"": ""PT25M"",
        ""totalTime"": ""PT40M"",
        ""recipeYield"": ""2 servings"",
        ""recipeCategory"": ""Main Course"",
        ""recipeCuisine"": ""American"",
        ""keywords"": ""steak, potatoes, dinner, main course"",
        ""nutrition"": {
            ""@type"": ""NutritionInformation"",
            ""calories"": ""650 calories"",
            ""proteinContent"": ""45g"",
            ""fatContent"": ""35g"",
            ""carbohydrateContent"": ""40g""
        },
        ""recipeIngredient"": [
            ""2 ribeye steaks (8 oz each)"",
            ""1 lb baby potatoes, halved"",
            ""4 tablespoons olive oil, divided"",
            ""2 cloves garlic, minced"",
            ""1 cup green beans, trimmed"",
            ""2 tablespoons butter"",
            ""Fresh rosemary and thyme"",
            ""Salt and black pepper to taste"",
            ""1 tablespoon balsamic vinegar""
        ],
        ""recipeInstructions"": [
            {
                ""@type"": ""HowToStep"",
                ""name"": ""Prepare ingredients"",
                ""text"": ""Remove steaks from refrigerator 30 minutes before cooking. Pat dry and season generously with salt and pepper."",
                ""position"": 1
            },
            {
                ""@type"": ""HowToStep"",
                ""name"": ""Roast potatoes"",
                ""text"": ""Preheat oven to 425°F (220°C). Toss potatoes with 2 tablespoons olive oil, salt, pepper, and herbs. Roast for 25-30 minutes until golden."",
                ""position"": 2
            },
            {
                ""@type"": ""HowToStep"",
                ""name"": ""Sear the steak"",
                ""text"": ""Heat remaining olive oil in a cast-iron skillet over high heat. Sear steaks for 3-4 minutes per side for medium-rare."",
                ""position"": 3
            },
            {
                ""@type"": ""HowToStep"",
                ""name"": ""Add butter and herbs"",
                ""text"": ""Add butter, garlic, and fresh herbs to the pan. Baste steaks with the melted butter mixture."",
                ""position"": 4
            },
            {
                ""@type"": ""HowToStep"",
                ""name"": ""Cook vegetables"",
                ""text"": ""In the same pan, quickly sauté green beans for 3-4 minutes until tender-crisp."",
                ""position"": 5
            },
            {
                ""@type"": ""HowToStep"",
                ""name"": ""Rest and serve"",
                ""text"": ""Let steaks rest for 5 minutes. Drizzle with balsamic vinegar. Serve with roasted potatoes and green beans."",
                ""position"": 6
            }
        ],
        ""aggregateRating"": {
            ""@type"": ""AggregateRating"",
            ""ratingValue"": ""4.8"",
            ""ratingCount"": ""156""
        }
    }
    </script>
</head>
<body>
    <article class=""recipe"">
        <header>
            <h1>Seared Steak with Roasted Potatoes</h1>
            <p class=""description"">Juicy seared steak paired with crispy roasted potatoes and fresh vegetables</p>
        </header>
        <section class=""recipe-details"">
            <div class=""prep-time"">Prep: 15 min</div>
            <div class=""cook-time"">Cook: 25 min</div>
            <div class=""servings"">Serves: 2</div>
        </section>
    </article>
</body>
</html>";

        var mockHttpClient = new Mock<DomainInterfaces.IStealthyHttpClient>();
        mockHttpClient.Setup(h => h.GetStringAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(recipeHtml);

        // Use real recipe extractor
        var extractor = new RecipeExtractorService(Mock.Of<ILogger<RecipeExtractorService>>());

        // Mock rate limiter to always allow
        var mockRateLimiter = new Mock<IRateLimiter>();
        mockRateLimiter.Setup(r => r.TryAcquireAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Mock ingredient normalizer
        var mockNormalizer = new Mock<IIngredientNormalizer>();
        mockNormalizer.Setup(n => n.NormalizeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string providerId, string providerCode, CancellationToken _) => providerCode);

        // Mock event bus
        var mockEventBus = new Mock<IEventBus>();

        // Mock fingerprinter
        var mockFingerprinter = new Mock<IRecipeFingerprinter>();
        mockFingerprinter.Setup(f => f.GenerateFingerprint(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string url, string title, string desc) => $"hash-{url.GetHashCode()}");
        mockFingerprinter.Setup(f => f.IsDuplicateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Create saga with real repositories and services
        var saga = new RecipeProcessingSaga(
            Mock.Of<ILogger<RecipeProcessingSaga>>(),
            _sagaRepository!,
            mockConfigLoader.Object,
            mockFactory.Object,
            mockFingerprinter.Object,
            mockNormalizer.Object,
            mockRateLimiter.Object,
            mockEventBus.Object,
            mockHttpClient.Object,
            extractor,
            _recipeRepository!,
            _fingerprintRepository!
        );

        // Act - Run the complete saga
        Guid correlationId = await saga.StartProcessingAsync(
            "e2e-provider",
            10,
            TimeSpan.FromMinutes(5),
            CancellationToken.None);

        // Give the saga time to complete asynchronously
        await Task.Delay(TimeSpan.FromSeconds(2));

        // Assert - Verify the complete workflow
        correlationId.ShouldNotBe(Guid.Empty);

        // 1. Verify saga state
        SagaState? sagaState = await _sagaRepository.GetByCorrelationIdAsync(correlationId, CancellationToken.None);
        sagaState.ShouldNotBeNull();
        sagaState!.Status.ShouldBe(SagaStatus.Completed, "Saga should complete successfully");

        // 2. Verify fingerprint was created (mock will be called)
        mockFingerprinter.Verify(f => f.GenerateFingerprint(
                It.Is<string>(url => url.Contains("pasta-primavera")),
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.AtLeastOnce, "Fingerprint should be generated");

        // 3. Verify recipe was created by querying via URL
        var recipe = await _recipeRepository!.GetByUrlAsync(
            "https://www.example-recipes.com/recipes/pasta-primavera",
            "e2e-provider",
            CancellationToken.None);

        // 4. Validate the parsed recipe has all expected fields
        recipe.ShouldNotBeNull("Recipe should be created and persisted");
        recipe.Title.ShouldBe("Seared Steak with Roasted Potatoes", "Recipe title should match");
        recipe.Description.ShouldBe("Juicy seared steak paired with crispy roasted potatoes and fresh vegetables", "Recipe description should match");
        recipe.SourceUrl.ShouldBe("https://www.example-recipes.com/recipes/pasta-primavera", "Source URL should match");
        recipe.ProviderName.ShouldBe("e2e-provider", "Provider name should match");
        
        recipe.PrepTimeMinutes.ShouldBe(15, "Prep time should be parsed correctly");
        recipe.CookTimeMinutes.ShouldBe(25, "Cook time should be parsed correctly");
        recipe.Servings.ShouldBe(2, "Servings should be parsed correctly");
        
        recipe.Ingredients.ShouldNotBeNull("Ingredients should not be null");
        recipe.Ingredients.Count.ShouldBe(9, "Should have 9 ingredients");
        recipe.Ingredients.ShouldContain(i => i.Name.Contains("ribeye steaks"));
        recipe.Ingredients.ShouldContain(i => i.Name.Contains("baby potatoes"));
        
        recipe.Instructions.ShouldNotBeNull("Instructions should not be null");
        recipe.Instructions.Count.ShouldBe(6, "Should have 6 instructions");
        recipe.Instructions.ShouldContain(i => i.Description.Contains("Remove steaks from refrigerator"));
        recipe.Instructions.ShouldContain(i => i.Description.Contains("Let steaks rest for 5 minutes"));
        
        recipe.Cuisine.ShouldBe("American", "Cuisine should be parsed correctly");
        recipe.IsReadyForPublication.ShouldBeTrue("Recipe should be ready for publication");

        // 5. Verify discovery was called
        mockDiscoveryService.Verify(d => d.DiscoverRecipeUrlsAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once, "Discovery should be called once");

        // 6. Verify HTTP fetch was called
        mockHttpClient.Verify(h => h.GetStringAsync(
                It.Is<string>(url => url == "https://www.example-recipes.com/recipes/pasta-primavera"),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once, "HTTP client should fetch the recipe page once");

        // 7. Verify events were published
        mockEventBus.Verify(e => e.Publish(It.IsAny<IDomainEvent>()),
            Times.AtLeastOnce, "Events should be published during the workflow");
    }

    [Fact(DisplayName = "End-to-end: Multiple recipes processed successfully")]
    public async Task EndToEnd_MultipleRecipes_ProcessedSuccessfully()
    {
        // Arrange
        var config = new ProviderConfiguration(
            "multi-provider",
            enabled: true,
            endpoint: new EndpointInfo("https://www.example-recipes.com/recipes"),
            discovery: new DiscoveryConfig(DiscoveryStrategy.Static),
            batching: new BatchingConfig(5, 60),
            rateLimit: new RateLimitConfig(1.0, 30, 3, 30));

        var mockConfigLoader = new Mock<IProviderConfigurationLoader>();
        mockConfigLoader.Setup(c => c.GetByProviderIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        // Mock discovery service to return multiple URLs
        var mockDiscoveryService = new Mock<DomainInterfaces.IDiscoveryService>();
        var discoveredUrls = new List<DiscoveredUrl>
        {
            new("https://www.example-recipes.com/recipes/recipe1", "multi-provider", DateTime.UtcNow),
            new("https://www.example-recipes.com/recipes/recipe2", "multi-provider", DateTime.UtcNow),
            new("https://www.example-recipes.com/recipes/recipe3", "multi-provider", DateTime.UtcNow)
        };

        mockDiscoveryService.Setup(d => d.DiscoverRecipeUrlsAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(discoveredUrls);

        var mockFactory = new Mock<IDiscoveryServiceFactory>();
        mockFactory.Setup(f => f.CreateDiscoveryService(It.IsAny<DiscoveryStrategy>()))
            .Returns(mockDiscoveryService.Object);

        // Mock HTTP client to return different recipes for each URL
        var mockHttpClient = new Mock<DomainInterfaces.IStealthyHttpClient>();
        mockHttpClient.Setup(h => h.GetStringAsync(
                It.Is<string>(url => url.EndsWith("recipe1")),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateRecipeHtml("Recipe One", "First recipe"));

        mockHttpClient.Setup(h => h.GetStringAsync(
                It.Is<string>(url => url.EndsWith("recipe2")),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateRecipeHtml("Recipe Two", "Second recipe"));

        mockHttpClient.Setup(h => h.GetStringAsync(
                It.Is<string>(url => url.EndsWith("recipe3")),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateRecipeHtml("Recipe Three", "Third recipe"));

        var extractor = new RecipeExtractorService(Mock.Of<ILogger<RecipeExtractorService>>());
        var mockRateLimiter = new Mock<IRateLimiter>();
        mockRateLimiter.Setup(r => r.TryAcquireAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var mockNormalizer = new Mock<IIngredientNormalizer>();
        mockNormalizer.Setup(n => n.NormalizeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string providerId, string providerCode, CancellationToken _) => providerCode);

        var mockFingerprinter = new Mock<IRecipeFingerprinter>();
        mockFingerprinter.Setup(f => f.GenerateFingerprint(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string url, string title, string desc) => $"hash-{url.GetHashCode()}");
        mockFingerprinter.Setup(f => f.IsDuplicateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var saga = new RecipeProcessingSaga(
            Mock.Of<ILogger<RecipeProcessingSaga>>(),
            _sagaRepository!,
            mockConfigLoader.Object,
            mockFactory.Object,
            mockFingerprinter.Object,
            mockNormalizer.Object,
            mockRateLimiter.Object,
            Mock.Of<IEventBus>(),
            mockHttpClient.Object,
            extractor,
            _recipeRepository!,
            _fingerprintRepository!
        );

        // Act
        Guid correlationId = await saga.StartProcessingAsync(
            "multi-provider",
            10,
            TimeSpan.FromMinutes(5),
            CancellationToken.None);

        await Task.Delay(TimeSpan.FromSeconds(3));

        // Assert - Query recipes individually to verify they were created
        var recipe1 = await _recipeRepository!.GetByUrlAsync(
            "https://www.example-recipes.com/recipes/recipe1",
            "multi-provider",
            CancellationToken.None);
        
        var recipe2 = await _recipeRepository!.GetByUrlAsync(
            "https://www.example-recipes.com/recipes/recipe2",
            "multi-provider",
            CancellationToken.None);
        
        var recipe3 = await _recipeRepository!.GetByUrlAsync(
            "https://www.example-recipes.com/recipes/recipe3",
            "multi-provider",
            CancellationToken.None);

        recipe1.ShouldNotBeNull("Recipe 1 should be created");
        recipe2.ShouldNotBeNull("Recipe 2 should be created");
        recipe3.ShouldNotBeNull("Recipe 3 should be created");

        recipe1.Title.ShouldBe("Recipe One");
        recipe2.Title.ShouldBe("Recipe Two");
        recipe3.Title.ShouldBe("Recipe Three");

        // Verify all recipes are valid
        var recipes = new[] { recipe1, recipe2, recipe3 };
        foreach (var recipe in recipes)
        {
            recipe.Ingredients.ShouldNotBeEmpty("Each recipe should have ingredients");
            recipe.Instructions.ShouldNotBeEmpty("Each recipe should have instructions");
            recipe.IsReadyForPublication.ShouldBeTrue("Each recipe should be ready for publication");
        }
    }

    private static string CreateRecipeHtml(string title, string description)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <script type=""application/ld+json"">
    {{
        ""@context"": ""https://schema.org"",
        ""@type"": ""Recipe"",
        ""name"": ""{title}"",
        ""description"": ""{description}"",
        ""prepTime"": ""PT10M"",
        ""cookTime"": ""PT20M"",
        ""recipeYield"": ""2 servings"",
        ""recipeIngredient"": [
            ""Ingredient 1"",
            ""Ingredient 2""
        ],
        ""recipeInstructions"": [
            {{
                ""@type"": ""HowToStep"",
                ""text"": ""Step 1""
            }},
            {{
                ""@type"": ""HowToStep"",
                ""text"": ""Step 2""
            }}
        ]
    }}
    </script>
</head>
<body>
    <h1>{title}</h1>
</body>
</html>";
    }
}
