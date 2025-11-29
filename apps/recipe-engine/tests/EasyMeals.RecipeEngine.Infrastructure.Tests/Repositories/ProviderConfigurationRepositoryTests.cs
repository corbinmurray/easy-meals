using EasyMeals.Domain.ProviderConfiguration;
using EasyMeals.Persistence.Abstractions.Exceptions;
using EasyMeals.Persistence.Mongo.Documents.ProviderConfiguration;
using EasyMeals.Persistence.Mongo.Repositories;
using EasyMeals.RecipeEngine.Infrastructure.Tests.Fixtures;
using FluentAssertions;
using MongoDB.Driver;
using Xunit;

namespace EasyMeals.RecipeEngine.Infrastructure.Tests.Repositories;

/// <summary>
/// Integration tests for <see cref="ProviderConfigurationRepository"/> CRUD operations
/// using Testcontainers for real MongoDB instance.
/// </summary>
[Collection(MongoDbTestCollection.Name)]
public class ProviderConfigurationRepositoryTests : IAsyncLifetime
{
    private readonly MongoDbFixture _fixture;
    private readonly ProviderConfigurationRepository _repository;
    private readonly IMongoCollection<ProviderConfigurationDocument> _collection;

    public ProviderConfigurationRepositoryTests(MongoDbFixture fixture)
    {
        _fixture = fixture;
        _repository = new ProviderConfigurationRepository(_fixture.Context);
        _collection = _fixture.Database.GetCollection<ProviderConfigurationDocument>("provider_configurations");
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        // Clean up collection after each test class
        await _collection.DeleteManyAsync(FilterDefinition<ProviderConfigurationDocument>.Empty);
    }

    #region Test Fixtures

    private static ProviderConfiguration CreateTestConfiguration(
        string providerName = "test-provider",
        string displayName = "Test Provider",
        int priority = 0,
        bool withApi = false)
    {
        var selectors = new ExtractionSelectors(
            titleSelector: ".recipe-title",
            descriptionSelector: ".recipe-description",
            ingredientsSelector: ".ingredients li",
            instructionsSelector: ".instructions ol li");

        var rateLimits = RateLimitSettings.Default;

        if (withApi)
        {
            var apiSettings = new ApiSettings(
                endpoint: "/api/recipes",
                authMethod: AuthMethod.ApiKey,
                headers: new Dictionary<string, string> { ["X-Api-Key"] = "secret:api-key" });

            return ProviderConfiguration.Create(
                providerName: providerName,
                displayName: displayName,
                baseUrl: "https://api.example.com",
                discoveryStrategy: DiscoveryStrategy.Api,
                fetchingStrategy: FetchingStrategy.Api,
                extractionSelectors: selectors,
                rateLimitSettings: rateLimits,
                priority: priority,
                apiSettings: apiSettings);
        }

        var crawlSettings = new CrawlSettings(
            seedUrls: new List<string> { "https://example.com/recipes" }.AsReadOnly(),
            includePatterns: new List<string> { "/recipes/*" }.AsReadOnly(),
            excludePatterns: new List<string>().AsReadOnly(),
            maxDepth: 3,
            linkSelector: "a.recipe-link");

        return ProviderConfiguration.Create(
            providerName: providerName,
            displayName: displayName,
            baseUrl: "https://example.com",
            discoveryStrategy: DiscoveryStrategy.Crawl,
            fetchingStrategy: FetchingStrategy.StaticHtml,
            extractionSelectors: selectors,
            rateLimitSettings: rateLimits,
            priority: priority,
            crawlSettings: crawlSettings);
    }

    #endregion

    #region AddAsync Tests

    [Fact]
    public async Task AddAsync_WithValidConfiguration_ReturnsGeneratedId()
    {
        // Arrange
        var config = CreateTestConfiguration("add-test-provider");

        // Act
        var id = await _repository.AddAsync(config);

        // Assert
        id.Should().NotBeNullOrEmpty();
        id.Should().HaveLength(24); // MongoDB ObjectId format
    }

    [Fact]
    public async Task AddAsync_WithValidConfiguration_PersistsToDatabase()
    {
        // Arrange
        var config = CreateTestConfiguration("persist-test-provider");

        // Act
        var id = await _repository.AddAsync(config);

        // Assert
        var document = await _collection.Find(d => d.Id == id).FirstOrDefaultAsync();
        document.Should().NotBeNull();
        document!.ProviderName.Should().Be("persist-test-provider");
        document.DisplayName.Should().Be("Test Provider");
        document.IsEnabled.Should().BeTrue();
        document.IsDeleted.Should().BeFalse();
        document.ConcurrencyToken.Should().Be(0);
        document.Version.Should().Be(1);
    }

    [Fact]
    public async Task AddAsync_WithDuplicateProviderName_ThrowsArgumentException()
    {
        // Arrange
        var config1 = CreateTestConfiguration("duplicate-provider");
        var config2 = CreateTestConfiguration("duplicate-provider");
        await _repository.AddAsync(config1);

        // Act
        var act = () => _repository.AddAsync(config2);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task AddAsync_NormalizesProviderName()
    {
        // Arrange - Provider name with uppercase will be normalized by Create()
        var config = CreateTestConfiguration("normalized-provider");

        // Act
        var id = await _repository.AddAsync(config);

        // Assert
        var document = await _collection.Find(d => d.Id == id).FirstOrDefaultAsync();
        document!.ProviderName.Should().Be("normalized-provider");
    }

    [Fact]
    public async Task AddAsync_SetsAuditTimestamps()
    {
        // Arrange
        var config = CreateTestConfiguration("audit-test-provider");
        var beforeAdd = DateTime.UtcNow;

        // Act
        var id = await _repository.AddAsync(config);

        // Assert
        var document = await _collection.Find(d => d.Id == id).FirstOrDefaultAsync();

        // Use BeCloseTo to account for MongoDB millisecond precision truncation
        document!.CreatedAt.Should().BeCloseTo(beforeAdd, TimeSpan.FromSeconds(1));
        document.UpdatedAt.Should().BeCloseTo(beforeAdd, TimeSpan.FromSeconds(1));
        document.CreatedAt.Should().Be(document.UpdatedAt);
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_WithExistingId_ReturnsConfiguration()
    {
        // Arrange
        var config = CreateTestConfiguration("getbyid-provider");
        var id = await _repository.AddAsync(config);

        // Act
        var result = await _repository.GetByIdAsync(id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(id);
        result.ProviderName.Should().Be("getbyid-provider");
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistentId_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByIdAsync("507f1f77bcf86cd799439011");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_WithInvalidId_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByIdAsync("invalid-id");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_WithEmptyId_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByIdAsync("");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_WithNullId_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByIdAsync(null!);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_WithSoftDeletedConfiguration_ReturnsNull()
    {
        // Arrange
        var config = CreateTestConfiguration("softdeleted-provider");
        var id = await _repository.AddAsync(config);
        await _repository.DeleteAsync(id);

        // Act
        var result = await _repository.GetByIdAsync(id);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetByProviderNameAsync Tests

    [Fact]
    public async Task GetByProviderNameAsync_WithExistingName_ReturnsConfiguration()
    {
        // Arrange
        var config = CreateTestConfiguration("getbyname-provider");
        await _repository.AddAsync(config);

        // Act
        var result = await _repository.GetByProviderNameAsync("getbyname-provider");

        // Assert
        result.Should().NotBeNull();
        result!.ProviderName.Should().Be("getbyname-provider");
    }

    [Fact]
    public async Task GetByProviderNameAsync_IsCaseInsensitive()
    {
        // Arrange
        var config = CreateTestConfiguration("case-insensitive-provider");
        await _repository.AddAsync(config);

        // Act
        var result = await _repository.GetByProviderNameAsync("CASE-INSENSITIVE-PROVIDER");

        // Assert
        result.Should().NotBeNull();
        result!.ProviderName.Should().Be("case-insensitive-provider");
    }

    [Fact]
    public async Task GetByProviderNameAsync_WithNonExistentName_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByProviderNameAsync("non-existent-provider");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByProviderNameAsync_WithSoftDeletedConfiguration_ReturnsNull()
    {
        // Arrange
        var config = CreateTestConfiguration("softdeleted-name-provider");
        var id = await _repository.AddAsync(config);
        await _repository.DeleteAsync(id);

        // Act
        var result = await _repository.GetByProviderNameAsync("softdeleted-name-provider");

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region ExistsByProviderNameAsync Tests

    [Fact]
    public async Task ExistsByProviderNameAsync_WithExistingName_ReturnsTrue()
    {
        // Arrange
        var config = CreateTestConfiguration("exists-provider");
        await _repository.AddAsync(config);

        // Act
        var result = await _repository.ExistsByProviderNameAsync("exists-provider");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsByProviderNameAsync_WithNonExistentName_ReturnsFalse()
    {
        // Act
        var result = await _repository.ExistsByProviderNameAsync("non-existent-exists-provider");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsByProviderNameAsync_WithSoftDeletedConfiguration_ReturnsFalse()
    {
        // Arrange
        var config = CreateTestConfiguration("softdeleted-exists-provider");
        var id = await _repository.AddAsync(config);
        await _repository.DeleteAsync(id);

        // Act
        var result = await _repository.ExistsByProviderNameAsync("softdeleted-exists-provider");

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region GetAllEnabledAsync Tests (T035)

    [Fact]
    public async Task GetAllEnabledAsync_ReturnsOnlyEnabledConfigurations()
    {
        // Arrange
        var enabledConfig = CreateTestConfiguration("enabled-filter-provider-1");
        var disabledConfig = CreateTestConfiguration("disabled-filter-provider");

        var enabledId = await _repository.AddAsync(enabledConfig);
        var disabledId = await _repository.AddAsync(disabledConfig);

        // Disable one configuration
        var toDisable = await _repository.GetByIdAsync(disabledId);
        toDisable!.Disable();
        await _repository.UpdateAsync(toDisable);

        // Act
        var results = await _repository.GetAllEnabledAsync();

        // Assert
        results.Should().Contain(c => c.Id == enabledId);
        results.Should().NotContain(c => c.Id == disabledId);
    }

    [Fact]
    public async Task GetAllEnabledAsync_ReturnsOrderedByPriorityDescending()
    {
        // Arrange - Create providers with different priorities
        var lowPriority = CreateTestConfiguration("priority-low-provider", priority: 10);
        var highPriority = CreateTestConfiguration("priority-high-provider", priority: 100);
        var mediumPriority = CreateTestConfiguration("priority-medium-provider", priority: 50);

        await _repository.AddAsync(lowPriority);
        await _repository.AddAsync(highPriority);
        await _repository.AddAsync(mediumPriority);

        // Act
        var results = await _repository.GetAllEnabledAsync();

        // Assert
        var priorityProviders = results
            .Where(c => c.ProviderName.StartsWith("priority-"))
            .ToList();

        priorityProviders.Should().HaveCount(3);
        priorityProviders[0].Priority.Should().Be(100);
        priorityProviders[1].Priority.Should().Be(50);
        priorityProviders[2].Priority.Should().Be(10);
    }

    [Fact]
    public async Task GetAllEnabledAsync_ExcludesSoftDeletedConfigurations()
    {
        // Arrange
        var activeConfig = CreateTestConfiguration("active-filter-provider");
        var deletedConfig = CreateTestConfiguration("deleted-filter-provider");

        await _repository.AddAsync(activeConfig);
        var deletedId = await _repository.AddAsync(deletedConfig);
        await _repository.DeleteAsync(deletedId);

        // Act
        var results = await _repository.GetAllEnabledAsync();

        // Assert
        results.Should().NotContain(c => c.ProviderName == "deleted-filter-provider");
    }

    [Fact]
    public async Task GetAllEnabledAsync_WithNoEnabledConfigurations_ReturnsEmptyList()
    {
        // Arrange - Create and disable a configuration
        var config = CreateTestConfiguration("lonely-disabled-provider");
        var id = await _repository.AddAsync(config);
        var toDisable = await _repository.GetByIdAsync(id);
        toDisable!.Disable();
        await _repository.UpdateAsync(toDisable);

        // Act
        var results = await _repository.GetAllEnabledAsync();

        // Assert
        results.Should().NotContain(c => c.ProviderName == "lonely-disabled-provider");
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_WithValidConfiguration_PersistsChanges()
    {
        // Arrange
        var config = CreateTestConfiguration("update-test-provider");
        var id = await _repository.AddAsync(config);
        var retrieved = await _repository.GetByIdAsync(id);
        retrieved!.Disable();

        // Act
        await _repository.UpdateAsync(retrieved);

        // Assert
        var updated = await _repository.GetByIdAsync(id);
        updated!.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_IncrementsConcurrencyToken()
    {
        // Arrange
        var config = CreateTestConfiguration("concurrency-test-provider");
        var id = await _repository.AddAsync(config);
        var retrieved = await _repository.GetByIdAsync(id);
        var originalToken = retrieved!.ConcurrencyToken;

        // Act
        retrieved.Disable();
        await _repository.UpdateAsync(retrieved);

        // Assert
        var document = await _collection.Find(d => d.Id == id).FirstOrDefaultAsync();
        document!.ConcurrencyToken.Should().Be(originalToken + 1);
    }

    [Fact]
    public async Task UpdateAsync_WithStaleConcurrencyToken_ThrowsConcurrencyException()
    {
        // Arrange
        var config = CreateTestConfiguration("stale-concurrency-provider");
        var id = await _repository.AddAsync(config);

        // Get two instances of the same configuration
        var instance1 = await _repository.GetByIdAsync(id);
        var instance2 = await _repository.GetByIdAsync(id);

        // Update first instance
        instance1!.Disable();
        await _repository.UpdateAsync(instance1);

        // Act - Try to update second instance (now stale)
        instance2!.Enable();
        var act = () => _repository.UpdateAsync(instance2);

        // Assert
        await act.Should().ThrowAsync<ConcurrencyException>();
    }

    [Fact]
    public async Task UpdateAsync_UpdatesTimestamp()
    {
        // Arrange
        var config = CreateTestConfiguration("timestamp-update-provider");
        var id = await _repository.AddAsync(config);
        var retrieved = await _repository.GetByIdAsync(id);
        var originalUpdatedAt = retrieved!.UpdatedAt;

        // Small delay to ensure timestamp difference
        await Task.Delay(50);

        // Act
        retrieved.Disable();
        await _repository.UpdateAsync(retrieved);

        // Assert
        var document = await _collection.Find(d => d.Id == id).FirstOrDefaultAsync();
        document!.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_SoftDeletesConfiguration()
    {
        // Arrange
        var config = CreateTestConfiguration("softdelete-test-provider");
        var id = await _repository.AddAsync(config);

        // Act
        await _repository.DeleteAsync(id);

        // Assert
        var document = await _collection.Find(d => d.Id == id).FirstOrDefaultAsync();
        document.Should().NotBeNull(); // Document still exists
        document!.IsDeleted.Should().BeTrue();
        document.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteAsync_WithInvalidId_ThrowsArgumentException()
    {
        // Act
        var act = () => _repository.DeleteAsync("invalid-id");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task DeleteAsync_WithEmptyId_ThrowsArgumentException()
    {
        // Act
        var act = () => _repository.DeleteAsync("");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    #endregion

    #region Round-Trip Persistence Tests

    [Fact]
    public async Task RoundTrip_PreservesAllCrawlConfigurationProperties()
    {
        // Arrange
        var original = CreateTestConfiguration(
            providerName: "roundtrip-crawl-provider",
            displayName: "Round Trip Crawl Provider",
            priority: 42);

        // Act
        var id = await _repository.AddAsync(original);
        var retrieved = await _repository.GetByIdAsync(id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.ProviderName.Should().Be("roundtrip-crawl-provider");
        retrieved.DisplayName.Should().Be("Round Trip Crawl Provider");
        retrieved.BaseUrl.Should().Be("https://example.com");
        retrieved.Priority.Should().Be(42);
        retrieved.IsEnabled.Should().BeTrue();
        retrieved.DiscoveryStrategy.Should().Be(DiscoveryStrategy.Crawl);
        retrieved.FetchingStrategy.Should().Be(FetchingStrategy.StaticHtml);

        // Extraction selectors
        retrieved.ExtractionSelectors.TitleSelector.Should().Be(".recipe-title");
        retrieved.ExtractionSelectors.DescriptionSelector.Should().Be(".recipe-description");
        retrieved.ExtractionSelectors.IngredientsSelector.Should().Be(".ingredients li");
        retrieved.ExtractionSelectors.InstructionsSelector.Should().Be(".instructions ol li");

        // Rate limits
        retrieved.RateLimitSettings.RequestsPerMinute.Should().Be(60);

        // Crawl settings
        retrieved.CrawlSettings.Should().NotBeNull();
        retrieved.CrawlSettings!.SeedUrls.Should().Contain("https://example.com/recipes");
        retrieved.CrawlSettings.MaxDepth.Should().Be(3);
    }

    [Fact]
    public async Task RoundTrip_PreservesAllApiConfigurationProperties()
    {
        // Arrange
        var original = CreateTestConfiguration(
            providerName: "roundtrip-api-provider",
            displayName: "Round Trip API Provider",
            priority: 99,
            withApi: true);

        // Act
        var id = await _repository.AddAsync(original);
        var retrieved = await _repository.GetByIdAsync(id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.ProviderName.Should().Be("roundtrip-api-provider");
        retrieved.DiscoveryStrategy.Should().Be(DiscoveryStrategy.Api);
        retrieved.FetchingStrategy.Should().Be(FetchingStrategy.Api);

        // API settings
        retrieved.ApiSettings.Should().NotBeNull();
        retrieved.ApiSettings!.Endpoint.Should().Be("/api/recipes");
        retrieved.ApiSettings.AuthMethod.Should().Be(AuthMethod.ApiKey);
        retrieved.ApiSettings.Headers.Should().ContainKey("X-Api-Key");
    }

    #endregion
}
