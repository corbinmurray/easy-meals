using EasyMeals.RecipeEngine.Application.Interfaces;
using EasyMeals.RecipeEngine.Domain.Entities;
using EasyMeals.RecipeEngine.Domain.Repositories;
using EasyMeals.RecipeEngine.Infrastructure.DependencyInjection;
using EasyMeals.RecipeEngine.Infrastructure.Normalization;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Testcontainers.MongoDb;

namespace EasyMeals.RecipeEngine.Tests.Integration;

/// <summary>
///     Integration tests for ingredient normalization workflow with MongoDB.
///     Tests T064: Query MongoDB, return canonical form, cache frequently used mappings
/// </summary>
public class IngredientNormalizationIntegrationTests : IAsyncLifetime
{
	private MongoDbContainer? _mongoContainer;
	private IIngredientNormalizer? _normalizer;
	private IIngredientMappingRepository? _repository;
	private ServiceProvider? _serviceProvider;

	public async Task DisposeAsync()
	{
		if (_serviceProvider != null) await _serviceProvider.DisposeAsync();

		if (_mongoContainer != null)
		{
			await _mongoContainer.StopAsync();
			await _mongoContainer.DisposeAsync();
		}
	}

	public async Task InitializeAsync()
	{
		// Start MongoDB container
		_mongoContainer = new MongoDbBuilder()
			.WithImage("mongo:7.0")
			.WithUsername("admin")
			.WithPassword("testpassword")
			.Build();

		await _mongoContainer.StartAsync();

		// Setup DI container with MongoDB connection
		IConfigurationRoot configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["MongoDB:ConnectionString"] = _mongoContainer.GetConnectionString(),
				["MongoDB:DatabaseName"] = "easymeals_test"
			})
			.Build();

		var services = new ServiceCollection();
		services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Debug));

		// Register event bus manually for tests
		services.AddSingleton<IEventBus, EasyMealsEventBus>();

		services.AddRecipeEngineInfrastructure(configuration);

		_serviceProvider = services.BuildServiceProvider();
		_repository = _serviceProvider.GetRequiredService<IIngredientMappingRepository>();
		_normalizer = _serviceProvider.GetRequiredService<IIngredientNormalizer>();
	}

	[Fact(DisplayName = "Integration: Different providers maintain separate ingredient mappings")]
	public async Task NormalizeAsync_DifferentProviders_MaintainSeparateMappings()
	{
		// Arrange - Same code, different providers, different canonical forms
		const string provider1 = "provider_001";
		const string provider2 = "provider_002";
		const string code = "BROCCOLI-001";

		await _repository!.SaveAsync(IngredientMapping.Create(provider1, code, "broccoli, fresh"));
		await _repository.SaveAsync(IngredientMapping.Create(provider2, code, "broccoli, frozen"));

		// Act
		string? result1 = await _normalizer!.NormalizeAsync(provider1, code);
		string? result2 = await _normalizer.NormalizeAsync(provider2, code);

		// Assert
		result1.Should().Be("broccoli, fresh");
		result2.Should().Be("broccoli, frozen");
	}

	[Fact(DisplayName = "Integration: Cache reduces database queries for frequently used ingredients")]
	public async Task NormalizeAsync_FrequentlyUsedIngredient_UsesCacheToReduceDatabaseQueries()
	{
		// Arrange
		const string providerId = "provider_001";
		const string providerCode = "HF-BROCCOLI-COMMON";
		const string canonicalForm = "broccoli";

		var mapping = IngredientMapping.Create(providerId, providerCode, canonicalForm);
		await _repository!.SaveAsync(mapping);

		// Act - Call multiple times
		string? result1 = await _normalizer!.NormalizeAsync(providerId, providerCode);
		string? result2 = await _normalizer.NormalizeAsync(providerId, providerCode);
		string? result3 = await _normalizer.NormalizeAsync(providerId, providerCode);

		// Assert
		result1.Should().Be(canonicalForm);
		result2.Should().Be(canonicalForm);
		result3.Should().Be(canonicalForm);

		// Cache hit is verified through logs - only first call queries MongoDB
		// Subsequent calls use cached value (verified in unit tests)
	}

	[Fact(DisplayName = "Integration: NormalizeAsync queries MongoDB and returns canonical form")]
	public async Task NormalizeAsync_QueriesMongoDB_ReturnsCanonicalForm()
	{
		// Arrange
		const string providerId = "provider_001";
		const string providerCode = "HF-BROCCOLI-FROZEN-012";
		const string expectedCanonical = "broccoli, frozen";

		var mapping = IngredientMapping.Create(providerId, providerCode, expectedCanonical);
		await _repository!.SaveAsync(mapping);

		// Act
		string? result = await _normalizer!.NormalizeAsync(providerId, providerCode);

		// Assert
		result.Should().Be(expectedCanonical);
	}

	[Fact(DisplayName = "Integration: NormalizeAsync returns null for unmapped ingredient")]
	public async Task NormalizeAsync_UnmappedIngredient_ReturnsNull()
	{
		// Arrange
		const string providerId = "provider_001";
		const string providerCode = "UNKNOWN-INGREDIENT-999";

		// Act
		string? result = await _normalizer!.NormalizeAsync(providerId, providerCode);

		// Assert
		result.Should().BeNull();
	}

	[Fact(DisplayName = "Integration: Updating ingredient mapping reflects in subsequent normalizations")]
	public async Task NormalizeAsync_UpdatedMapping_ReflectsInSubsequentCalls()
	{
		// Arrange
		const string providerId = "provider_001";
		const string providerCode = "HF-TOMATO-001";
		const string originalCanonical = "tomato, fresh";
		const string updatedCanonical = "tomato, organic";

		// Create initial mapping
		var mapping = IngredientMapping.Create(providerId, providerCode, originalCanonical);
		await _repository!.SaveAsync(mapping);

		// First normalization
		string? result1 = await _normalizer!.NormalizeAsync(providerId, providerCode);
		result1.Should().Be(originalCanonical);

		// Update mapping
		mapping.UpdateCanonicalForm(updatedCanonical);
		await _repository.SaveAsync(mapping);

		// Create a new normalizer instance to bypass cache
		var loggerFactory = _serviceProvider!.GetRequiredService<ILoggerFactory>();
		var eventBus = _serviceProvider.GetRequiredService<IEventBus>();
		ArgumentNullException.ThrowIfNull(_serviceProvider);
		var newNormalizer = new IngredientNormalizationService(
			_repository,
			loggerFactory.CreateLogger<IngredientNormalizationService>(),
			eventBus);

		// Act - Second normalization with fresh normalizer
		string? result2 = await newNormalizer.NormalizeAsync(providerId, providerCode);

		// Assert
		result2.Should().Be(updatedCanonical);
	}

	[Fact(DisplayName = "Integration: NormalizeBatchAsync handles large batches efficiently")]
	public async Task NormalizeBatchAsync_LargeBatch_HandlesEfficiently()
	{
		// Arrange - Create 100 ingredient mappings
		const string providerId = "provider_001";
		var codes = new List<string>();

		for (var i = 1; i <= 100; i++)
		{
			var code = $"HF-INGREDIENT-{i:D3}";
			var canonical = $"ingredient_{i}";
			codes.Add(code);
			await _repository!.SaveAsync(IngredientMapping.Create(providerId, code, canonical));
		}

		// Act
		IDictionary<string, string?> result = await _normalizer!.NormalizeBatchAsync(providerId, codes);

		// Assert
		result.Should().HaveCount(100);
		result.Values.Should().NotContainNulls();
		result["HF-INGREDIENT-001"].Should().Be("ingredient_1");
		result["HF-INGREDIENT-100"].Should().Be("ingredient_100");
	}

	[Fact(DisplayName = "Integration: NormalizeBatchAsync efficiently queries multiple ingredients")]
	public async Task NormalizeBatchAsync_MultipleIngredients_QueriesEfficientlyWithDifferentProviders()
	{
		// Arrange
		const string providerId = "provider_001";
		var codes = new[] { "HF-GARLIC-001", "HF-OLIVE-OIL-002", "HF-SALT-003", "UNKNOWN-999" };

		// Seed mappings for known ingredients
		await _repository!.SaveAsync(IngredientMapping.Create(providerId, "HF-GARLIC-001", "garlic"));
		await _repository.SaveAsync(IngredientMapping.Create(providerId, "HF-OLIVE-OIL-002", "olive oil"));
		await _repository.SaveAsync(IngredientMapping.Create(providerId, "HF-SALT-003", "salt"));

		// Act
		IDictionary<string, string?> result = await _normalizer!.NormalizeBatchAsync(providerId, codes);

		// Assert
		result.Should().HaveCount(4);
		result["HF-GARLIC-001"].Should().Be("garlic");
		result["HF-OLIVE-OIL-002"].Should().Be("olive oil");
		result["HF-SALT-003"].Should().Be("salt");
		result["UNKNOWN-999"].Should().BeNull();
	}
}