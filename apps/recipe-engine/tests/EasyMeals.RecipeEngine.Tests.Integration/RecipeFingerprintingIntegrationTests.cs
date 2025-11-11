using EasyMeals.RecipeEngine.Application.Interfaces;
using EasyMeals.RecipeEngine.Domain.Entities;
using EasyMeals.RecipeEngine.Domain.Repositories;
using EasyMeals.RecipeEngine.Infrastructure.Documents;
using EasyMeals.RecipeEngine.Infrastructure.Fingerprinting;
using EasyMeals.RecipeEngine.Infrastructure.Repositories;
using FluentAssertions;
using MongoDB.Driver;
using Testcontainers.MongoDb;

namespace EasyMeals.RecipeEngine.Tests.Integration;

/// <summary>
///     Integration tests for recipe fingerprinting with MongoDB.
///     Verifies fingerprint persistence, lookup, and duplicate detection with real database.
///     T119: Integration test for fingerprint persistence and lookup (save to MongoDB, query by hash, verify fast lookup with index)
/// </summary>
public class RecipeFingerprintingIntegrationTests : IAsyncLifetime
{
	private IMongoDatabase? _database;
	private IRecipeFingerprinter? _fingerprintService;
	private IMongoClient? _mongoClient;
	private MongoDbContainer? _mongoContainer;
	private IRecipeFingerprintRepository? _repository;

	public async Task DisposeAsync()
	{
		if (_mongoContainer != null) await _mongoContainer.DisposeAsync();
	}

	public async Task InitializeAsync()
	{
		_mongoContainer = new MongoDbBuilder()
			.WithImage("mongo:7.0")
			.Build();

		await _mongoContainer.StartAsync();

		_mongoClient = new MongoClient(_mongoContainer.GetConnectionString());
		_database = _mongoClient.GetDatabase("easymeals_test");
		_repository = new RecipeFingerprintRepository(_database);
		_fingerprintService = new RecipeFingerprintService(_repository);

		// Create indexes for fast lookups
		IMongoCollection<RecipeFingerprintDocument>? collection = _database.GetCollection<RecipeFingerprintDocument>("fingerprints");

		// Index on FingerprintHash for fast duplicate detection
		await collection.Indexes.CreateOneAsync(
			new CreateIndexModel<RecipeFingerprintDocument>(
				Builders<RecipeFingerprintDocument>.IndexKeys.Ascending(d => d.FingerprintHash),
				new CreateIndexOptions { Name = "idx_fingerprint_hash" }));

		// Compound index on ProviderId + RecipeUrl
		await collection.Indexes.CreateOneAsync(
			new CreateIndexModel<RecipeFingerprintDocument>(
				Builders<RecipeFingerprintDocument>.IndexKeys
					.Ascending(d => d.ProviderId)
					.Ascending(d => d.RecipeUrl),
				new CreateIndexOptions { Name = "idx_provider_url" }));
	}

	[Fact(DisplayName = "Count by provider returns correct count")]
	public async Task CountByProvider_ReturnsCorrectCount()
	{
		// Arrange
		const string provider1 = "provider_001";
		const string provider2 = "provider_002";

		// Store 3 fingerprints for provider_001
		for (var i = 0; i < 3; i++)
		{
			var url = $"https://example.com/provider1/recipe-{i}";
			string fingerprint = _fingerprintService!.GenerateFingerprint(url, $"Recipe {i}", $"Description {i}");
			await _fingerprintService.StoreFingerprintAsync(fingerprint, provider1, url, Guid.NewGuid());
		}

		// Store 2 fingerprints for provider_002
		for (var i = 0; i < 2; i++)
		{
			var url = $"https://example.com/provider2/recipe-{i}";
			string fingerprint = _fingerprintService!.GenerateFingerprint(url, $"Recipe {i}", $"Description {i}");
			await _fingerprintService.StoreFingerprintAsync(fingerprint, provider2, url, Guid.NewGuid());
		}

		// Act
		int count1 = await _repository!.CountByProviderAsync(provider1);
		int count2 = await _repository.CountByProviderAsync(provider2);

		// Assert
		count1.Should().Be(3, "Provider 001 should have 3 fingerprints");
		count2.Should().Be(2, "Provider 002 should have 2 fingerprints");
	}

	[Fact(DisplayName = "Different content generates different fingerprints")]
	public async Task DifferentContent_GeneratesDifferentFingerprints()
	{
		// Arrange
		const string url1 = "https://example.com/recipes/pasta-1";
		const string url2 = "https://example.com/recipes/pasta-2";
		const string title = "Pasta Recipe";
		const string description = "Delicious pasta";
		const string providerId = "provider_001";
		var recipeId1 = Guid.NewGuid();
		var recipeId2 = Guid.NewGuid();

		// Act - Generate different fingerprints
		string fingerprint1 = _fingerprintService!.GenerateFingerprint(url1, title, description);
		string fingerprint2 = _fingerprintService.GenerateFingerprint(url2, title, description);

		// Store first fingerprint
		await _fingerprintService.StoreFingerprintAsync(
			fingerprint1,
			providerId,
			url1,
			recipeId1);

		// Check if second is duplicate
		bool isDuplicate = await _fingerprintService.IsDuplicateAsync(fingerprint2);

		// Assert
		fingerprint1.Should().NotBe(fingerprint2, "Different URLs should produce different hashes");
		isDuplicate.Should().BeFalse("Different fingerprint should not be detected as duplicate");
	}

	[Fact(DisplayName = "Fast lookup with hash index verifies MongoDB performance")]
	public async Task FastLookup_WithHashIndex_VerifiesPerformance()
	{
		// Arrange - Store multiple fingerprints to test index performance
		const int fingerprintCount = 100;
		var fingerprints = new List<string>();

		for (var i = 0; i < fingerprintCount; i++)
		{
			var url = $"https://example.com/recipes/recipe-{i}";
			var title = $"Recipe {i}";
			var description = $"Description for recipe {i}";
			string fingerprint = _fingerprintService!.GenerateFingerprint(url, title, description);

			await _fingerprintService.StoreFingerprintAsync(
				fingerprint,
				"provider_001",
				url,
				Guid.NewGuid());

			fingerprints.Add(fingerprint);
		}

		// Act - Perform lookups and measure time
		DateTime startTime = DateTime.UtcNow;

		foreach (string fingerprint in fingerprints)
		{
			bool isDuplicate = await _fingerprintService!.IsDuplicateAsync(fingerprint);
			isDuplicate.Should().BeTrue("All fingerprints were stored");
		}

		TimeSpan duration = DateTime.UtcNow - startTime;

		// Assert - Verify all lookups completed reasonably fast (with index)
		// With proper indexing, 100 lookups should complete in well under 1 second
		duration.Should().BeLessThan(TimeSpan.FromSeconds(2),
			"Hash index should enable fast duplicate detection");
	}

	[Fact(DisplayName = "Generate and store fingerprint persists to MongoDB")]
	public async Task GenerateAndStoreFingerprint_PersistsToMongoDB()
	{
		// Arrange
		const string url = "https://example.com/recipes/pasta-carbonara";
		const string title = "Pasta Carbonara";
		const string description = "Classic Italian pasta dish with eggs, cheese, and bacon";
		const string providerId = "provider_001";
		var recipeId = Guid.NewGuid();

		// Act - Generate fingerprint
		string fingerprint = _fingerprintService!.GenerateFingerprint(url, title, description);

		// Store fingerprint
		await _fingerprintService.StoreFingerprintAsync(
			fingerprint,
			providerId,
			url,
			recipeId);

		// Assert - Verify it was saved to MongoDB
		RecipeFingerprint? savedFingerprint = await _repository!.GetByUrlAsync(url, providerId);

		savedFingerprint.Should().NotBeNull();
		savedFingerprint!.FingerprintHash.Should().Be(fingerprint);
		savedFingerprint.ProviderId.Should().Be(providerId);
		savedFingerprint.RecipeUrl.Should().Be(url);
		savedFingerprint.RecipeId.Should().Be(recipeId);
		savedFingerprint.ProcessedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
	}

	[Fact(DisplayName = "Is duplicate returns true for existing fingerprint")]
	public async Task IsDuplicate_ExistingFingerprint_ReturnsTrue()
	{
		// Arrange
		const string url = "https://example.com/recipes/spaghetti-bolognese";
		const string title = "Spaghetti Bolognese";
		const string description = "Traditional Italian meat sauce with pasta";
		const string providerId = "provider_001";
		var recipeId = Guid.NewGuid();

		// Generate and store fingerprint
		string fingerprint = _fingerprintService!.GenerateFingerprint(url, title, description);
		await _fingerprintService.StoreFingerprintAsync(
			fingerprint,
			providerId,
			url,
			recipeId);

		// Act - Check if duplicate
		bool isDuplicate = await _fingerprintService.IsDuplicateAsync(fingerprint);

		// Assert
		isDuplicate.Should().BeTrue("Fingerprint was previously stored");
	}

	[Fact(DisplayName = "Is duplicate returns false for new fingerprint")]
	public async Task IsDuplicate_NewFingerprint_ReturnsFalse()
	{
		// Arrange
		const string url = "https://example.com/recipes/never-seen-before";
		const string title = "Brand New Recipe";
		const string description = "This recipe has never been processed";

		// Generate fingerprint (but don't store it)
		string fingerprint = _fingerprintService!.GenerateFingerprint(url, title, description);

		// Act - Check if duplicate
		bool isDuplicate = await _fingerprintService.IsDuplicateAsync(fingerprint);

		// Assert
		isDuplicate.Should().BeFalse("Fingerprint has never been stored");
	}

	[Fact(DisplayName = "Multiple providers can have different fingerprints for same URL")]
	public async Task MultipleProviders_DifferentFingerprintsForSameUrl()
	{
		// Arrange
		const string url = "https://example.com/recipes/shared-recipe";
		const string title = "Shared Recipe";
		const string description = "This recipe exists on multiple provider sites";
		const string provider1 = "provider_001";
		const string provider2 = "provider_002";
		var recipeId1 = Guid.NewGuid();
		var recipeId2 = Guid.NewGuid();

		// Generate same fingerprint (same content)
		string fingerprint = _fingerprintService!.GenerateFingerprint(url, title, description);

		// Act - Store for both providers
		await _fingerprintService.StoreFingerprintAsync(
			fingerprint,
			provider1,
			url,
			recipeId1);

		await _fingerprintService.StoreFingerprintAsync(
			fingerprint,
			provider2,
			url,
			recipeId2);

		// Assert - Both should be stored
		RecipeFingerprint? saved1 = await _repository!.GetByUrlAsync(url, provider1);
		RecipeFingerprint? saved2 = await _repository.GetByUrlAsync(url, provider2);

		saved1.Should().NotBeNull();
		saved2.Should().NotBeNull();
		saved1!.ProviderId.Should().Be(provider1);
		saved2!.ProviderId.Should().Be(provider2);
		saved1.FingerprintHash.Should().Be(saved2.FingerprintHash, "Same content produces same hash");
	}

	[Fact(DisplayName = "Same content generates same fingerprint and detects duplicate")]
	public async Task SameContent_GeneratesSameFingerprintAndDetectsDuplicate()
	{
		// Arrange
		const string url = "https://example.com/recipes/chicken-curry";
		const string title = "Chicken Curry";
		const string description = "Spicy Indian curry with tender chicken";
		const string providerId = "provider_001";
		var recipeId1 = Guid.NewGuid();
		var recipeId2 = Guid.NewGuid();

		// Act - Generate fingerprint twice
		string fingerprint1 = _fingerprintService!.GenerateFingerprint(url, title, description);
		string fingerprint2 = _fingerprintService.GenerateFingerprint(url, title, description);

		// Store first fingerprint
		await _fingerprintService.StoreFingerprintAsync(
			fingerprint1,
			providerId,
			url,
			recipeId1);

		// Check if second is duplicate
		bool isDuplicate = await _fingerprintService.IsDuplicateAsync(fingerprint2);

		// Assert
		fingerprint1.Should().Be(fingerprint2, "Same content should produce same hash");
		isDuplicate.Should().BeTrue("Second fingerprint should be detected as duplicate");
	}
}