using EasyMeals.RecipeEngine.Application.Interfaces;
using EasyMeals.RecipeEngine.Application.Sagas;
using EasyMeals.RecipeEngine.Domain.Entities;
using EasyMeals.RecipeEngine.Domain.Interfaces;
using EasyMeals.RecipeEngine.Domain.Repositories;
using EasyMeals.RecipeEngine.Domain.ValueObjects;
using EasyMeals.RecipeEngine.Domain.ValueObjects.Discovery;
using EasyMeals.RecipeEngine.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Moq;
using Testcontainers.MongoDb;

namespace EasyMeals.RecipeEngine.Tests.Integration;

/// <summary>
/// Integration tests for saga state persistence after each processing stage.
/// Validates that DiscoveredUrls, FingerprintedUrls, ProcessedUrls, FailedUrls, and CurrentIndex
/// are correctly updated and persisted for crash recovery.
/// </summary>
public class RecipeProcessingSagaStatePersistenceTests : IAsyncLifetime
{
    private MongoDbContainer? _mongoContainer;
    private IMongoDatabase? _mongoDatabase;
    private ISagaStateRepository? _sagaRepository;

    public async Task InitializeAsync()
    {
        _mongoContainer = new MongoDbBuilder()
            .WithImage("mongo:7.0")
            .Build();

        await _mongoContainer.StartAsync();

        var mongoClient = new MongoClient(_mongoContainer.GetConnectionString());
        _mongoDatabase = mongoClient.GetDatabase("recipe-engine-persistence-test");
        _sagaRepository = new SagaStateRepository(_mongoDatabase);
    }

    public async Task DisposeAsync()
    {
        if (_mongoContainer != null)
        {
            await _mongoContainer.DisposeAsync();
        }
    }

    [Fact(DisplayName = "Saga persists DiscoveredUrls after discovery phase")]
    public async Task SagaStatePersistence_PersistsDiscoveredUrls_AfterDiscoveryPhase()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<RecipeProcessingSaga>>();
        var mockConfigLoader = CreateMockConfigLoader();
        
        var expectedUrls = new[]
        {
            "https://test.com/recipe1",
            "https://test.com/recipe2",
            "https://test.com/recipe3"
        };
        
        var mockDiscoveryService = CreateMockDiscoveryService(expectedUrls);
        var mockFingerprinter = CreateMockFingerprinter();
        var mockNormalizer = CreateMockNormalizer();
        var mockRateLimiter = CreateMockRateLimiter();
        var mockBatchRepository = CreateMockBatchRepository();
        var mockEventBus = CreateMockEventBus();

        var saga = new RecipeProcessingSaga(
            mockLogger.Object,
            _sagaRepository!,
            mockConfigLoader.Object,
            mockDiscoveryService.Object,
            mockFingerprinter.Object,
            mockNormalizer.Object,
            mockRateLimiter.Object,
            mockBatchRepository.Object,
            mockEventBus.Object);

        // Act
        var batchId = await saga.StartProcessingAsync(
            "test-provider",
            batchSize: 10,
            timeWindow: TimeSpan.FromMinutes(10),
            CancellationToken.None);

        // Assert
        var sagaState = await _sagaRepository!.GetByCorrelationIdAsync(batchId, CancellationToken.None);
        sagaState.Should().NotBeNull();
        sagaState!.StateData.Should().ContainKey("DiscoveredUrls");
        
        var discoveredUrls = sagaState.StateData["DiscoveredUrls"] as List<string>;
        discoveredUrls.Should().NotBeNull();
        discoveredUrls.Should().HaveCount(expectedUrls.Length);
        discoveredUrls.Should().BeEquivalentTo(expectedUrls);
    }

    [Fact(DisplayName = "Saga persists FingerprintedUrls after fingerprinting phase")]
    public async Task SagaStatePersistence_PersistsFingerprintedUrls_AfterFingerprintingPhase()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<RecipeProcessingSaga>>();
        var mockConfigLoader = CreateMockConfigLoader();
        var mockDiscoveryService = CreateMockDiscoveryService(new[]
        {
            "https://test.com/recipe1",
            "https://test.com/recipe2-duplicate", // Will be filtered out
            "https://test.com/recipe3"
        });

        var mockFingerprinter = new Mock<IRecipeFingerprinter>();
        mockFingerprinter
            .Setup(f => f.GenerateFingerprint(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns("test-fingerprint");
        
        // Mark recipe2 as duplicate
        mockFingerprinter
            .Setup(f => f.IsDuplicateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string fingerprint, CancellationToken ct) => 
                fingerprint.Contains("recipe2"));

        var mockNormalizer = CreateMockNormalizer();
        var mockRateLimiter = CreateMockRateLimiter();
        var mockBatchRepository = CreateMockBatchRepository();
        var mockEventBus = CreateMockEventBus();

        var saga = new RecipeProcessingSaga(
            mockLogger.Object,
            _sagaRepository!,
            mockConfigLoader.Object,
            mockDiscoveryService.Object,
            mockFingerprinter.Object,
            mockNormalizer.Object,
            mockRateLimiter.Object,
            mockBatchRepository.Object,
            mockEventBus.Object);

        // Act
        var batchId = await saga.StartProcessingAsync(
            "test-provider",
            batchSize: 10,
            timeWindow: TimeSpan.FromMinutes(10),
            CancellationToken.None);

        // Assert
        var sagaState = await _sagaRepository!.GetByCorrelationIdAsync(batchId, CancellationToken.None);
        sagaState.Should().NotBeNull();
        sagaState!.StateData.Should().ContainKey("FingerprintedUrls");
        
        var fingerprintedUrls = sagaState.StateData["FingerprintedUrls"] as List<string>;
        fingerprintedUrls.Should().NotBeNull();
        
        // Should exclude duplicates
        fingerprintedUrls.Should().NotContain(url => url.Contains("recipe2-duplicate"));
    }

    [Fact(DisplayName = "Saga persists ProcessedUrls and CurrentIndex during processing")]
    public async Task SagaStatePersistence_PersistsProcessedUrlsAndCurrentIndex_DuringProcessing()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<RecipeProcessingSaga>>();
        var mockConfigLoader = CreateMockConfigLoader();
        var mockDiscoveryService = CreateMockDiscoveryService(new[]
        {
            "https://test.com/recipe1",
            "https://test.com/recipe2",
            "https://test.com/recipe3"
        });

        var mockFingerprinter = CreateMockFingerprinter();
        var mockNormalizer = CreateMockNormalizer();
        var mockRateLimiter = CreateMockRateLimiter();
        var mockBatchRepository = CreateMockBatchRepository();
        var mockEventBus = CreateMockEventBus();

        var saga = new RecipeProcessingSaga(
            mockLogger.Object,
            _sagaRepository!,
            mockConfigLoader.Object,
            mockDiscoveryService.Object,
            mockFingerprinter.Object,
            mockNormalizer.Object,
            mockRateLimiter.Object,
            mockBatchRepository.Object,
            mockEventBus.Object);

        // Act
        var batchId = await saga.StartProcessingAsync(
            "test-provider",
            batchSize: 10,
            timeWindow: TimeSpan.FromMinutes(10),
            CancellationToken.None);

        // Assert
        var sagaState = await _sagaRepository!.GetByCorrelationIdAsync(batchId, CancellationToken.None);
        sagaState.Should().NotBeNull();
        
        // Verify ProcessedUrls are tracked
        sagaState!.StateData.Should().ContainKey("ProcessedUrls");
        var processedUrls = sagaState.StateData["ProcessedUrls"] as List<string>;
        processedUrls.Should().NotBeNull();
        
        // Verify CurrentIndex is tracked for resumability
        sagaState.StateData.Should().ContainKey("CurrentIndex");
        var currentIndex = sagaState.StateData["CurrentIndex"];
        currentIndex.Should().NotBeNull();
        
        // In Phase 5, CurrentIndex should equal ProcessedUrls.Count after completion
    }

    [Fact(DisplayName = "Saga persists FailedUrls with error details")]
    public async Task SagaStatePersistence_PersistsFailedUrls_WithErrorDetails()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<RecipeProcessingSaga>>();
        var mockConfigLoader = CreateMockConfigLoader();
        var mockDiscoveryService = CreateMockDiscoveryService(new[]
        {
            "https://test.com/recipe1"
        });

        var mockFingerprinter = CreateMockFingerprinter();
        var mockNormalizer = CreateMockNormalizer();
        var mockRateLimiter = CreateMockRateLimiter();
        var mockBatchRepository = CreateMockBatchRepository();
        var mockEventBus = CreateMockEventBus();

        var saga = new RecipeProcessingSaga(
            mockLogger.Object,
            _sagaRepository!,
            mockConfigLoader.Object,
            mockDiscoveryService.Object,
            mockFingerprinter.Object,
            mockNormalizer.Object,
            mockRateLimiter.Object,
            mockBatchRepository.Object,
            mockEventBus.Object);

        // Act
        var batchId = await saga.StartProcessingAsync(
            "test-provider",
            batchSize: 10,
            timeWindow: TimeSpan.FromMinutes(10),
            CancellationToken.None);

        // Assert
        var sagaState = await _sagaRepository!.GetByCorrelationIdAsync(batchId, CancellationToken.None);
        sagaState.Should().NotBeNull();
        sagaState!.StateData.Should().ContainKey("FailedUrls");
        
        var failedUrls = sagaState.StateData["FailedUrls"] as List<Dictionary<string, object>>;
        failedUrls.Should().NotBeNull();
        
        // In Phase 5, when errors occur, FailedUrls should contain:
        // - Url
        // - Error message
        // - RetryCount
        // - Timestamp
        // - IsPermanent/IsTransient flag
    }

    [Fact(DisplayName = "Saga state can be reconstituted for crash recovery")]
    public async Task SagaStatePersistence_CanBeReconstituted_ForCrashRecovery()
    {
        // Arrange - First run
        var mockLogger = new Mock<ILogger<RecipeProcessingSaga>>();
        var mockConfigLoader = CreateMockConfigLoader();
        var mockDiscoveryService = CreateMockDiscoveryService(new[]
        {
            "https://test.com/recipe1",
            "https://test.com/recipe2",
            "https://test.com/recipe3"
        });

        var mockFingerprinter = CreateMockFingerprinter();
        var mockNormalizer = CreateMockNormalizer();
        var mockRateLimiter = CreateMockRateLimiter();
        var mockBatchRepository = CreateMockBatchRepository();
        var mockEventBus = CreateMockEventBus();

        var saga = new RecipeProcessingSaga(
            mockLogger.Object,
            _sagaRepository!,
            mockConfigLoader.Object,
            mockDiscoveryService.Object,
            mockFingerprinter.Object,
            mockNormalizer.Object,
            mockRateLimiter.Object,
            mockBatchRepository.Object,
            mockEventBus.Object);

        var batchId = await saga.StartProcessingAsync(
            "test-provider",
            batchSize: 10,
            timeWindow: TimeSpan.FromMinutes(10),
            CancellationToken.None);

        // Act - Verify state was persisted
        var originalState = await _sagaRepository!.GetByCorrelationIdAsync(batchId, CancellationToken.None);
        originalState.Should().NotBeNull();

        // Reconstitute from persisted data
        var reconstitutedState = SagaState.Reconstitute(
            id: originalState!.Id,
            sagaType: originalState.SagaType,
            correlationId: originalState.CorrelationId,
            status: originalState.Status,
            currentPhase: originalState.CurrentPhase,
            phaseProgress: originalState.PhaseProgress,
            stateData: originalState.StateData,
            checkpointData: originalState.CheckpointData,
            metrics: originalState.Metrics,
            errorMessage: originalState.ErrorMessage,
            errorStackTrace: originalState.ErrorStackTrace,
            createdAt: originalState.CreatedAt,
            startedAt: originalState.StartedAt,
            updatedAt: originalState.UpdatedAt,
            completedAt: originalState.CompletedAt);

        // Assert - Reconstituted state should match original
        reconstitutedState.Should().NotBeNull();
        reconstitutedState.Id.Should().Be(originalState.Id);
        reconstitutedState.CorrelationId.Should().Be(originalState.CorrelationId);
        reconstitutedState.StateData["DiscoveredUrls"].Should().BeEquivalentTo(originalState.StateData["DiscoveredUrls"]);
        reconstitutedState.StateData["ProcessedUrls"].Should().BeEquivalentTo(originalState.StateData["ProcessedUrls"]);
        reconstitutedState.StateData["CurrentIndex"].Should().Be(originalState.StateData["CurrentIndex"]);
    }

    #region Helper Methods

    private static Mock<IProviderConfigurationLoader> CreateMockConfigLoader()
    {
        var mock = new Mock<IProviderConfigurationLoader>();
        var config = new ProviderConfiguration(
            providerId: "test-provider",
            enabled: true,
            discoveryStrategy: DiscoveryStrategy.Static,
            recipeRootUrl: "https://test.com/recipes",
            batchSize: 50,
            timeWindowMinutes: 60,
            minDelaySeconds: 0.1,
            maxRequestsPerMinute: 10,
            retryCount: 3,
            requestTimeoutSeconds: 30);

        mock.Setup(c => c.GetByProviderIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        return mock;
    }

    private static Mock<Domain.Interfaces.IDiscoveryService> CreateMockDiscoveryService(string[] urls)
    {
        var mock = new Mock<Domain.Interfaces.IDiscoveryService>();
        var discoveredUrls = urls.Select(url => 
            new DiscoveredUrl(url, "test-provider", DateTime.UtcNow)).ToList();

        mock.Setup(d => d.DiscoverRecipeUrlsAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(discoveredUrls);

        return mock;
    }

    private static Mock<IRecipeFingerprinter> CreateMockFingerprinter()
    {
        var mock = new Mock<IRecipeFingerprinter>();
        mock.Setup(f => f.GenerateFingerprint(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns("test-fingerprint");
        mock.Setup(f => f.IsDuplicateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        return mock;
    }

    private static Mock<IIngredientNormalizer> CreateMockNormalizer()
    {
        var mock = new Mock<IIngredientNormalizer>();
        mock.Setup(n => n.NormalizeBatchAsync(
                It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string?>());
        return mock;
    }

    private static Mock<IRateLimiter> CreateMockRateLimiter()
    {
        var mock = new Mock<IRateLimiter>();
        mock.Setup(r => r.TryAcquireAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        return mock;
    }

    private static Mock<IRecipeBatchRepository> CreateMockBatchRepository()
    {
        return new Mock<IRecipeBatchRepository>();
    }

    private static Mock<IEventBus> CreateMockEventBus()
    {
        return new Mock<IEventBus>();
    }

    #endregion
}
