using EasyMeals.Persistence.Mongo.Documents.ProviderConfiguration;
using EasyMeals.Persistence.Mongo.Indexes;
using EasyMeals.RecipeEngine.Infrastructure.Tests.Fixtures;
using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace EasyMeals.RecipeEngine.Infrastructure.Tests.Indexes;

/// <summary>
/// Integration tests to verify MongoDB indexes are created correctly.
/// </summary>
[Collection(MongoDbTestCollection.Name)]
public class IndexEnforcementTests : IAsyncLifetime
{
    private readonly MongoDbFixture _fixture;
    private readonly IMongoCollection<ProviderConfigurationDocument> _collection;

    public IndexEnforcementTests(MongoDbFixture fixture)
    {
        _fixture = fixture;
        _collection = _fixture.Database.GetCollection<ProviderConfigurationDocument>("provider_configurations");
    }

    public async Task InitializeAsync()
    {
        // Drop and recreate collection to start fresh
        await _fixture.Database.DropCollectionAsync("provider_configurations");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CreateIndexesAsync_CreatesProviderNameUniqueIndex()
    {
        // Act
        await ProviderConfigurationIndexes.CreateIndexesAsync(_collection);

        // Assert
        var indexes = await _collection.Indexes.List().ToListAsync();
        var providerNameIndex = indexes.FirstOrDefault(i =>
            i["name"].AsString == "idx_provider_name_unique");

        providerNameIndex.Should().NotBeNull();
        providerNameIndex!["unique"].AsBoolean.Should().BeTrue();
    }

    [Fact]
    public async Task CreateIndexesAsync_CreatesEnabledPriorityCompoundIndex()
    {
        // Act
        await ProviderConfigurationIndexes.CreateIndexesAsync(_collection);

        // Assert
        var indexes = await _collection.Indexes.List().ToListAsync();
        var enabledPriorityIndex = indexes.FirstOrDefault(i =>
            i["name"].AsString == "idx_enabled_priority");

        enabledPriorityIndex.Should().NotBeNull();

        // Verify compound index structure (using BSON field names)
        var key = enabledPriorityIndex!["key"].AsBsonDocument;
        key.Contains("isEnabled").Should().BeTrue();
        key.Contains("isDeleted").Should().BeTrue();
        key.Contains("priority").Should().BeTrue();
    }

    [Fact]
    public async Task CreateIndexesAsync_CreatesIsDeletedIndex()
    {
        // Act
        await ProviderConfigurationIndexes.CreateIndexesAsync(_collection);

        // Assert
        var indexes = await _collection.Indexes.List().ToListAsync();
        var isDeletedIndex = indexes.FirstOrDefault(i =>
            i["name"].AsString == "idx_is_deleted");

        isDeletedIndex.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateIndexesAsync_CreatesTotalOfFourIndexes()
    {
        // Act
        await ProviderConfigurationIndexes.CreateIndexesAsync(_collection);

        // Assert
        var indexes = await _collection.Indexes.List().ToListAsync();

        // Should have 4 indexes: _id (default) + 3 custom indexes
        indexes.Should().HaveCount(4);
    }

    [Fact]
    public async Task CreateIndexesAsync_IsIdempotent()
    {
        // Act - Call twice
        await ProviderConfigurationIndexes.CreateIndexesAsync(_collection);
        await ProviderConfigurationIndexes.CreateIndexesAsync(_collection);

        // Assert - Should still have exactly 4 indexes
        var indexes = await _collection.Indexes.List().ToListAsync();
        indexes.Should().HaveCount(4);
    }

    [Fact]
    public async Task CreateIndexesAsync_WithDatabaseOverload_CreatesIndexes()
    {
        // Act
        await ProviderConfigurationIndexes.CreateIndexesAsync(_fixture.Database);

        // Assert
        var indexes = await _collection.Indexes.List().ToListAsync();
        indexes.Should().HaveCount(4);
    }

    [Fact]
    public async Task CreateIndexesAsync_WithContextOverload_CreatesIndexes()
    {
        // Act
        await ProviderConfigurationIndexes.CreateIndexesAsync(_fixture.Context);

        // Assert
        var indexes = await _collection.Indexes.List().ToListAsync();
        indexes.Should().HaveCount(4);
    }

    [Fact]
    public async Task UniqueProviderNameIndex_EnforcesDuplicateRejection()
    {
        // Arrange
        await ProviderConfigurationIndexes.CreateIndexesAsync(_collection);

        var document1 = CreateTestDocument("unique-test-provider");
        var document2 = CreateTestDocument("unique-test-provider");

        await _collection.InsertOneAsync(document1);

        // Act
        var act = () => _collection.InsertOneAsync(document2);

        // Assert
        await act.Should().ThrowAsync<MongoWriteException>()
            .Where(e => e.WriteError.Category == ServerErrorCategory.DuplicateKey);
    }

    [Fact]
    public async Task EnabledPriorityIndex_OptimizesEnabledProviderQueries()
    {
        // Arrange
        await ProviderConfigurationIndexes.CreateIndexesAsync(_collection);

        // Insert test documents
        for (int i = 0; i < 10; i++)
        {
            var doc = CreateTestDocument($"perf-test-provider-{i}");
            doc.Priority = i * 10;
            doc.IsEnabled = i % 2 == 0;
            await _collection.InsertOneAsync(doc);
        }

        // Act - Query using the indexed fields
        var filter = Builders<ProviderConfigurationDocument>.Filter.And(
            Builders<ProviderConfigurationDocument>.Filter.Eq(d => d.IsEnabled, true),
            Builders<ProviderConfigurationDocument>.Filter.Eq(d => d.IsDeleted, false));
        var sort = Builders<ProviderConfigurationDocument>.Sort.Descending(d => d.Priority);

        var results = await _collection
            .Find(filter)
            .Sort(sort)
            .ToListAsync();

        // Assert - Verify correct results
        results.Should().HaveCount(5);
        results.Should().BeInDescendingOrder(d => d.Priority);
    }

    private static ProviderConfigurationDocument CreateTestDocument(string providerName)
    {
        return new ProviderConfigurationDocument
        {
            Id = ObjectId.GenerateNewId().ToString(),
            ProviderName = providerName,
            DisplayName = "Test Provider",
            BaseUrl = "https://example.com",
            IsEnabled = true,
            Priority = 0,
            DiscoveryStrategy = "Crawl",
            FetchingStrategy = "StaticHtml",
            ExtractionSelectors = new ExtractionSelectorsDocument
            {
                TitleSelector = ".title",
                DescriptionSelector = ".description",
                IngredientsSelector = ".ingredients",
                InstructionsSelector = ".instructions"
            },
            RateLimitSettings = new RateLimitSettingsDocument
            {
                RequestsPerMinute = 60,
                DelayBetweenRequestsMs = 100,
                MaxConcurrentRequests = 5,
                MaxRetries = 3,
                RetryDelayMs = 1000
            },
            ConcurrencyToken = 0,
            Version = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}
