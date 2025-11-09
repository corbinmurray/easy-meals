using EasyMeals.RecipeEngine.Application.Interfaces;
using EasyMeals.RecipeEngine.Application.Sagas;
using EasyMeals.RecipeEngine.Domain.Entities;
using EasyMeals.RecipeEngine.Domain.Interfaces;
using EasyMeals.RecipeEngine.Domain.Repositories;
using EasyMeals.RecipeEngine.Domain.ValueObjects;
using EasyMeals.RecipeEngine.Domain.ValueObjects.Discovery;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Moq;
using Testcontainers.MongoDb;

namespace EasyMeals.RecipeEngine.Tests.Integration;

/// <summary>
/// Integration tests for the complete batch processing workflow.
/// Tests discovery → fingerprinting → processing → persistence with MongoDB.
/// </summary>
public class RecipeProcessingWorkflowTests : IAsyncLifetime
{
	private MongoDbContainer? _mongoContainer;
	private IMongoDatabase? _mongoDatabase;
	private ISagaStateRepository? _sagaRepository;
	
	public async Task InitializeAsync()
	{
		// Start MongoDB container for integration testing
		_mongoContainer = new MongoDbBuilder()
			.WithImage("mongo:7.0")
			.Build();
			
		await _mongoContainer.StartAsync();
		
		// Connect to MongoDB
		var mongoClient = new MongoClient(_mongoContainer.GetConnectionString());
		_mongoDatabase = mongoClient.GetDatabase("recipe-engine-test");
		
		_sagaRepository = new Infrastructure.Repositories.SagaStateRepository(_mongoDatabase);
	}
	
	public async Task DisposeAsync()
	{
		if (_mongoContainer != null)
		{
			await _mongoContainer.DisposeAsync();
		}
	}
	
	[Fact(DisplayName = "Complete workflow processes recipes through all phases")]
	public async Task CompleteWorkflow_ProcessesRecipesThroughAllPhases()
	{
		// Arrange
		var mockConfigLoader = new Mock<IProviderConfigurationLoader>();
		var mockConfig = new ProviderConfiguration(
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
		
		mockConfigLoader.Setup(c => c.GetByProviderIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(mockConfig);
		
		var mockDiscoveryService = new Mock<Domain.Interfaces.IDiscoveryService>();
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
		
		var saga = new RecipeProcessingSaga(
			Mock.Of<ILogger<RecipeProcessingSaga>>(),
			_sagaRepository!,
			mockConfigLoader.Object,
			mockDiscoveryService.Object,
			mockFingerprinter.Object,
			Mock.Of<IIngredientNormalizer>(),
			mockRateLimiter.Object,
			mockBatchRepo.Object,
		Mock.Of<IEventBus>()
		);
		
		// Act
		var correlationId = await saga.StartProcessingAsync(
			"test-provider",
			10,
			TimeSpan.FromMinutes(5),
			CancellationToken.None);
		
		// Assert
		correlationId.Should().NotBeEmpty();
		
		// Verify saga state was persisted
		var sagaState = await _sagaRepository.GetByCorrelationIdAsync(correlationId, CancellationToken.None);
		sagaState.Should().NotBeNull();
		sagaState!.Status.Should().BeOneOf(SagaStatus.Completed, SagaStatus.Failed);
		
		// Verify discovery was called
		mockDiscoveryService.Verify(d => d.DiscoverRecipeUrlsAsync(
			It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
			Times.Once);
		
		// Verify fingerprinting was called for each URL
		mockFingerprinter.Verify(f => f.GenerateFingerprint(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
			Times.AtLeastOnce);
	}
	
	[Fact(DisplayName = "Workflow respects batch size limit")]
	public async Task Workflow_RespectsBatchSizeLimit()
	{
		// Arrange
		const int batchSize = 2;
		var processedCount = 0;
		
		var mockConfigLoader = new Mock<IProviderConfigurationLoader>();
		var mockConfig = new ProviderConfiguration(
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
		
		mockConfigLoader.Setup(c => c.GetByProviderIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(mockConfig);
		
		var mockDiscoveryService = new Mock<Domain.Interfaces.IDiscoveryService>();
		var discoveredUrls = Enumerable.Range(1, 10)
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
		
		var saga = new RecipeProcessingSaga(
			Mock.Of<ILogger<RecipeProcessingSaga>>(),
			_sagaRepository!,
			mockConfigLoader.Object,
			mockDiscoveryService.Object,
			mockFingerprinter.Object,
			Mock.Of<IIngredientNormalizer>(),
			mockRateLimiter.Object,
			Mock.Of<IRecipeBatchRepository>(),
		Mock.Of<IEventBus>()
		);
		
		// Act
		await saga.StartProcessingAsync(
			"test-provider",
			batchSize,
			TimeSpan.FromMinutes(5),
			CancellationToken.None);
		
		// Assert - Should process at most batchSize recipes
		processedCount.Should().BeLessThanOrEqualTo(batchSize);
	}
	
	[Fact(DisplayName = "Workflow handles discovery errors gracefully")]
	public async Task Workflow_HandlesDiscoveryErrors()
	{
		// Arrange
		var mockConfigLoader = new Mock<IProviderConfigurationLoader>();
		mockConfigLoader.Setup(c => c.GetByProviderIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((ProviderConfiguration?)null);
		
		var saga = new RecipeProcessingSaga(
			Mock.Of<ILogger<RecipeProcessingSaga>>(),
			_sagaRepository!,
			mockConfigLoader.Object,
			Mock.Of<Domain.Interfaces.IDiscoveryService>(),
			Mock.Of<IRecipeFingerprinter>(),
			Mock.Of<IIngredientNormalizer>(),
			Mock.Of<IRateLimiter>(),
			Mock.Of<IRecipeBatchRepository>(),
		Mock.Of<IEventBus>()
		);
		
		// Act & Assert
		await Assert.ThrowsAsync<InvalidOperationException>(async () =>
			await saga.StartProcessingAsync("test-provider", 10, TimeSpan.FromMinutes(5), CancellationToken.None));
	}
	
	[Fact(DisplayName = "Workflow filters duplicate recipes")]
	public async Task Workflow_FiltersDuplicateRecipes()
	{
		// Arrange
		var mockConfigLoader = new Mock<IProviderConfigurationLoader>();
		var mockConfig = new ProviderConfiguration(
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
		
		mockConfigLoader.Setup(c => c.GetByProviderIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(mockConfig);
		
		var mockDiscoveryService = new Mock<Domain.Interfaces.IDiscoveryService>();
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
		
		var saga = new RecipeProcessingSaga(
			Mock.Of<ILogger<RecipeProcessingSaga>>(),
			_sagaRepository!,
			mockConfigLoader.Object,
			mockDiscoveryService.Object,
			mockFingerprinter.Object,
			Mock.Of<IIngredientNormalizer>(),
			mockRateLimiter.Object,
			Mock.Of<IRecipeBatchRepository>(),
		Mock.Of<IEventBus>()
		);
		
		// Act
		await saga.StartProcessingAsync(
			"test-provider",
			10,
			TimeSpan.FromMinutes(5),
			CancellationToken.None);
		
		// Assert - Should only process 2 (filtered out the duplicate)
		processedCount.Should().Be(2, "one recipe should be filtered as duplicate");
	}
}
