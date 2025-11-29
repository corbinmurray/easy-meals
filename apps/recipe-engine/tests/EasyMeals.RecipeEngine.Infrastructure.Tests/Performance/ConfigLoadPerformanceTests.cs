using EasyMeals.Domain.ProviderConfiguration;
using EasyMeals.Persistence.Mongo.Indexes;
using EasyMeals.Persistence.Mongo.Repositories;
using EasyMeals.RecipeEngine.Infrastructure.Tests.Fixtures;
using FluentAssertions;
using System.Diagnostics;
using Xunit;

namespace EasyMeals.RecipeEngine.Infrastructure.Tests.Performance;

/// <summary>
/// Performance tests for provider configuration loading.
/// Validates that config load times meet p95 &lt; 100ms SLA.
/// </summary>
[Collection(MongoDbTestCollection.Name)]
public class ConfigLoadPerformanceTests : IAsyncLifetime
{
    private readonly MongoDbFixture _fixture;
    private readonly ProviderConfigurationRepository _repository;

    public ConfigLoadPerformanceTests(MongoDbFixture fixture)
    {
        _fixture = fixture;
        _repository = new ProviderConfigurationRepository(_fixture.Context);
    }

    public async Task InitializeAsync()
    {
        // Create indexes for optimal query performance
        await ProviderConfigurationIndexes.CreateIndexesAsync(_fixture.Database);

        // Seed test data
        for (int i = 0; i < 10; i++)
        {
            var config = CreateTestConfiguration($"perf-test-provider-{i}", priority: i * 10);
            await _repository.AddAsync(config);
        }
    }

    public async Task DisposeAsync()
    {
        await _fixture.Database.DropCollectionAsync("provider_configurations");
    }

    #region Test Fixtures

    private static ProviderConfiguration CreateTestConfiguration(string providerName, int priority = 0)
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

        return ProviderConfiguration.Create(
            providerName: providerName,
            displayName: $"Perf Test Provider {providerName}",
            baseUrl: "https://example.com",
            discoveryStrategy: DiscoveryStrategy.Crawl,
            fetchingStrategy: FetchingStrategy.StaticHtml,
            extractionSelectors: selectors,
            rateLimitSettings: rateLimits,
            priority: priority,
            crawlSettings: crawlSettings);
    }

    #endregion

    [Fact]
    public async Task GetAllEnabledAsync_P95LoadTime_ShouldBeLessThan100ms()
    {
        // Arrange
        const int iterations = 100;
        var loadTimes = new List<double>(iterations);

        // Warm up
        await _repository.GetAllEnabledAsync();

        // Act - Measure load times
        for (int i = 0; i < iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            await _repository.GetAllEnabledAsync();
            sw.Stop();
            loadTimes.Add(sw.Elapsed.TotalMilliseconds);
        }

        // Calculate p95
        loadTimes.Sort();
        var p95Index = (int)Math.Ceiling(iterations * 0.95) - 1;
        var p95 = loadTimes[p95Index];

        // Assert
        p95.Should().BeLessThan(100, $"p95 load time was {p95:F2}ms, which exceeds the 100ms SLA");
    }

    [Fact]
    public async Task GetByIdAsync_P95LoadTime_ShouldBeLessThan50ms()
    {
        // Arrange
        var config = CreateTestConfiguration("perf-getbyid-provider");
        var id = await _repository.AddAsync(config);

        const int iterations = 100;
        var loadTimes = new List<double>(iterations);

        // Warm up
        await _repository.GetByIdAsync(id);

        // Act - Measure load times
        for (int i = 0; i < iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            await _repository.GetByIdAsync(id);
            sw.Stop();
            loadTimes.Add(sw.Elapsed.TotalMilliseconds);
        }

        // Calculate p95
        loadTimes.Sort();
        var p95Index = (int)Math.Ceiling(iterations * 0.95) - 1;
        var p95 = loadTimes[p95Index];

        // Assert
        p95.Should().BeLessThan(50, $"p95 load time was {p95:F2}ms, which exceeds the 50ms SLA");
    }

    [Fact]
    public async Task GetByProviderNameAsync_P95LoadTime_ShouldBeLessThan50ms()
    {
        // Arrange
        const int iterations = 100;
        var loadTimes = new List<double>(iterations);

        // Warm up
        await _repository.GetByProviderNameAsync("perf-test-provider-0");

        // Act - Measure load times
        for (int i = 0; i < iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            await _repository.GetByProviderNameAsync("perf-test-provider-0");
            sw.Stop();
            loadTimes.Add(sw.Elapsed.TotalMilliseconds);
        }

        // Calculate p95
        loadTimes.Sort();
        var p95Index = (int)Math.Ceiling(iterations * 0.95) - 1;
        var p95 = loadTimes[p95Index];

        // Assert
        p95.Should().BeLessThan(50, $"p95 load time was {p95:F2}ms, which exceeds the 50ms SLA");
    }
}
