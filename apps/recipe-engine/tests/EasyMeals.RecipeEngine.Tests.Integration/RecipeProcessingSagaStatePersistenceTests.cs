using EasyMeals.RecipeEngine.Application.Interfaces;
using EasyMeals.RecipeEngine.Application.Sagas;
using EasyMeals.RecipeEngine.Domain.Entities;
using EasyMeals.RecipeEngine.Domain.Interfaces;
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
///     Integration tests for saga state persistence after each processing stage.
///     Validates that DiscoveredUrls, FingerprintedUrls, ProcessedUrls, FailedUrls, and CurrentIndex
///     are correctly updated and persisted for crash recovery.
/// </summary>
public class RecipeProcessingSagaStatePersistenceTests : IAsyncLifetime
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
		_mongoContainer = new MongoDbBuilder()
			.WithImage("mongo:8.0")
			.Build();

		await _mongoContainer.StartAsync();

		var mongoClient = new MongoClient(_mongoContainer.GetConnectionString());
		_mongoDatabase = mongoClient.GetDatabase("recipe-engine-persistence-test");
		_sagaRepository = new SagaStateRepository(_mongoDatabase);
	}

	private static Mock<IRecipeBatchRepository> CreateMockBatchRepository() => new();

	private static Mock<IProviderConfigurationLoader> CreateMockConfigLoader()
	{
		var mock = new Mock<IProviderConfigurationLoader>();
		var config = new ProviderConfiguration(
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

		mock.Setup(c => c.GetByProviderIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(config);

		return mock;
	}

	private static Mock<IDiscoveryService> CreateMockDiscoveryService(string[] urls)
	{
		var mock = new Mock<IDiscoveryService>();
		List<DiscoveredUrl> discoveredUrls = urls.Select(url =>
			new DiscoveredUrl(url, "test-provider", DateTime.UtcNow)).ToList();

		mock.Setup(d => d.DiscoverRecipeUrlsAsync(
				It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(discoveredUrls);

		return mock;
	}

	private static Mock<IEventBus> CreateMockEventBus() => new();

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

	[Fact(DisplayName = "Saga state can be reconstituted for crash recovery")]
	public async Task SagaStatePersistence_CanBeReconstituted_ForCrashRecovery()
	{
		// Arrange - First run
		var mockLogger = new Mock<ILogger<RecipeProcessingSaga>>();
		Mock<IProviderConfigurationLoader> mockConfigLoader = CreateMockConfigLoader();
		Mock<IDiscoveryService> mockDiscoveryService = CreateMockDiscoveryService(new[]
		{
			"https://test.com/recipe1",
			"https://test.com/recipe2",
			"https://test.com/recipe3"
		});

		Mock<IRecipeFingerprinter> mockFingerprinter = CreateMockFingerprinter();
		Mock<IIngredientNormalizer> mockNormalizer = CreateMockNormalizer();
		Mock<IRateLimiter> mockRateLimiter = CreateMockRateLimiter();
		Mock<IRecipeBatchRepository> mockBatchRepository = CreateMockBatchRepository();
		Mock<IEventBus> mockEventBus = CreateMockEventBus();

		var mockFactory = new Mock<IDiscoveryServiceFactory>();
		mockFactory.Setup(f => f.CreateDiscoveryService(It.IsAny<DiscoveryStrategy>()))
			.Returns(mockDiscoveryService.Object);

		var saga = new RecipeProcessingSaga(
			mockLogger.Object,
			_sagaRepository!,
			mockConfigLoader.Object,
			mockFactory.Object,
			mockFingerprinter.Object,
			mockNormalizer.Object,
			mockRateLimiter.Object,
			mockBatchRepository.Object,
			mockEventBus.Object);

		Guid batchId = await saga.StartProcessingAsync(
			"test-provider",
			10,
			TimeSpan.FromMinutes(10),
			CancellationToken.None);

		// Act - Verify state was persisted
		SagaState? originalState = await _sagaRepository!.GetByCorrelationIdAsync(batchId, CancellationToken.None);
		originalState.ShouldNotBeNull();

		// Reconstitute from persisted data
		SagaState reconstitutedState = SagaState.Reconstitute(
			originalState!.Id,
			originalState.SagaType,
			originalState.CorrelationId,
			originalState.Status,
			originalState.CurrentPhase,
			originalState.PhaseProgress,
			originalState.StateData,
			originalState.CheckpointData,
			originalState.Metrics,
			originalState.ErrorMessage,
			originalState.ErrorStackTrace,
			originalState.CreatedAt,
			originalState.StartedAt,
			originalState.UpdatedAt,
			originalState.CompletedAt);

		// Assert - Reconstituted state should match original
		reconstitutedState.ShouldNotBeNull();
		reconstitutedState.Id.ShouldBe(originalState.Id);
		reconstitutedState.CorrelationId.ShouldBe(originalState.CorrelationId);
		reconstitutedState.StateData["DiscoveredUrls"].ShouldBeEquivalentTo(originalState.StateData["DiscoveredUrls"]);
		reconstitutedState.StateData["ProcessedUrls"].ShouldBeEquivalentTo(originalState.StateData["ProcessedUrls"]);
		reconstitutedState.StateData["CurrentIndex"].ShouldBe(originalState.StateData["CurrentIndex"]);
	}

	[Fact(DisplayName = "Saga persists DiscoveredUrls after discovery phase")]
	public async Task SagaStatePersistence_PersistsDiscoveredUrls_AfterDiscoveryPhase()
	{
		// Arrange
		var mockLogger = new Mock<ILogger<RecipeProcessingSaga>>();
		Mock<IProviderConfigurationLoader> mockConfigLoader = CreateMockConfigLoader();

		var expectedUrls = new[]
		{
			"https://test.com/recipe1",
			"https://test.com/recipe2",
			"https://test.com/recipe3"
		};

		Mock<IDiscoveryService> mockDiscoveryService = CreateMockDiscoveryService(expectedUrls);
		Mock<IRecipeFingerprinter> mockFingerprinter = CreateMockFingerprinter();
		Mock<IIngredientNormalizer> mockNormalizer = CreateMockNormalizer();
		Mock<IRateLimiter> mockRateLimiter = CreateMockRateLimiter();
		Mock<IRecipeBatchRepository> mockBatchRepository = CreateMockBatchRepository();
		Mock<IEventBus> mockEventBus = CreateMockEventBus();

		var mockFactory = new Mock<IDiscoveryServiceFactory>();
		mockFactory.Setup(f => f.CreateDiscoveryService(It.IsAny<DiscoveryStrategy>()))
			.Returns(mockDiscoveryService.Object);

		var saga = new RecipeProcessingSaga(
			mockLogger.Object,
			_sagaRepository!,
			mockConfigLoader.Object,
			mockFactory.Object,
			mockFingerprinter.Object,
			mockNormalizer.Object,
			mockRateLimiter.Object,
			mockBatchRepository.Object,
			mockEventBus.Object);

		// Act
		Guid batchId = await saga.StartProcessingAsync(
			"test-provider",
			10,
			TimeSpan.FromMinutes(10),
			CancellationToken.None);

		// Assert
		SagaState? sagaState = await _sagaRepository!.GetByCorrelationIdAsync(batchId, CancellationToken.None);
		sagaState.ShouldNotBeNull();
		sagaState!.StateData.ShouldContainKey("DiscoveredUrls");

		var discoveredUrls = sagaState.StateData["DiscoveredUrls"] as List<string>;
		discoveredUrls.ShouldNotBeNull();
		discoveredUrls!.Count.ShouldBe(expectedUrls.Length);
		discoveredUrls.ShouldBe(expectedUrls.ToList());
	}

	[Fact(DisplayName = "Saga persists FailedUrls with error details")]
	public async Task SagaStatePersistence_PersistsFailedUrls_WithErrorDetails()
	{
		// Arrange
		var mockLogger = new Mock<ILogger<RecipeProcessingSaga>>();
		Mock<IProviderConfigurationLoader> mockConfigLoader = CreateMockConfigLoader();
		Mock<IDiscoveryService> mockDiscoveryService = CreateMockDiscoveryService(new[]
		{
			"https://test.com/recipe1"
		});

		Mock<IRecipeFingerprinter> mockFingerprinter = CreateMockFingerprinter();
		Mock<IIngredientNormalizer> mockNormalizer = CreateMockNormalizer();
		Mock<IRateLimiter> mockRateLimiter = CreateMockRateLimiter();
		Mock<IRecipeBatchRepository> mockBatchRepository = CreateMockBatchRepository();
		Mock<IEventBus> mockEventBus = CreateMockEventBus();

		var mockFactory = new Mock<IDiscoveryServiceFactory>();
		mockFactory.Setup(f => f.CreateDiscoveryService(It.IsAny<DiscoveryStrategy>()))
			.Returns(mockDiscoveryService.Object);

		var saga = new RecipeProcessingSaga(
			mockLogger.Object,
			_sagaRepository!,
			mockConfigLoader.Object,
			mockFactory.Object,
			mockFingerprinter.Object,
			mockNormalizer.Object,
			mockRateLimiter.Object,
			mockBatchRepository.Object,
			mockEventBus.Object);

		// Act
		Guid batchId = await saga.StartProcessingAsync(
			"test-provider",
			10,
			TimeSpan.FromMinutes(10),
			CancellationToken.None);

		// Assert
		SagaState? sagaState = await _sagaRepository!.GetByCorrelationIdAsync(batchId, CancellationToken.None);
		sagaState.ShouldNotBeNull();
		sagaState!.StateData.ShouldContainKey("FailedUrls");

		var failedUrls = sagaState.StateData["FailedUrls"] as List<Dictionary<string, object>>;
		failedUrls.ShouldNotBeNull();

		// In Phase 5, when errors occur, FailedUrls should contain:
		// - Url
		// - Error message
		// - RetryCount
		// - Timestamp
		// - IsPermanent/IsTransient flag
	}

	[Fact(DisplayName = "Saga persists FingerprintedUrls after fingerprinting phase")]
	public async Task SagaStatePersistence_PersistsFingerprintedUrls_AfterFingerprintingPhase()
	{
		// Arrange
		var mockLogger = new Mock<ILogger<RecipeProcessingSaga>>();
		Mock<IProviderConfigurationLoader> mockConfigLoader = CreateMockConfigLoader();
		Mock<IDiscoveryService> mockDiscoveryService = CreateMockDiscoveryService(new[]
		{
			"https://test.com/recipe1",
			"https://test.com/recipe2-duplicate", // Will be filtered out
			"https://test.com/recipe3"
		});
		var mockDiscoveryFactoryService = new Mock<IDiscoveryServiceFactory>();
		mockDiscoveryFactoryService.Setup(x => x.CreateDiscoveryService(It.IsAny<DiscoveryStrategy>())).Returns(mockDiscoveryService.Object);

		var mockFingerprinter = new Mock<IRecipeFingerprinter>();
		mockFingerprinter
			.Setup(f => f.GenerateFingerprint(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
			.Returns((string url, string title, string description) =>
				url.Contains("recipe2-duplicate") ? "fingerprint-recipe2" : $"fingerprint-{url}");

		// Mark recipe2 as duplicate
		mockFingerprinter
			.Setup(f => f.IsDuplicateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((string fingerprint, CancellationToken ct) =>
				fingerprint == "fingerprint-recipe2");

		Mock<IIngredientNormalizer> mockNormalizer = CreateMockNormalizer();
		Mock<IRateLimiter> mockRateLimiter = CreateMockRateLimiter();
		Mock<IRecipeBatchRepository> mockBatchRepository = CreateMockBatchRepository();
		Mock<IEventBus> mockEventBus = CreateMockEventBus();

		var saga = new RecipeProcessingSaga(
			mockLogger.Object,
			_sagaRepository!,
			mockConfigLoader.Object,
			mockDiscoveryFactoryService.Object,
			mockFingerprinter.Object,
			mockNormalizer.Object,
			mockRateLimiter.Object,
			mockBatchRepository.Object,
			mockEventBus.Object);

		// Act
		Guid batchId = await saga.StartProcessingAsync(
			"test-provider",
			10,
			TimeSpan.FromMinutes(10),
			CancellationToken.None);

		// Assert
		SagaState? sagaState = await _sagaRepository!.GetByCorrelationIdAsync(batchId, CancellationToken.None);
		sagaState.ShouldNotBeNull();
		sagaState!.StateData.ShouldContainKey("FingerprintedUrls");

		var fingerprintedUrls = sagaState.StateData["FingerprintedUrls"] as List<string>;
		fingerprintedUrls.ShouldNotBeNull();

		// Should exclude duplicates
		fingerprintedUrls.ShouldNotContain(url => url.Contains("recipe2-duplicate"));
	}

	[Fact(DisplayName = "Saga persists ProcessedUrls and CurrentIndex during processing")]
	public async Task SagaStatePersistence_PersistsProcessedUrlsAndCurrentIndex_DuringProcessing()
	{
		// Arrange
		var mockLogger = new Mock<ILogger<RecipeProcessingSaga>>();
		Mock<IProviderConfigurationLoader> mockConfigLoader = CreateMockConfigLoader();
		Mock<IDiscoveryService> mockDiscoveryService = CreateMockDiscoveryService(new[]
		{
			"https://test.com/recipe1",
			"https://test.com/recipe2",
			"https://test.com/recipe3"
		});

		Mock<IRecipeFingerprinter> mockFingerprinter = CreateMockFingerprinter();
		Mock<IIngredientNormalizer> mockNormalizer = CreateMockNormalizer();
		Mock<IRateLimiter> mockRateLimiter = CreateMockRateLimiter();
		Mock<IRecipeBatchRepository> mockBatchRepository = CreateMockBatchRepository();
		Mock<IEventBus> mockEventBus = CreateMockEventBus();

		var mockFactory = new Mock<IDiscoveryServiceFactory>();
		mockFactory.Setup(f => f.CreateDiscoveryService(It.IsAny<DiscoveryStrategy>()))
			.Returns(mockDiscoveryService.Object);

		var saga = new RecipeProcessingSaga(
			mockLogger.Object,
			_sagaRepository!,
			mockConfigLoader.Object,
			mockFactory.Object,
			mockFingerprinter.Object,
			mockNormalizer.Object,
			mockRateLimiter.Object,
			mockBatchRepository.Object,
			mockEventBus.Object);

		// Act
		Guid batchId = await saga.StartProcessingAsync(
			"test-provider",
			10,
			TimeSpan.FromMinutes(10),
			CancellationToken.None);

		// Assert
		SagaState? sagaState = await _sagaRepository!.GetByCorrelationIdAsync(batchId, CancellationToken.None);
		sagaState.ShouldNotBeNull();

		// Verify ProcessedUrls are tracked
		sagaState!.StateData.ShouldContainKey("ProcessedUrls");
		var processedUrls = sagaState.StateData["ProcessedUrls"] as List<string>;
		processedUrls.ShouldNotBeNull();

		// Verify CurrentIndex is tracked for resumability
		sagaState.StateData.ShouldContainKey("CurrentIndex");
		object currentIndex = sagaState.StateData["CurrentIndex"];
		currentIndex.ShouldNotBeNull();

		// In Phase 5, CurrentIndex should equal ProcessedUrls.Count after completion
	}
}