using EasyMeals.Domain.ProviderConfiguration;
using FluentAssertions;
using Xunit;

namespace EasyMeals.RecipeEngine.Infrastructure.Tests.Domain;

/// <summary>
/// Unit tests for value object behavior: equality, immutability, and validation.
/// </summary>
public class ValueObjectTests
{
    #region ExtractionSelectors Tests

    [Fact]
    public void ExtractionSelectors_Constructor_SetsAllProperties()
    {
        // Arrange & Act
        var selectors = new ExtractionSelectors(
            titleSelector: ".title",
            descriptionSelector: ".description",
            ingredientsSelector: ".ingredients li",
            instructionsSelector: ".instructions li",
            titleFallbackSelector: "h1",
            descriptionFallbackSelector: "meta[name='description']",
            prepTimeSelector: ".prep-time",
            cookTimeSelector: ".cook-time",
            totalTimeSelector: ".total-time",
            servingsSelector: ".servings",
            imageUrlSelector: "img.recipe-image",
            authorSelector: ".author",
            cuisineSelector: ".cuisine",
            difficultySelector: ".difficulty",
            nutritionSelector: ".nutrition");

        // Assert
        selectors.TitleSelector.Should().Be(".title");
        selectors.DescriptionSelector.Should().Be(".description");
        selectors.IngredientsSelector.Should().Be(".ingredients li");
        selectors.InstructionsSelector.Should().Be(".instructions li");
        selectors.TitleFallbackSelector.Should().Be("h1");
        selectors.DescriptionFallbackSelector.Should().Be("meta[name='description']");
        selectors.PrepTimeSelector.Should().Be(".prep-time");
        selectors.CookTimeSelector.Should().Be(".cook-time");
        selectors.TotalTimeSelector.Should().Be(".total-time");
        selectors.ServingsSelector.Should().Be(".servings");
        selectors.ImageUrlSelector.Should().Be("img.recipe-image");
        selectors.AuthorSelector.Should().Be(".author");
        selectors.CuisineSelector.Should().Be(".cuisine");
        selectors.DifficultySelector.Should().Be(".difficulty");
        selectors.NutritionSelector.Should().Be(".nutrition");
    }

    [Fact]
    public void ExtractionSelectors_Constructor_WithRequiredSelectorsOnly_SetsOptionalToNull()
    {
        // Arrange & Act
        var selectors = new ExtractionSelectors(
            titleSelector: ".title",
            descriptionSelector: ".description",
            ingredientsSelector: ".ingredients",
            instructionsSelector: ".instructions");

        // Assert
        selectors.TitleSelector.Should().Be(".title");
        selectors.TitleFallbackSelector.Should().BeNull();
        selectors.PrepTimeSelector.Should().BeNull();
        selectors.CookTimeSelector.Should().BeNull();
        selectors.TotalTimeSelector.Should().BeNull();
        selectors.ServingsSelector.Should().BeNull();
        selectors.ImageUrlSelector.Should().BeNull();
        selectors.AuthorSelector.Should().BeNull();
        selectors.CuisineSelector.Should().BeNull();
        selectors.DifficultySelector.Should().BeNull();
        selectors.NutritionSelector.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    public void ExtractionSelectors_Constructor_WithNullTitleSelector_ThrowsArgumentNullException(string? titleSelector)
    {
        // Act
        var act = () => new ExtractionSelectors(
            titleSelector: titleSelector!,
            descriptionSelector: ".description",
            ingredientsSelector: ".ingredients",
            instructionsSelector: ".instructions");

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("titleSelector");
    }

    [Theory]
    [InlineData(null)]
    public void ExtractionSelectors_Constructor_WithNullDescriptionSelector_ThrowsArgumentNullException(string? descriptionSelector)
    {
        // Act
        var act = () => new ExtractionSelectors(
            titleSelector: ".title",
            descriptionSelector: descriptionSelector!,
            ingredientsSelector: ".ingredients",
            instructionsSelector: ".instructions");

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("descriptionSelector");
    }

    [Theory]
    [InlineData(null)]
    public void ExtractionSelectors_Constructor_WithNullIngredientsSelector_ThrowsArgumentNullException(string? ingredientsSelector)
    {
        // Act
        var act = () => new ExtractionSelectors(
            titleSelector: ".title",
            descriptionSelector: ".description",
            ingredientsSelector: ingredientsSelector!,
            instructionsSelector: ".instructions");

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("ingredientsSelector");
    }

    [Theory]
    [InlineData(null)]
    public void ExtractionSelectors_Constructor_WithNullInstructionsSelector_ThrowsArgumentNullException(string? instructionsSelector)
    {
        // Act
        var act = () => new ExtractionSelectors(
            titleSelector: ".title",
            descriptionSelector: ".description",
            ingredientsSelector: ".ingredients",
            instructionsSelector: instructionsSelector!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("instructionsSelector");
    }

    [Fact]
    public void ExtractionSelectors_GetAllSelectors_ReturnsRequiredSelectorsOnly_WhenOptionalAreNull()
    {
        // Arrange
        var selectors = new ExtractionSelectors(
            titleSelector: ".title",
            descriptionSelector: ".description",
            ingredientsSelector: ".ingredients",
            instructionsSelector: ".instructions");

        // Act
        var allSelectors = selectors.GetAllSelectors().ToList();

        // Assert
        allSelectors.Should().HaveCount(4);
        allSelectors.Should().Contain(".title");
        allSelectors.Should().Contain(".description");
        allSelectors.Should().Contain(".ingredients");
        allSelectors.Should().Contain(".instructions");
    }

    [Fact]
    public void ExtractionSelectors_GetAllSelectors_ReturnsAllSelectorsIncludingOptional()
    {
        // Arrange
        var selectors = new ExtractionSelectors(
            titleSelector: ".title",
            descriptionSelector: ".description",
            ingredientsSelector: ".ingredients",
            instructionsSelector: ".instructions",
            titleFallbackSelector: "h1",
            prepTimeSelector: ".prep-time");

        // Act
        var allSelectors = selectors.GetAllSelectors().ToList();

        // Assert
        allSelectors.Should().HaveCount(6);
        allSelectors.Should().Contain("h1");
        allSelectors.Should().Contain(".prep-time");
    }

    #endregion

    #region RateLimitSettings Tests

    [Fact]
    public void RateLimitSettings_Constructor_SetsAllProperties()
    {
        // Arrange & Act
        var settings = new RateLimitSettings(
            requestsPerMinute: 100,
            delayBetweenRequests: TimeSpan.FromMilliseconds(200),
            maxConcurrentRequests: 10,
            maxRetries: 5,
            retryDelay: TimeSpan.FromSeconds(2));

        // Assert
        settings.RequestsPerMinute.Should().Be(100);
        settings.DelayBetweenRequests.Should().Be(TimeSpan.FromMilliseconds(200));
        settings.MaxConcurrentRequests.Should().Be(10);
        settings.MaxRetries.Should().Be(5);
        settings.RetryDelay.Should().Be(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void RateLimitSettings_Constructor_WithDefaults_UsesDefaultValues()
    {
        // Arrange & Act
        var settings = new RateLimitSettings();

        // Assert
        settings.RequestsPerMinute.Should().Be(60);
        settings.DelayBetweenRequests.Should().Be(TimeSpan.FromMilliseconds(100));
        settings.MaxConcurrentRequests.Should().Be(5);
        settings.MaxRetries.Should().Be(3);
        settings.RetryDelay.Should().Be(TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void RateLimitSettings_Constructor_WithInvalidRequestsPerMinute_ThrowsArgumentOutOfRangeException(int requestsPerMinute)
    {
        // Act
        var act = () => new RateLimitSettings(requestsPerMinute: requestsPerMinute);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("requestsPerMinute");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void RateLimitSettings_Constructor_WithInvalidMaxConcurrentRequests_ThrowsArgumentOutOfRangeException(int maxConcurrent)
    {
        // Act
        var act = () => new RateLimitSettings(maxConcurrentRequests: maxConcurrent);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("maxConcurrentRequests");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    public void RateLimitSettings_Constructor_WithNegativeMaxRetries_ThrowsArgumentOutOfRangeException(int maxRetries)
    {
        // Act
        var act = () => new RateLimitSettings(maxRetries: maxRetries);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("maxRetries");
    }

    [Fact]
    public void RateLimitSettings_Constructor_WithZeroMaxRetries_IsValid()
    {
        // Arrange & Act
        var settings = new RateLimitSettings(maxRetries: 0);

        // Assert
        settings.MaxRetries.Should().Be(0);
    }

    [Fact]
    public void RateLimitSettings_Default_ReturnsExpectedValues()
    {
        // Act
        var settings = RateLimitSettings.Default;

        // Assert
        settings.RequestsPerMinute.Should().Be(60);
        settings.DelayBetweenRequests.Should().Be(TimeSpan.FromMilliseconds(100));
        settings.MaxConcurrentRequests.Should().Be(5);
        settings.MaxRetries.Should().Be(3);
        settings.RetryDelay.Should().Be(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void RateLimitSettings_Conservative_ReturnsExpectedValues()
    {
        // Act
        var settings = RateLimitSettings.Conservative;

        // Assert
        settings.RequestsPerMinute.Should().Be(30);
        settings.DelayBetweenRequests.Should().Be(TimeSpan.FromMilliseconds(500));
        settings.MaxConcurrentRequests.Should().Be(2);
        settings.MaxRetries.Should().Be(3);
        settings.RetryDelay.Should().Be(TimeSpan.FromSeconds(2));
    }

    #endregion

    #region ApiSettings Tests

    [Fact]
    public void ApiSettings_Constructor_SetsAllProperties()
    {
        // Arrange
        var headers = new Dictionary<string, string> { ["X-Api-Key"] = "secret:api-key" };

        // Act
        var settings = new ApiSettings(
            endpoint: "/api/recipes",
            authMethod: AuthMethod.ApiKey,
            headers: headers,
            pageSizeParam: "limit",
            pageNumberParam: "page",
            defaultPageSize: 50);

        // Assert
        settings.Endpoint.Should().Be("/api/recipes");
        settings.AuthMethod.Should().Be(AuthMethod.ApiKey);
        settings.Headers.Should().Contain("X-Api-Key", "secret:api-key");
        settings.PageSizeParam.Should().Be("limit");
        settings.PageNumberParam.Should().Be("page");
        settings.DefaultPageSize.Should().Be(50);
    }

    [Fact]
    public void ApiSettings_Constructor_WithDefaults_UsesDefaultValues()
    {
        // Act
        var settings = new ApiSettings(endpoint: "/api/recipes");

        // Assert
        settings.Endpoint.Should().Be("/api/recipes");
        settings.AuthMethod.Should().Be(AuthMethod.None);
        settings.Headers.Should().BeEmpty();
        settings.PageSizeParam.Should().BeNull();
        settings.PageNumberParam.Should().BeNull();
        settings.DefaultPageSize.Should().Be(20);
    }

    [Fact]
    public void ApiSettings_Constructor_WithNullEndpoint_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new ApiSettings(endpoint: null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("endpoint");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void ApiSettings_Constructor_WithInvalidDefaultPageSize_ThrowsArgumentOutOfRangeException(int pageSize)
    {
        // Act
        var act = () => new ApiSettings(endpoint: "/api", defaultPageSize: pageSize);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("defaultPageSize");
    }

    [Fact]
    public void ApiSettings_HasValidSecretReferences_WithValidSecrets_ReturnsTrue()
    {
        // Arrange
        var headers = new Dictionary<string, string>
        {
            ["X-Api-Key"] = "secret:provider-api-key",
            ["Authorization"] = "secret:provider-bearer-token",
            ["Accept"] = "application/json"  // Non-secret header
        };
        var settings = new ApiSettings(endpoint: "/api", headers: headers);

        // Act
        var result = settings.HasValidSecretReferences();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ApiSettings_HasValidSecretReferences_WithEmptySecretName_ReturnsFalse()
    {
        // Arrange - Secret reference with no name after "secret:"
        var headers = new Dictionary<string, string>
        {
            ["X-Api-Key"] = "secret:"
        };
        var settings = new ApiSettings(endpoint: "/api", headers: headers);

        // Act
        var result = settings.HasValidSecretReferences();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ApiSettings_HasValidSecretReferences_WithWhitespaceSecretName_ReturnsFalse()
    {
        // Arrange - Secret reference with only whitespace after "secret:"
        var headers = new Dictionary<string, string>
        {
            ["X-Api-Key"] = "secret:   "
        };
        var settings = new ApiSettings(endpoint: "/api", headers: headers);

        // Act
        var result = settings.HasValidSecretReferences();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ApiSettings_HasValidSecretReferences_WithNoSecrets_ReturnsTrue()
    {
        // Arrange - No secret references, just regular headers
        var headers = new Dictionary<string, string>
        {
            ["Accept"] = "application/json",
            ["Content-Type"] = "application/json"
        };
        var settings = new ApiSettings(endpoint: "/api", headers: headers);

        // Act
        var result = settings.HasValidSecretReferences();

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region CrawlSettings Tests

    [Fact]
    public void CrawlSettings_Constructor_SetsAllProperties()
    {
        // Arrange
        var seedUrls = new List<string> { "https://example.com/recipes", "https://example.com/recipes/page2" };
        var includePatterns = new List<string> { @"/recipes/\d+" };
        var excludePatterns = new List<string> { @"/login", @"/signup" };

        // Act
        var settings = new CrawlSettings(
            seedUrls: seedUrls.AsReadOnly(),
            includePatterns: includePatterns.AsReadOnly(),
            excludePatterns: excludePatterns.AsReadOnly(),
            maxDepth: 5,
            linkSelector: "a.recipe-link");

        // Assert
        settings.SeedUrls.Should().HaveCount(2);
        settings.IncludePatterns.Should().HaveCount(1);
        settings.ExcludePatterns.Should().HaveCount(2);
        settings.MaxDepth.Should().Be(5);
        settings.LinkSelector.Should().Be("a.recipe-link");
    }

    [Fact]
    public void CrawlSettings_Constructor_WithDefaults_UsesDefaultValues()
    {
        // Arrange
        var seedUrls = new List<string> { "https://example.com" };

        // Act
        var settings = new CrawlSettings(seedUrls: seedUrls.AsReadOnly());

        // Assert
        settings.SeedUrls.Should().HaveCount(1);
        settings.IncludePatterns.Should().BeEmpty();
        settings.ExcludePatterns.Should().BeEmpty();
        settings.MaxDepth.Should().Be(3);
        settings.LinkSelector.Should().Be("a[href]");
    }

    [Fact]
    public void CrawlSettings_Constructor_WithNullSeedUrls_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new CrawlSettings(seedUrls: null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("seedUrls");
    }

    [Fact]
    public void CrawlSettings_Constructor_WithNullLinkSelector_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new CrawlSettings(
            seedUrls: new List<string> { "https://example.com" }.AsReadOnly(),
            linkSelector: null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("linkSelector");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    public void CrawlSettings_Constructor_WithNegativeMaxDepth_ThrowsArgumentOutOfRangeException(int maxDepth)
    {
        // Act
        var act = () => new CrawlSettings(
            seedUrls: new List<string> { "https://example.com" }.AsReadOnly(),
            maxDepth: maxDepth);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("maxDepth");
    }

    [Fact]
    public void CrawlSettings_Constructor_WithZeroMaxDepth_IsValid()
    {
        // Arrange & Act
        var settings = new CrawlSettings(
            seedUrls: new List<string> { "https://example.com" }.AsReadOnly(),
            maxDepth: 0);

        // Assert
        settings.MaxDepth.Should().Be(0);
    }

    [Fact]
    public void CrawlSettings_HasValidSeedUrls_WithValidUrls_ReturnsTrue()
    {
        // Arrange
        var seedUrls = new List<string>
        {
            "https://example.com/recipes",
            "http://another-site.com/food"
        };
        var settings = new CrawlSettings(seedUrls: seedUrls.AsReadOnly());

        // Act
        var result = settings.HasValidSeedUrls();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CrawlSettings_HasValidSeedUrls_WithEmptyList_ReturnsFalse()
    {
        // Arrange
        var settings = new CrawlSettings(seedUrls: new List<string>().AsReadOnly());

        // Act
        var result = settings.HasValidSeedUrls();

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://example.com")]
    [InlineData("file:///path/to/file")]
    [InlineData("")]
    [InlineData("   ")]
    public void CrawlSettings_HasValidSeedUrls_WithInvalidUrl_ReturnsFalse(string invalidUrl)
    {
        // Arrange
        var seedUrls = new List<string> { invalidUrl };
        var settings = new CrawlSettings(seedUrls: seedUrls.AsReadOnly());

        // Act
        var result = settings.HasValidSeedUrls();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CrawlSettings_HasValidSeedUrls_WithMixedValidAndInvalidUrls_ReturnsFalse()
    {
        // Arrange - One valid URL and one invalid URL
        var seedUrls = new List<string>
        {
            "https://example.com/recipes",
            "not-a-url"
        };
        var settings = new CrawlSettings(seedUrls: seedUrls.AsReadOnly());

        // Act
        var result = settings.HasValidSeedUrls();

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region AuthMethod Enum Tests

    [Fact]
    public void AuthMethod_HasExpectedValues()
    {
        // Assert
        Enum.GetValues<AuthMethod>().Should().HaveCount(4);
        Enum.IsDefined(AuthMethod.None).Should().BeTrue();
        Enum.IsDefined(AuthMethod.ApiKey).Should().BeTrue();
        Enum.IsDefined(AuthMethod.Bearer).Should().BeTrue();
        Enum.IsDefined(AuthMethod.Basic).Should().BeTrue();
    }

    #endregion

    #region DiscoveryStrategy Enum Tests

    [Fact]
    public void DiscoveryStrategy_HasExpectedValues()
    {
        // Assert
        Enum.GetValues<DiscoveryStrategy>().Should().HaveCount(2);
        Enum.IsDefined(DiscoveryStrategy.Api).Should().BeTrue();
        Enum.IsDefined(DiscoveryStrategy.Crawl).Should().BeTrue();
    }

    #endregion

    #region FetchingStrategy Enum Tests

    [Fact]
    public void FetchingStrategy_HasExpectedValues()
    {
        // Assert
        Enum.GetValues<FetchingStrategy>().Should().HaveCount(3);
        Enum.IsDefined(FetchingStrategy.Api).Should().BeTrue();
        Enum.IsDefined(FetchingStrategy.StaticHtml).Should().BeTrue();
        Enum.IsDefined(FetchingStrategy.DynamicHtml).Should().BeTrue();
    }

    #endregion
}
