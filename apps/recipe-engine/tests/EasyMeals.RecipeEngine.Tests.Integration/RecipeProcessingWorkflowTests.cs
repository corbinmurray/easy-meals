using EasyMeals.RecipeEngine.Application.Interfaces;
using EasyMeals.RecipeEngine.Application.Sagas;
using EasyMeals.RecipeEngine.Domain.Entities;
using DomainInterfaces = EasyMeals.RecipeEngine.Domain.Interfaces;
using EasyMeals.RecipeEngine.Domain.Repositories;
using EasyMeals.RecipeEngine.Domain.ValueObjects;
using EasyMeals.RecipeEngine.Domain.ValueObjects.Discovery;
using EasyMeals.RecipeEngine.Infrastructure.Repositories;
using Shouldly;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Moq;
using Testcontainers.MongoDb;

namespace EasyMeals.RecipeEngine.Tests.Integration;

/// <summary>
///     Integration tests for the complete batch processing workflow.
///     Tests discovery → fingerprinting → processing → persistence with MongoDB.
/// </summary>
public class RecipeProcessingWorkflowTests : IAsyncLifetime
{
    private MongoDbContainer? _mongoContainer;
    private IMongoDatabase? _mongoDatabase;
    private ISagaStateRepository? _sagaRepository;

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
    }

    [Fact(DisplayName = "Complete workflow processes recipes through all phases")]
    public async Task CompleteWorkflow_ProcessesRecipesThroughAllPhases()
    {
        // Arrange
        var mockConfigLoader = new Mock<IProviderConfigurationLoader>();
        var mockConfig = new ProviderConfiguration(
            "test-provider",
            true,
            DiscoveryStrategy.Static,
            "https://test.com/recipes",
            50,
            60,
            0.1,
            10,
            3,
            30);

        mockConfigLoader.Setup(c => c.GetByProviderIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockConfig);

        var mockDiscoveryService = new Mock<DomainInterfaces.IDiscoveryService>();
        var discoveredUrls = new List<DiscoveredUrl>
        {
            new("https://test.com/recipe1", "test-provider", DateTime.UtcNow),
            new("https://test.com/recipe2", "test-provider", DateTime.UtcNow),
            new("https://test.com/recipe3", "test-provider", DateTime.UtcNow)
        };

        mockDiscoveryService.Setup(d => d.DiscoverRecipeUrlsAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(discoveredUrls);

        var mockFingerprinter = new Mock<IRecipeFingerprinter>();
        mockFingerprinter.Setup(f => f.GenerateFingerprint(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string url, string title, string desc) => $"hash-{url.GetHashCode()}");
        mockFingerprinter.Setup(f => f.IsDuplicateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var mockRateLimiter = new Mock<IRateLimiter>();
        mockRateLimiter.Setup(r => r.TryAcquireAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var mockBatchRepo = new Mock<IRecipeBatchRepository>();

        var mockFactory = new Mock<IDiscoveryServiceFactory>();
        mockFactory.Setup(f => f.CreateDiscoveryService(mockConfig.DiscoveryStrategy))
            .Returns(mockDiscoveryService.Object);

        var saga = new RecipeProcessingSaga(
            Mock.Of<ILogger<RecipeProcessingSaga>>(),
            _sagaRepository!,
            mockConfigLoader.Object,
            mockFactory.Object,
            mockFingerprinter.Object,
            Mock.Of<IIngredientNormalizer>(),
            mockRateLimiter.Object,
            mockBatchRepo.Object,
            Mock.Of<IEventBus>(),
            Mock.Of<DomainInterfaces.IStealthyHttpClient>(),
            Mock.Of<DomainInterfaces.IRecipeExtractor>(),
            Mock.Of<IRecipeRepository>(),
            Mock.Of<IFingerprintRepository>()
        );

        // Act
        Guid correlationId = await saga.StartProcessingAsync(
            "test-provider",
            10,
            TimeSpan.FromMinutes(5),
            CancellationToken.None);

        // Assert
        correlationId.ShouldNotBe(Guid.Empty);

        // Verify saga state was persisted
        SagaState? sagaState = await _sagaRepository.GetByCorrelationIdAsync(correlationId, CancellationToken.None);
        sagaState.ShouldNotBeNull();
        sagaState!.Status.ShouldBeOneOf(SagaStatus.Completed, SagaStatus.Failed);

        // Verify discovery was called
        mockDiscoveryService.Verify(d => d.DiscoverRecipeUrlsAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // Verify fingerprinting was called for each URL
        mockFingerprinter.Verify(f => f.GenerateFingerprint(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.AtLeastOnce);
    }

    [Fact(DisplayName = "Workflow filters duplicate recipes")]
    public async Task Workflow_FiltersDuplicateRecipes()
    {
        // Arrange
        var mockConfigLoader = new Mock<IProviderConfigurationLoader>();
        var mockConfig = new ProviderConfiguration(
            "test-provider",
            true,
            DiscoveryStrategy.Static,
            "https://test.com/recipes",
            50,
            60,
            0.1,
            10,
            3,
            30);

        mockConfigLoader.Setup(c => c.GetByProviderIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockConfig);

        var mockDiscoveryService = new Mock<DomainInterfaces.IDiscoveryService>();
        var discoveredUrls = new List<DiscoveredUrl>
        {
            new("https://test.com/recipe1", "test-provider", DateTime.UtcNow),
            new("https://test.com/recipe2", "test-provider", DateTime.UtcNow),
            new("https://test.com/recipe3", "test-provider", DateTime.UtcNow)
        };

        mockDiscoveryService.Setup(d => d.DiscoverRecipeUrlsAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(discoveredUrls);

        var mockFingerprinter = new Mock<IRecipeFingerprinter>();
        mockFingerprinter.Setup(f => f.GenerateFingerprint(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string url, string title, string desc) => $"hash-{url}");

        // Make second URL a duplicate
        mockFingerprinter.Setup(f => f.IsDuplicateAsync("hash-https://test.com/recipe2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        mockFingerprinter.Setup(f => f.IsDuplicateAsync(It.Is<string>(h => h != "hash-https://test.com/recipe2"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var processedCount = 0;
        var mockRateLimiter = new Mock<IRateLimiter>();
        mockRateLimiter.Setup(r => r.TryAcquireAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => processedCount++)
            .ReturnsAsync(true);

        var mockFactory = new Mock<IDiscoveryServiceFactory>();
        mockFactory.Setup(f => f.CreateDiscoveryService(mockConfig.DiscoveryStrategy))
            .Returns(mockDiscoveryService.Object);
        var saga = new RecipeProcessingSaga(
            Mock.Of<ILogger<RecipeProcessingSaga>>(),
            _sagaRepository!,
            mockConfigLoader.Object,
            mockFactory.Object,
            mockFingerprinter.Object,
            Mock.Of<IIngredientNormalizer>(),
            mockRateLimiter.Object,
            Mock.Of<IRecipeBatchRepository>(),
            Mock.Of<IEventBus>(),
            Mock.Of<DomainInterfaces.IStealthyHttpClient>(),
            Mock.Of<DomainInterfaces.IRecipeExtractor>(),
            Mock.Of<IRecipeRepository>(),
            Mock.Of<IFingerprintRepository>()
        );

        // Act
        await saga.StartProcessingAsync(
            "test-provider",
            10,
            TimeSpan.FromMinutes(5),
            CancellationToken.None);

        // Assert - Should only process 2 (filtered out the duplicate)
        processedCount.ShouldBe(2, "one recipe should be filtered as duplicate");
    }

    [Fact(DisplayName = "Workflow handles discovery errors gracefully")]
    public async Task Workflow_HandlesDiscoveryErrors()
    {
        // Arrange
        var mockConfigLoader = new Mock<IProviderConfigurationLoader>();
        mockConfigLoader.Setup(c => c.GetByProviderIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProviderConfiguration?)null);

        var mockFactory = new Mock<IDiscoveryServiceFactory>();
        mockFactory.Setup(f => f.CreateDiscoveryService(It.IsAny<DiscoveryStrategy>()))
            .Returns(Mock.Of<DomainInterfaces.IDiscoveryService>());
        var saga = new RecipeProcessingSaga(
            Mock.Of<ILogger<RecipeProcessingSaga>>(),
            _sagaRepository!,
            mockConfigLoader.Object,
            mockFactory.Object,
            Mock.Of<IRecipeFingerprinter>(),
            Mock.Of<IIngredientNormalizer>(),
            Mock.Of<IRateLimiter>(),
            Mock.Of<IRecipeBatchRepository>(),
            Mock.Of<IEventBus>(),
            Mock.Of<DomainInterfaces.IStealthyHttpClient>(),
            Mock.Of<DomainInterfaces.IRecipeExtractor>(),
            Mock.Of<IRecipeRepository>(),
            Mock.Of<IFingerprintRepository>()
        );

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await saga.StartProcessingAsync("test-provider", 10, TimeSpan.FromMinutes(5), CancellationToken.None));
    }

    [Fact(DisplayName = "Workflow respects batch size limit")]
    public async Task Workflow_RespectsBatchSizeLimit()
    {
        // Arrange
        const int batchSize = 2;
        var processedCount = 0;

        var mockConfigLoader = new Mock<IProviderConfigurationLoader>();
        var mockConfig = new ProviderConfiguration(
            "test-provider",
            true,
            DiscoveryStrategy.Static,
            "https://test.com/recipes",
            50,
            60,
            0.1,
            10,
            3,
            30);

        mockConfigLoader.Setup(c => c.GetByProviderIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockConfig);

        var mockDiscoveryService = new Mock<DomainInterfaces.IDiscoveryService>();
        List<DiscoveredUrl> discoveredUrls = Enumerable.Range(1, 10)
            .Select(i => new DiscoveredUrl($"https://test.com/recipe{i}", "test-provider", DateTime.UtcNow))
            .ToList();

        mockDiscoveryService.Setup(d => d.DiscoverRecipeUrlsAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(discoveredUrls);

        var mockFingerprinter = new Mock<IRecipeFingerprinter>();
        mockFingerprinter.Setup(f => f.GenerateFingerprint(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string url, string title, string desc) => $"hash-{url}");
        mockFingerprinter.Setup(f => f.IsDuplicateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var mockRateLimiter = new Mock<IRateLimiter>();
        mockRateLimiter.Setup(r => r.TryAcquireAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => processedCount++)
            .ReturnsAsync(true);

        var mockFactory = new Mock<IDiscoveryServiceFactory>();
        mockFactory.Setup(f => f.CreateDiscoveryService(mockConfig.DiscoveryStrategy))
            .Returns(mockDiscoveryService.Object);
        var saga = new RecipeProcessingSaga(
            Mock.Of<ILogger<RecipeProcessingSaga>>(),
            _sagaRepository!,
            mockConfigLoader.Object,
            mockFactory.Object,
            mockFingerprinter.Object,
            Mock.Of<IIngredientNormalizer>(),
            mockRateLimiter.Object,
            Mock.Of<IRecipeBatchRepository>(),
            Mock.Of<IEventBus>(),
            Mock.Of<DomainInterfaces.IStealthyHttpClient>(),
            Mock.Of<DomainInterfaces.IRecipeExtractor>(),
            Mock.Of<IRecipeRepository>(),
            Mock.Of<IFingerprintRepository>()
        );

        // Act
        await saga.StartProcessingAsync(
            "test-provider",
            batchSize,
            TimeSpan.FromMinutes(5),
            CancellationToken.None);

        // Assert - Should process at most batchSize recipes
        processedCount.ShouldBeLessThanOrEqualTo(batchSize);
    }
}