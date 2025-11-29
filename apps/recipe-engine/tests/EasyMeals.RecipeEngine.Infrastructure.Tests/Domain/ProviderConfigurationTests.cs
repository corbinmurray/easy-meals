using EasyMeals.Domain.ProviderConfiguration;
using FluentAssertions;
using Xunit;

namespace EasyMeals.RecipeEngine.Infrastructure.Tests.Domain;

/// <summary>
/// Unit tests for <see cref="ProviderConfiguration"/> aggregate root validation and behavior.
/// </summary>
public class ProviderConfigurationTests
{
    #region Test Fixtures

    private static ExtractionSelectors CreateValidSelectors() => new(
        titleSelector: ".recipe-title",
        descriptionSelector: ".recipe-description",
        ingredientsSelector: ".ingredients li",
        instructionsSelector: ".instructions ol li");

    private static RateLimitSettings CreateValidRateLimits() => RateLimitSettings.Default;

    private static ApiSettings CreateValidApiSettings() => new(
        endpoint: "/api/recipes",
        authMethod: AuthMethod.ApiKey,
        headers: new Dictionary<string, string> { ["X-Api-Key"] = "secret:provider-api-key" });

    private static CrawlSettings CreateValidCrawlSettings() => new(
        seedUrls: new List<string> { "https://example.com/recipes" }.AsReadOnly(),
        includePatterns: new List<string> { "/recipes/*" }.AsReadOnly(),
        excludePatterns: new List<string>().AsReadOnly(),
        maxDepth: 3,
        linkSelector: "a.recipe-link");

    #endregion

    #region Create() - Success Cases

    [Fact]
    public void Create_WithValidCrawlConfiguration_ReturnsProviderConfiguration()
    {
        // Arrange
        var selectors = CreateValidSelectors();
        var rateLimits = CreateValidRateLimits();
        var crawlSettings = CreateValidCrawlSettings();

        // Act
        var config = ProviderConfiguration.Create(
            providerName: "test-provider",
            displayName: "Test Provider",
            baseUrl: "https://example.com",
            discoveryStrategy: DiscoveryStrategy.Crawl,
            fetchingStrategy: FetchingStrategy.StaticHtml,
            extractionSelectors: selectors,
            rateLimitSettings: rateLimits,
            crawlSettings: crawlSettings);

        // Assert
        config.Should().NotBeNull();
        config.ProviderName.Should().Be("test-provider");
        config.DisplayName.Should().Be("Test Provider");
        config.BaseUrl.Should().Be("https://example.com");
        config.DiscoveryStrategy.Should().Be(DiscoveryStrategy.Crawl);
        config.FetchingStrategy.Should().Be(FetchingStrategy.StaticHtml);
        config.IsEnabled.Should().BeTrue();
        config.Priority.Should().Be(0);
        config.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        config.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        config.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void Create_WithValidApiConfiguration_ReturnsProviderConfiguration()
    {
        // Arrange
        var selectors = CreateValidSelectors();
        var rateLimits = CreateValidRateLimits();
        var apiSettings = CreateValidApiSettings();

        // Act
        var config = ProviderConfiguration.Create(
            providerName: "api-provider",
            displayName: "API Provider",
            baseUrl: "https://api.example.com",
            discoveryStrategy: DiscoveryStrategy.Api,
            fetchingStrategy: FetchingStrategy.Api,
            extractionSelectors: selectors,
            rateLimitSettings: rateLimits,
            apiSettings: apiSettings);

        // Assert
        config.Should().NotBeNull();
        config.ProviderName.Should().Be("api-provider");
        config.DiscoveryStrategy.Should().Be(DiscoveryStrategy.Api);
        config.FetchingStrategy.Should().Be(FetchingStrategy.Api);
        config.ApiSettings.Should().NotBeNull();
    }

    [Fact]
    public void Create_WithPriority_SetsPriorityCorrectly()
    {
        // Arrange
        var selectors = CreateValidSelectors();
        var rateLimits = CreateValidRateLimits();
        var crawlSettings = CreateValidCrawlSettings();

        // Act
        var config = ProviderConfiguration.Create(
            providerName: "priority-provider",
            displayName: "Priority Provider",
            baseUrl: "https://example.com",
            discoveryStrategy: DiscoveryStrategy.Crawl,
            fetchingStrategy: FetchingStrategy.StaticHtml,
            extractionSelectors: selectors,
            rateLimitSettings: rateLimits,
            priority: 100,
            crawlSettings: crawlSettings);

        // Assert
        config.Priority.Should().Be(100);
    }

    [Theory]
    [InlineData("HELLO-FRESH", "hello-fresh")]
    [InlineData("AllRecipes", "allrecipes")]
    [InlineData("  test-provider  ", "test-provider")]
    [InlineData("TEST123", "test123")]
    public void Create_NormalizesProviderNameToLowercase(string input, string expected)
    {
        // Arrange
        var selectors = CreateValidSelectors();
        var rateLimits = CreateValidRateLimits();
        var crawlSettings = CreateValidCrawlSettings();

        // Act
        var config = ProviderConfiguration.Create(
            providerName: input,
            displayName: "Test",
            baseUrl: "https://example.com",
            discoveryStrategy: DiscoveryStrategy.Crawl,
            fetchingStrategy: FetchingStrategy.StaticHtml,
            extractionSelectors: selectors,
            rateLimitSettings: rateLimits,
            crawlSettings: crawlSettings);

        // Assert
        config.ProviderName.Should().Be(expected);
    }

    #endregion

    #region Create() - Validation Failure Cases

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithNullOrEmptyProviderName_ThrowsArgumentException(string? providerName)
    {
        // Arrange
        var selectors = CreateValidSelectors();
        var rateLimits = CreateValidRateLimits();
        var crawlSettings = CreateValidCrawlSettings();

        // Act
        var act = () => ProviderConfiguration.Create(
            providerName: providerName!,
            displayName: "Test",
            baseUrl: "https://example.com",
            discoveryStrategy: DiscoveryStrategy.Crawl,
            fetchingStrategy: FetchingStrategy.StaticHtml,
            extractionSelectors: selectors,
            rateLimitSettings: rateLimits,
            crawlSettings: crawlSettings);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("hello_fresh")]  // Underscores not allowed
    [InlineData("hello fresh")]  // Spaces not allowed
    [InlineData("hello.fresh")]  // Dots not allowed
    [InlineData("HELLO@FRESH")]  // Special chars not allowed
    public void Create_WithInvalidProviderNameFormat_ThrowsArgumentException(string providerName)
    {
        // Arrange
        var selectors = CreateValidSelectors();
        var rateLimits = CreateValidRateLimits();
        var crawlSettings = CreateValidCrawlSettings();

        // Act
        var act = () => ProviderConfiguration.Create(
            providerName: providerName,
            displayName: "Test",
            baseUrl: "https://example.com",
            discoveryStrategy: DiscoveryStrategy.Crawl,
            fetchingStrategy: FetchingStrategy.StaticHtml,
            extractionSelectors: selectors,
            rateLimitSettings: rateLimits,
            crawlSettings: crawlSettings);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*ProviderName must be lowercase alphanumeric with hyphens only*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithNullOrEmptyDisplayName_ThrowsArgumentException(string? displayName)
    {
        // Arrange
        var selectors = CreateValidSelectors();
        var rateLimits = CreateValidRateLimits();
        var crawlSettings = CreateValidCrawlSettings();

        // Act
        var act = () => ProviderConfiguration.Create(
            providerName: "test-provider",
            displayName: displayName!,
            baseUrl: "https://example.com",
            discoveryStrategy: DiscoveryStrategy.Crawl,
            fetchingStrategy: FetchingStrategy.StaticHtml,
            extractionSelectors: selectors,
            rateLimitSettings: rateLimits,
            crawlSettings: crawlSettings);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*DisplayName is required*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-url")]
    [InlineData("ftp://example.com")]  // Not HTTP(S)
    [InlineData("file:///path/to/file")]  // Not HTTP(S)
    public void Create_WithInvalidBaseUrl_ThrowsArgumentException(string? baseUrl)
    {
        // Arrange
        var selectors = CreateValidSelectors();
        var rateLimits = CreateValidRateLimits();
        var crawlSettings = CreateValidCrawlSettings();

        // Act
        var act = () => ProviderConfiguration.Create(
            providerName: "test-provider",
            displayName: "Test Provider",
            baseUrl: baseUrl!,
            discoveryStrategy: DiscoveryStrategy.Crawl,
            fetchingStrategy: FetchingStrategy.StaticHtml,
            extractionSelectors: selectors,
            rateLimitSettings: rateLimits,
            crawlSettings: crawlSettings);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*BaseUrl*");
    }

    [Fact]
    public void Create_WithNegativePriority_ThrowsArgumentException()
    {
        // Arrange
        var selectors = CreateValidSelectors();
        var rateLimits = CreateValidRateLimits();
        var crawlSettings = CreateValidCrawlSettings();

        // Act
        var act = () => ProviderConfiguration.Create(
            providerName: "test-provider",
            displayName: "Test Provider",
            baseUrl: "https://example.com",
            discoveryStrategy: DiscoveryStrategy.Crawl,
            fetchingStrategy: FetchingStrategy.StaticHtml,
            extractionSelectors: selectors,
            rateLimitSettings: rateLimits,
            priority: -1,
            crawlSettings: crawlSettings);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Priority must be >= 0*");
    }

    [Fact]
    public void Create_WithApiStrategyButNoApiSettings_ThrowsArgumentException()
    {
        // Arrange
        var selectors = CreateValidSelectors();
        var rateLimits = CreateValidRateLimits();

        // Act
        var act = () => ProviderConfiguration.Create(
            providerName: "api-provider",
            displayName: "API Provider",
            baseUrl: "https://api.example.com",
            discoveryStrategy: DiscoveryStrategy.Api,
            fetchingStrategy: FetchingStrategy.StaticHtml,
            extractionSelectors: selectors,
            rateLimitSettings: rateLimits);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*ApiSettings is required when using Api strategy*");
    }

    [Fact]
    public void Create_WithApiFetchingStrategyButNoApiSettings_ThrowsArgumentException()
    {
        // Arrange
        var selectors = CreateValidSelectors();
        var rateLimits = CreateValidRateLimits();
        var crawlSettings = CreateValidCrawlSettings();

        // Act
        var act = () => ProviderConfiguration.Create(
            providerName: "mixed-provider",
            displayName: "Mixed Provider",
            baseUrl: "https://example.com",
            discoveryStrategy: DiscoveryStrategy.Crawl,
            fetchingStrategy: FetchingStrategy.Api,
            extractionSelectors: selectors,
            rateLimitSettings: rateLimits,
            crawlSettings: crawlSettings);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*ApiSettings is required when using Api strategy*");
    }

    [Fact]
    public void Create_WithCrawlStrategyButNoCrawlSettings_ThrowsArgumentException()
    {
        // Arrange
        var selectors = CreateValidSelectors();
        var rateLimits = CreateValidRateLimits();

        // Act
        var act = () => ProviderConfiguration.Create(
            providerName: "crawl-provider",
            displayName: "Crawl Provider",
            baseUrl: "https://example.com",
            discoveryStrategy: DiscoveryStrategy.Crawl,
            fetchingStrategy: FetchingStrategy.StaticHtml,
            extractionSelectors: selectors,
            rateLimitSettings: rateLimits);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*CrawlSettings is required when using Crawl discovery strategy*");
    }

    [Fact]
    public void Create_WithInvalidCssSelector_ThrowsArgumentException()
    {
        // Arrange
        var invalidSelectors = new ExtractionSelectors(
            titleSelector: "[invalid[",  // Invalid CSS selector
            descriptionSelector: ".valid",
            ingredientsSelector: ".valid",
            instructionsSelector: ".valid");
        var rateLimits = CreateValidRateLimits();
        var crawlSettings = CreateValidCrawlSettings();

        // Act
        var act = () => ProviderConfiguration.Create(
            providerName: "test-provider",
            displayName: "Test Provider",
            baseUrl: "https://example.com",
            discoveryStrategy: DiscoveryStrategy.Crawl,
            fetchingStrategy: FetchingStrategy.StaticHtml,
            extractionSelectors: invalidSelectors,
            rateLimitSettings: rateLimits,
            crawlSettings: crawlSettings);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Invalid CSS selector*");
    }

    #endregion

    #region Validate() Method

    [Fact]
    public void Validate_WithValidConfiguration_ReturnsValidResult()
    {
        // Arrange
        var config = ProviderConfiguration.Create(
            providerName: "test-provider",
            displayName: "Test Provider",
            baseUrl: "https://example.com",
            discoveryStrategy: DiscoveryStrategy.Crawl,
            fetchingStrategy: FetchingStrategy.StaticHtml,
            extractionSelectors: CreateValidSelectors(),
            rateLimitSettings: CreateValidRateLimits(),
            crawlSettings: CreateValidCrawlSettings());

        // Act
        var result = config.Validate();

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    #endregion

    #region Enable() and Disable()

    [Fact]
    public void Enable_SetsIsEnabledToTrue()
    {
        // Arrange
        var config = ProviderConfiguration.Create(
            providerName: "test-provider",
            displayName: "Test Provider",
            baseUrl: "https://example.com",
            discoveryStrategy: DiscoveryStrategy.Crawl,
            fetchingStrategy: FetchingStrategy.StaticHtml,
            extractionSelectors: CreateValidSelectors(),
            rateLimitSettings: CreateValidRateLimits(),
            crawlSettings: CreateValidCrawlSettings());
        config.Disable();
        var previousUpdatedAt = config.UpdatedAt;

        // Act
        Thread.Sleep(10);
        config.Enable();

        // Assert
        config.IsEnabled.Should().BeTrue();
        config.UpdatedAt.Should().BeAfter(previousUpdatedAt);
    }

    [Fact]
    public void Disable_SetsIsEnabledToFalse()
    {
        // Arrange
        var config = ProviderConfiguration.Create(
            providerName: "test-provider",
            displayName: "Test Provider",
            baseUrl: "https://example.com",
            discoveryStrategy: DiscoveryStrategy.Crawl,
            fetchingStrategy: FetchingStrategy.StaticHtml,
            extractionSelectors: CreateValidSelectors(),
            rateLimitSettings: CreateValidRateLimits(),
            crawlSettings: CreateValidCrawlSettings());
        var previousUpdatedAt = config.UpdatedAt;

        // Act
        Thread.Sleep(10);
        config.Disable();

        // Assert
        config.IsEnabled.Should().BeFalse();
        config.UpdatedAt.Should().BeAfter(previousUpdatedAt);
    }

    #endregion

    #region UpdateSelectors()

    [Fact]
    public void UpdateSelectors_WithValidSelectors_UpdatesSelectorsAndTimestamp()
    {
        // Arrange
        var config = ProviderConfiguration.Create(
            providerName: "test-provider",
            displayName: "Test Provider",
            baseUrl: "https://example.com",
            discoveryStrategy: DiscoveryStrategy.Crawl,
            fetchingStrategy: FetchingStrategy.StaticHtml,
            extractionSelectors: CreateValidSelectors(),
            rateLimitSettings: CreateValidRateLimits(),
            crawlSettings: CreateValidCrawlSettings());

        var newSelectors = new ExtractionSelectors(
            titleSelector: "h1.new-title",
            descriptionSelector: "p.new-description",
            ingredientsSelector: "ul.new-ingredients li",
            instructionsSelector: "ol.new-instructions li");

        var previousUpdatedAt = config.UpdatedAt;

        // Act
        Thread.Sleep(10);
        config.UpdateSelectors(newSelectors);

        // Assert
        config.ExtractionSelectors.TitleSelector.Should().Be("h1.new-title");
        config.UpdatedAt.Should().BeAfter(previousUpdatedAt);
    }

    [Fact]
    public void UpdateSelectors_WithNullSelectors_ThrowsArgumentNullException()
    {
        // Arrange
        var config = ProviderConfiguration.Create(
            providerName: "test-provider",
            displayName: "Test Provider",
            baseUrl: "https://example.com",
            discoveryStrategy: DiscoveryStrategy.Crawl,
            fetchingStrategy: FetchingStrategy.StaticHtml,
            extractionSelectors: CreateValidSelectors(),
            rateLimitSettings: CreateValidRateLimits(),
            crawlSettings: CreateValidCrawlSettings());

        // Act
        var act = () => config.UpdateSelectors(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void UpdateSelectors_WithInvalidSelector_ThrowsArgumentException()
    {
        // Arrange
        var config = ProviderConfiguration.Create(
            providerName: "test-provider",
            displayName: "Test Provider",
            baseUrl: "https://example.com",
            discoveryStrategy: DiscoveryStrategy.Crawl,
            fetchingStrategy: FetchingStrategy.StaticHtml,
            extractionSelectors: CreateValidSelectors(),
            rateLimitSettings: CreateValidRateLimits(),
            crawlSettings: CreateValidCrawlSettings());

        var invalidSelectors = new ExtractionSelectors(
            titleSelector: "[invalid[",
            descriptionSelector: ".valid",
            ingredientsSelector: ".valid",
            instructionsSelector: ".valid");

        // Act
        var act = () => config.UpdateSelectors(invalidSelectors);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Invalid CSS selector*");
    }

    #endregion

    #region UpdateRateLimits()

    [Fact]
    public void UpdateRateLimits_WithValidSettings_UpdatesSettingsAndTimestamp()
    {
        // Arrange
        var config = ProviderConfiguration.Create(
            providerName: "test-provider",
            displayName: "Test Provider",
            baseUrl: "https://example.com",
            discoveryStrategy: DiscoveryStrategy.Crawl,
            fetchingStrategy: FetchingStrategy.StaticHtml,
            extractionSelectors: CreateValidSelectors(),
            rateLimitSettings: CreateValidRateLimits(),
            crawlSettings: CreateValidCrawlSettings());

        var newRateLimits = RateLimitSettings.Conservative;
        var previousUpdatedAt = config.UpdatedAt;

        // Act
        Thread.Sleep(10);
        config.UpdateRateLimits(newRateLimits);

        // Assert
        config.RateLimitSettings.RequestsPerMinute.Should().Be(30);
        config.UpdatedAt.Should().BeAfter(previousUpdatedAt);
    }

    [Fact]
    public void UpdateRateLimits_WithNullSettings_ThrowsArgumentNullException()
    {
        // Arrange
        var config = ProviderConfiguration.Create(
            providerName: "test-provider",
            displayName: "Test Provider",
            baseUrl: "https://example.com",
            discoveryStrategy: DiscoveryStrategy.Crawl,
            fetchingStrategy: FetchingStrategy.StaticHtml,
            extractionSelectors: CreateValidSelectors(),
            rateLimitSettings: CreateValidRateLimits(),
            crawlSettings: CreateValidCrawlSettings());

        // Act
        var act = () => config.UpdateRateLimits(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion
}
