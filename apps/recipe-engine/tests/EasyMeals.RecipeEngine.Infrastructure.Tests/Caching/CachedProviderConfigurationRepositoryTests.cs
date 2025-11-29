using EasyMeals.Domain.ProviderConfiguration;
using EasyMeals.Persistence.Abstractions.Repositories;
using EasyMeals.RecipeEngine.Infrastructure.Caching;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace EasyMeals.RecipeEngine.Infrastructure.Tests.Caching;

/// <summary>
/// Integration tests for <see cref="CachedProviderConfigurationRepository"/> caching behavior.
/// Tests cache TTL expiration and cache hit/miss scenarios.
/// </summary>
public class CachedProviderConfigurationRepositoryTests
{
    private readonly IProviderConfigurationRepository _innerRepository;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CachedProviderConfigurationRepository> _logger;

    public CachedProviderConfigurationRepositoryTests()
    {
        _innerRepository = Substitute.For<IProviderConfigurationRepository>();
        _cache = new MemoryCache(new MemoryCacheOptions());
        _logger = Substitute.For<ILogger<CachedProviderConfigurationRepository>>();
    }

    #region Test Fixtures

    private static ProviderConfiguration CreateTestConfiguration(
        string providerName = "test-provider",
        string id = "507f1f77bcf86cd799439011")
    {
        var selectors = new ExtractionSelectors(
            titleSelector: ".recipe-title",
            descriptionSelector: ".recipe-description",
            ingredientsSelector: ".ingredients li",
            instructionsSelector: ".instructions ol li");

        var rateLimits = RateLimitSettings.Default;

        var crawlSettings = new CrawlSettings(
            seedUrls: new List<string> { "https://example.com/recipes" }.AsReadOnly(),
            includePatterns: new List<string> { "/recipes/*" }.AsReadOnly(),
            excludePatterns: new List<string>().AsReadOnly(),
            maxDepth: 3,
            linkSelector: "a.recipe-link");

        var config = ProviderConfiguration.Create(
            providerName: providerName,
            displayName: "Test Provider",
            baseUrl: "https://example.com",
            discoveryStrategy: DiscoveryStrategy.Crawl,
            fetchingStrategy: FetchingStrategy.StaticHtml,
            extractionSelectors: selectors,
            rateLimitSettings: rateLimits,
            crawlSettings: crawlSettings);

        // Use reflection to set the Id since it's normally set by the repository
        typeof(ProviderConfiguration)
            .GetProperty("Id")!
            .SetValue(config, id);

        return config;
    }

    private CachedProviderConfigurationRepository CreateRepository(TimeSpan? ttl = null, bool enabled = true)
    {
        var options = Options.Create(new ProviderConfigurationCacheOptions
        {
            CacheTtl = ttl ?? TimeSpan.FromSeconds(30),
            Enabled = enabled
        });

        return new CachedProviderConfigurationRepository(
            _innerRepository,
            _cache,
            options,
            _logger);
    }

    #endregion

    #region Cache Hit/Miss Tests (T045)

    [Fact]
    public async Task GetByIdAsync_FirstCall_CachesMissAndFetchesFromInner()
    {
        // Arrange
        var config = CreateTestConfiguration("cache-miss-provider", "id123");
        _innerRepository.GetByIdAsync("id123", Arg.Any<CancellationToken>())
            .Returns(config);

        var repository = CreateRepository();

        // Act
        var result = await repository.GetByIdAsync("id123");

        // Assert
        result.Should().NotBeNull();
        result!.ProviderName.Should().Be("cache-miss-provider");

        // Verify inner repository was called
        await _innerRepository.Received(1).GetByIdAsync("id123", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetByIdAsync_SecondCallWithinTtl_ReturnsCachedInstance()
    {
        // Arrange
        var config = CreateTestConfiguration("cached-provider", "id456");
        _innerRepository.GetByIdAsync("id456", Arg.Any<CancellationToken>())
            .Returns(config);

        var repository = CreateRepository(TimeSpan.FromMinutes(5));

        // Act - First call populates cache
        var firstResult = await repository.GetByIdAsync("id456");

        // Act - Second call should use cache
        var secondResult = await repository.GetByIdAsync("id456");

        // Assert
        firstResult.Should().BeSameAs(secondResult);

        // Inner repository should only be called once
        await _innerRepository.Received(1).GetByIdAsync("id456", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAllEnabledAsync_FirstCall_CachesMissAndFetchesFromInner()
    {
        // Arrange
        var configs = new List<ProviderConfiguration>
        {
            CreateTestConfiguration("provider-1", "id1"),
            CreateTestConfiguration("provider-2", "id2")
        }.AsReadOnly();

        _innerRepository.GetAllEnabledAsync(Arg.Any<CancellationToken>())
            .Returns(configs);

        var repository = CreateRepository();

        // Act
        var result = await repository.GetAllEnabledAsync();

        // Assert
        result.Should().HaveCount(2);

        // Verify inner repository was called
        await _innerRepository.Received(1).GetAllEnabledAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAllEnabledAsync_SecondCallWithinTtl_ReturnsCachedInstance()
    {
        // Arrange
        var configs = new List<ProviderConfiguration>
        {
            CreateTestConfiguration("cached-all-1", "id1"),
            CreateTestConfiguration("cached-all-2", "id2")
        }.AsReadOnly();

        _innerRepository.GetAllEnabledAsync(Arg.Any<CancellationToken>())
            .Returns(configs);

        var repository = CreateRepository(TimeSpan.FromMinutes(5));

        // Act - First call populates cache
        var firstResult = await repository.GetAllEnabledAsync();

        // Act - Second call should use cache
        var secondResult = await repository.GetAllEnabledAsync();

        // Assert
        firstResult.Should().BeSameAs(secondResult);

        // Inner repository should only be called once
        await _innerRepository.Received(1).GetAllEnabledAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetByProviderNameAsync_SecondCallWithinTtl_ReturnsCachedInstance()
    {
        // Arrange
        var config = CreateTestConfiguration("name-cached-provider", "id789");
        _innerRepository.GetByProviderNameAsync("name-cached-provider", Arg.Any<CancellationToken>())
            .Returns(config);

        var repository = CreateRepository(TimeSpan.FromMinutes(5));

        // Act - First call populates cache
        var firstResult = await repository.GetByProviderNameAsync("name-cached-provider");

        // Act - Second call should use cache
        var secondResult = await repository.GetByProviderNameAsync("name-cached-provider");

        // Assert
        firstResult.Should().BeSameAs(secondResult);

        // Inner repository should only be called once
        await _innerRepository.Received(1).GetByProviderNameAsync("name-cached-provider", Arg.Any<CancellationToken>());
    }

    #endregion

    #region Cache TTL Expiration Tests (T044)

    [Fact]
    public async Task GetByIdAsync_AfterTtlExpires_RefreshesFromInner()
    {
        // Arrange
        var shortTtl = TimeSpan.FromMilliseconds(50);
        var config = CreateTestConfiguration("ttl-expiry-provider", "ttl-id");

        _innerRepository.GetByIdAsync("ttl-id", Arg.Any<CancellationToken>())
            .Returns(config);

        var repository = CreateRepository(shortTtl);

        // Act - First call populates cache
        await repository.GetByIdAsync("ttl-id");

        // Wait for TTL to expire
        await Task.Delay(100);

        // Act - Third call should miss cache after TTL expiry
        await repository.GetByIdAsync("ttl-id");

        // Assert - Inner repository should be called twice (initial + after expiry)
        await _innerRepository.Received(2).GetByIdAsync("ttl-id", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAllEnabledAsync_AfterTtlExpires_RefreshesFromInner()
    {
        // Arrange
        var shortTtl = TimeSpan.FromMilliseconds(50);
        var configs = new List<ProviderConfiguration>
        {
            CreateTestConfiguration("ttl-all-1", "id1")
        }.AsReadOnly();

        _innerRepository.GetAllEnabledAsync(Arg.Any<CancellationToken>())
            .Returns(configs);

        var repository = CreateRepository(shortTtl);

        // Act - First call populates cache
        await repository.GetAllEnabledAsync();

        // Wait for TTL to expire
        await Task.Delay(100);

        // Act - Second call should miss cache after TTL expiry
        await repository.GetAllEnabledAsync();

        // Assert - Inner repository should be called twice (initial + after expiry)
        await _innerRepository.Received(2).GetAllEnabledAsync(Arg.Any<CancellationToken>());
    }

    #endregion

    #region Cache Invalidation Tests

    [Fact]
    public async Task AddAsync_InvalidatesAllEnabledCache()
    {
        // Arrange
        var existingConfigs = new List<ProviderConfiguration>
        {
            CreateTestConfiguration("existing-provider", "id1")
        }.AsReadOnly();

        var newConfig = CreateTestConfiguration("new-provider", "id2");

        _innerRepository.GetAllEnabledAsync(Arg.Any<CancellationToken>())
            .Returns(existingConfigs);
        _innerRepository.AddAsync(newConfig, Arg.Any<CancellationToken>())
            .Returns("id2");

        var repository = CreateRepository(TimeSpan.FromMinutes(5));

        // Populate cache
        await repository.GetAllEnabledAsync();

        // Act - Add new config should invalidate cache
        await repository.AddAsync(newConfig);

        // Act - Next call should miss cache
        await repository.GetAllEnabledAsync();

        // Assert - GetAllEnabledAsync should be called twice (initial + after add)
        await _innerRepository.Received(2).GetAllEnabledAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_InvalidatesCacheForEntity()
    {
        // Arrange
        var config = CreateTestConfiguration("update-cache-provider", "update-id");

        _innerRepository.GetByIdAsync("update-id", Arg.Any<CancellationToken>())
            .Returns(config);

        var repository = CreateRepository(TimeSpan.FromMinutes(5));

        // Populate cache
        await repository.GetByIdAsync("update-id");

        // Act - Update should invalidate cache
        await repository.UpdateAsync(config);

        // Act - Next call should miss cache
        await repository.GetByIdAsync("update-id");

        // Assert - GetByIdAsync should be called twice (initial + after update)
        await _innerRepository.Received(2).GetByIdAsync("update-id", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_InvalidatesCacheForEntity()
    {
        // Arrange
        var config = CreateTestConfiguration("delete-cache-provider", "delete-id");

        _innerRepository.GetByIdAsync("delete-id", Arg.Any<CancellationToken>())
            .Returns(config);

        var repository = CreateRepository(TimeSpan.FromMinutes(5));

        // Populate cache
        await repository.GetByIdAsync("delete-id");

        // Act - Delete should invalidate cache
        await repository.DeleteAsync("delete-id");

        // Reconfigure mock to return null (deleted)
        _innerRepository.GetByIdAsync("delete-id", Arg.Any<CancellationToken>())
            .Returns((ProviderConfiguration?)null);

        // Act - Next call should miss cache
        var result = await repository.GetByIdAsync("delete-id");

        // Assert
        result.Should().BeNull();
        await _innerRepository.Received(2).GetByIdAsync("delete-id", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClearCache_InvalidatesAllCaches()
    {
        // Arrange
        var config = CreateTestConfiguration("clear-cache-provider", "clear-id");
        var allConfigs = new List<ProviderConfiguration> { config }.AsReadOnly();

        _innerRepository.GetByIdAsync("clear-id", Arg.Any<CancellationToken>())
            .Returns(config);
        _innerRepository.GetAllEnabledAsync(Arg.Any<CancellationToken>())
            .Returns(allConfigs);

        var repository = CreateRepository(TimeSpan.FromMinutes(5));

        // Populate caches
        await repository.GetByIdAsync("clear-id");
        await repository.GetAllEnabledAsync();

        // Act - Clear cache
        repository.ClearCache();

        // Act - Next calls should miss cache
        await repository.GetAllEnabledAsync();

        // Assert - GetAllEnabledAsync should be called twice (initial + after clear)
        await _innerRepository.Received(2).GetAllEnabledAsync(Arg.Any<CancellationToken>());
    }

    #endregion

    #region Cache Disabled Tests

    [Fact]
    public async Task GetByIdAsync_WhenCacheDisabled_AlwaysFetchesFromInner()
    {
        // Arrange
        var config = CreateTestConfiguration("no-cache-provider", "no-cache-id");
        _innerRepository.GetByIdAsync("no-cache-id", Arg.Any<CancellationToken>())
            .Returns(config);

        var repository = CreateRepository(enabled: false);

        // Act - Multiple calls
        await repository.GetByIdAsync("no-cache-id");
        await repository.GetByIdAsync("no-cache-id");
        await repository.GetByIdAsync("no-cache-id");

        // Assert - Inner repository should be called each time
        await _innerRepository.Received(3).GetByIdAsync("no-cache-id", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAllEnabledAsync_WhenCacheDisabled_AlwaysFetchesFromInner()
    {
        // Arrange
        var configs = new List<ProviderConfiguration>
        {
            CreateTestConfiguration("no-cache-all", "id1")
        }.AsReadOnly();

        _innerRepository.GetAllEnabledAsync(Arg.Any<CancellationToken>())
            .Returns(configs);

        var repository = CreateRepository(enabled: false);

        // Act - Multiple calls
        await repository.GetAllEnabledAsync();
        await repository.GetAllEnabledAsync();

        // Assert - Inner repository should be called each time
        await _innerRepository.Received(2).GetAllEnabledAsync(Arg.Any<CancellationToken>());
    }

    #endregion

    #region ExistsByProviderNameAsync Tests

    [Fact]
    public async Task ExistsByProviderNameAsync_NeverCaches_AlwaysFetchesFromInner()
    {
        // Arrange
        _innerRepository.ExistsByProviderNameAsync("realtime-provider", Arg.Any<CancellationToken>())
            .Returns(true);

        var repository = CreateRepository(TimeSpan.FromMinutes(5));

        // Act - Multiple calls
        await repository.ExistsByProviderNameAsync("realtime-provider");
        await repository.ExistsByProviderNameAsync("realtime-provider");
        await repository.ExistsByProviderNameAsync("realtime-provider");

        // Assert - Inner repository should be called each time (not cached for uniqueness validation)
        await _innerRepository.Received(3).ExistsByProviderNameAsync("realtime-provider", Arg.Any<CancellationToken>());
    }

    #endregion

    #region Null Result Handling

    [Fact]
    public async Task GetByIdAsync_WhenInnerReturnsNull_DoesNotCacheNull()
    {
        // Arrange
        _innerRepository.GetByIdAsync("missing-id", Arg.Any<CancellationToken>())
            .Returns((ProviderConfiguration?)null);

        var repository = CreateRepository(TimeSpan.FromMinutes(5));

        // Act - Multiple calls
        await repository.GetByIdAsync("missing-id");
        await repository.GetByIdAsync("missing-id");

        // Assert - Inner repository should be called each time (null not cached)
        await _innerRepository.Received(2).GetByIdAsync("missing-id", Arg.Any<CancellationToken>());
    }

    #endregion
}
