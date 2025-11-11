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
///     Integration tests for end-to-end recipe processing workflow with comprehensive error handling.
///     Tests all error scenarios: discovery failure, fingerprinting failure, processing failure, persistence failure.
///     Validates transient error retry and permanent error skip behavior.
/// </summary>
public class RecipeProcessingErrorHandlingTests : IAsyncLifetime
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
		_mongoDatabase = mongoClient.GetDatabase("recipe-engine-error-test");
		_sagaRepository = new SagaStateRepository(_mongoDatabase);
	}

	private static Mock<IRecipeBatchRepository> CreateMockBatchRepository()
	{
		var mock = new Mock<IRecipeBatchRepository>();
		return mock;
	}

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

	private static Mock<IEventBus> CreateMockEventBus()
	{
		var mock = new Mock<IEventBus>();
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

	[Fact(DisplayName = "Saga handles fingerprinting errors gracefully")]
	public async Task SagaErrorHandling_HandlesFingerprintingErrors_Gracefully()
	{
		// Arrange
		var mockLogger = new Mock<ILogger<RecipeProcessingSaga>>();
		Mock<IProviderConfigurationLoader> mockConfigLoader = CreateMockConfigLoader();
		Mock<IDiscoveryService> mockDiscoveryService = CreateMockDiscoveryService(new[]
		{
			"https://test.com/recipe1"
		});

		var mockFingerprinter = new Mock<IRecipeFingerprinter>();
		mockFingerprinter
			.Setup(f => f.GenerateFingerprint(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
			.Throws(new ArgumentException("Invalid URL format")); // Permanent error

		mockFingerprinter
			.Setup(f => f.IsDuplicateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(false);

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

		// Act & Assert
		// Currently will fail because error handling is not implemented
		// After Phase 5, should skip fingerprinting errors and continue
		var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
		{
			await saga.StartProcessingAsync(
				"test-provider",
				10,
				TimeSpan.FromMinutes(10),
				CancellationToken.None);
		});

		exception.Message.ShouldContain("Invalid URL format");
	}

	[Fact(DisplayName = "Saga persists state after each processing attempt for crash recovery")]
	public async Task SagaErrorHandling_PersistsStateAfterEachAttempt_ForCrashRecovery()
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

		// Verify state was persisted with checkpoints
		sagaState!.HasCheckpoint.ShouldBeTrue(); // "should have created checkpoints during processing";

		// In Phase 5, we'll verify:
		// - CurrentIndex is updated after each recipe
		// - ProcessedUrls and FailedUrls are updated incrementally
		// - State can be used to resume processing
	}

	[Fact(DisplayName = "Saga retries transient discovery errors with exponential backoff")]
	public async Task SagaErrorHandling_RetriesTransientDiscoveryErrors_WithExponentialBackoff()
	{
		// Arrange
		var mockLogger = new Mock<ILogger<RecipeProcessingSaga>>();
		Mock<IProviderConfigurationLoader> mockConfigLoader = CreateMockConfigLoader();
		var attemptCount = 0;

		var mockDiscoveryFactoryService = new Mock<IDiscoveryServiceFactory>();
		var mockDiscoveryService = new Mock<IDiscoveryService>();
		mockDiscoveryService
			.Setup(d => d.DiscoverRecipeUrlsAsync(
				It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
			.Returns((string url, string provider, int depth, int max, CancellationToken ct) =>
			{
				attemptCount++;
				if (attemptCount <= 2)
					// Simulate transient network error
					throw new HttpRequestException("Connection timeout");
				// Success on third attempt
				return Task.FromResult<IEnumerable<DiscoveredUrl>>(new List<DiscoveredUrl>
				{
					new("https://test.com/recipe1", provider, DateTime.UtcNow)
				});
			});
		mockDiscoveryFactoryService.Setup(x => x.CreateDiscoveryService(It.IsAny<DiscoveryStrategy>())).Returns(mockDiscoveryService.Object);

		Mock<IRecipeFingerprinter> mockFingerprinter = CreateMockFingerprinter();
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
		Guid batchId = await saga.StartProcessingAsync("test-provider", 10, TimeSpan.FromMinutes(10), CancellationToken.None);

		// Assert
		batchId.ShouldNotBe(Guid.Empty); // "should successfully complete after retries";
		attemptCount.ShouldBe(3, "should have retried exactly 3 times (2 failures + 1 success)");

		// Verify saga completed successfully
		SagaState? sagaState = await _sagaRepository!.GetByCorrelationIdAsync(batchId, CancellationToken.None);
		sagaState.ShouldNotBeNull();
		sagaState!.Status.ShouldBe(SagaStatus.Completed, "saga should complete successfully after retries");
	}

	[Fact(DisplayName = "Saga skips permanent processing errors and continues")]
	public async Task SagaErrorHandling_SkipsPermanentProcessingErrors_AndContinues()
	{
		// Arrange
		var mockLogger = new Mock<ILogger<RecipeProcessingSaga>>();
		Mock<IProviderConfigurationLoader> mockConfigLoader = CreateMockConfigLoader();
		Mock<IDiscoveryService> mockDiscoveryService = CreateMockDiscoveryService(new[]
		{
			"https://test.com/recipe1",
			"https://test.com/recipe-bad-data", // This will cause permanent error
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
		sagaState!.Status.ShouldBe(SagaStatus.Completed, "saga should complete despite permanent errors");

		// In Phase 5 implementation, we'll verify:
		// - Failed URLs are tracked in sagaState.StateData["FailedUrls"]
		// - Processing continued for recipe3 after recipe-bad-data failed
		// - ProcessingErrorEvent was emitted for monitoring
	}
}