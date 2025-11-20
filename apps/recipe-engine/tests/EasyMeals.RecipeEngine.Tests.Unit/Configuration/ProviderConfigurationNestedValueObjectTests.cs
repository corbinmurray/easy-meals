using EasyMeals.RecipeEngine.Domain.ValueObjects;
using EasyMeals.RecipeEngine.Domain.ValueObjects.Provider;
using Shouldly;

namespace EasyMeals.RecipeEngine.Tests.Unit.Configuration;

public class ProviderConfigurationNestedValueObjectTests
{
    [Fact]
    public void EndpointInfo_CreatesSuccessfully_WithValidHttpsUrl()
    {
        // Arrange & Act
        var endpoint = new EndpointInfo("https://example.com/recipes");

        // Assert
        endpoint.RecipeRootUrl.ShouldBe("https://example.com/recipes");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EndpointInfo_ThrowsException_WhenUrlIsInvalid(string? invalidUrl)
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => new EndpointInfo(invalidUrl!));
    }

    [Theory]
    [InlineData("http://example.com")]
    [InlineData("ftp://example.com")]
    public void EndpointInfo_ThrowsException_WhenUrlIsNotHttps(string nonHttpsUrl)
    {
        // Act & Assert
        var ex = Should.Throw<ArgumentException>(() => new EndpointInfo(nonHttpsUrl));
        ex.Message.ShouldContain("HTTPS");
    }

    [Fact]
    public void EndpointInfo_ThrowsException_WhenUrlIsNotAbsolute()
    {
        // Act & Assert
        var ex = Should.Throw<ArgumentException>(() => new EndpointInfo("relative/path"));
        ex.Message.ShouldContain("absolute URL");
    }

    [Fact]
    public void DiscoveryConfig_CreatesSuccessfully_WithValidInputs()
    {
        // Arrange & Act
        var config = new DiscoveryConfig(
            DiscoveryStrategy.Dynamic,
            recipeUrlPattern: @"\/recipe\/[a-z0-9\-]+",
            categoryUrlPattern: @"\/category\/[a-z\-]+");

        // Assert
        config.Strategy.ShouldBe(DiscoveryStrategy.Dynamic);
        config.RecipeUrlPattern.ShouldBe(@"\/recipe\/[a-z0-9\-]+");
        config.CategoryUrlPattern.ShouldBe(@"\/category\/[a-z\-]+");
    }

    [Fact]
    public void DiscoveryConfig_CreatesSuccessfully_WithNullPatterns()
    {
        // Arrange & Act
        var config = new DiscoveryConfig(DiscoveryStrategy.Static);

        // Assert
        config.Strategy.ShouldBe(DiscoveryStrategy.Static);
        config.RecipeUrlPattern.ShouldBeNull();
        config.CategoryUrlPattern.ShouldBeNull();
    }

    [Theory]
    [InlineData("[invalid")]
    [InlineData("(?<invalid")]
    public void DiscoveryConfig_ThrowsException_WhenRecipePatternIsInvalidRegex(string invalidPattern)
    {
        // Act & Assert
        var ex = Should.Throw<ArgumentException>(() => 
            new DiscoveryConfig(DiscoveryStrategy.Static, recipeUrlPattern: invalidPattern));
        ex.ParamName.ShouldBe("recipeUrlPattern");
        ex.Message.ShouldContain("not a valid regex");
    }

    [Theory]
    [InlineData("[invalid")]
    [InlineData("(?<invalid")]
    public void DiscoveryConfig_ThrowsException_WhenCategoryPatternIsInvalidRegex(string invalidPattern)
    {
        // Act & Assert
        var ex = Should.Throw<ArgumentException>(() => 
            new DiscoveryConfig(DiscoveryStrategy.Static, categoryUrlPattern: invalidPattern));
        ex.ParamName.ShouldBe("categoryUrlPattern");
        ex.Message.ShouldContain("not a valid regex");
    }

    [Fact]
    public void BatchingConfig_CreatesSuccessfully_WithValidInputs()
    {
        // Arrange & Act
        var config = new BatchingConfig(batchSize: 10, timeWindowMinutes: 15);

        // Assert
        config.BatchSize.ShouldBe(10);
        config.TimeWindow.ShouldBe(TimeSpan.FromMinutes(15));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void BatchingConfig_ThrowsException_WhenBatchSizeIsNotPositive(int invalidBatchSize)
    {
        // Act & Assert
        var ex = Should.Throw<ArgumentException>(() => 
            new BatchingConfig(invalidBatchSize, 10));
        ex.Message.ShouldContain("BatchSize must be positive");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void BatchingConfig_ThrowsException_WhenTimeWindowIsNotPositive(int invalidTimeWindow)
    {
        // Act & Assert
        var ex = Should.Throw<ArgumentException>(() => 
            new BatchingConfig(10, invalidTimeWindow));
        ex.Message.ShouldContain("TimeWindow must be positive");
    }

    [Fact]
    public void RateLimitConfig_CreatesSuccessfully_WithValidInputs()
    {
        // Arrange & Act
        var config = new RateLimitConfig(
            minDelaySeconds: 2.0,
            maxRequestsPerMinute: 10,
            retryCount: 3,
            requestTimeoutSeconds: 30);

        // Assert
        config.MinDelay.ShouldBe(TimeSpan.FromSeconds(2.0));
        config.MaxRequestsPerMinute.ShouldBe(10);
        config.RetryCount.ShouldBe(3);
        config.RequestTimeout.ShouldBe(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void RateLimitConfig_AllowsZeroMinDelay()
    {
        // Arrange & Act
        var config = new RateLimitConfig(0.0, 10, 3, 30);

        // Assert
        config.MinDelay.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void RateLimitConfig_AllowsZeroRetryCount()
    {
        // Arrange & Act
        var config = new RateLimitConfig(2.0, 10, 0, 30);

        // Assert
        config.RetryCount.ShouldBe(0);
    }

    [Fact]
    public void RateLimitConfig_ThrowsException_WhenMinDelayIsNegative()
    {
        // Act & Assert
        var ex = Should.Throw<ArgumentException>(() => 
            new RateLimitConfig(-1.0, 10, 3, 30));
        ex.Message.ShouldContain("MinDelay cannot be negative");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RateLimitConfig_ThrowsException_WhenMaxRequestsPerMinuteIsNotPositive(int invalidMax)
    {
        // Act & Assert
        var ex = Should.Throw<ArgumentException>(() => 
            new RateLimitConfig(2.0, invalidMax, 3, 30));
        ex.Message.ShouldContain("MaxRequestsPerMinute must be positive");
    }

    [Fact]
    public void RateLimitConfig_ThrowsException_WhenRetryCountIsNegative()
    {
        // Act & Assert
        var ex = Should.Throw<ArgumentException>(() => 
            new RateLimitConfig(2.0, 10, -1, 30));
        ex.Message.ShouldContain("RetryCount cannot be negative");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RateLimitConfig_ThrowsException_WhenRequestTimeoutIsNotPositive(int invalidTimeout)
    {
        // Act & Assert
        var ex = Should.Throw<ArgumentException>(() => 
            new RateLimitConfig(2.0, 10, 3, invalidTimeout));
        ex.Message.ShouldContain("RequestTimeout must be positive");
    }

    [Fact]
    public void ProviderConfiguration_CreatesSuccessfully_WithNestedValueObjects()
    {
        // Arrange
        var endpoint = new EndpointInfo("https://example.com/recipes");
        var discovery = new DiscoveryConfig(DiscoveryStrategy.Dynamic);
        var batching = new BatchingConfig(10, 15);
        var rateLimit = new RateLimitConfig(2.0, 10, 3, 30);

        // Act
        var config = new ProviderConfiguration(
            "provider_001",
            true,
            endpoint,
            discovery,
            batching,
            rateLimit);

        // Assert
        config.ProviderId.ShouldBe("provider_001");
        config.Enabled.ShouldBeTrue();
        config.Endpoint.ShouldBe(endpoint);
        config.Discovery.ShouldBe(discovery);
        config.Batching.ShouldBe(batching);
        config.RateLimit.ShouldBe(rateLimit);
    }

    [Fact]
    public void ProviderConfiguration_BackwardCompatibilityConstructor_CreatesNestedValueObjects()
    {
        // Arrange & Act
        var config = new ProviderConfiguration(
            "provider_001",
            true,
            DiscoveryStrategy.Dynamic,
            "https://example.com/recipes",
            10,
            15,
            2.0,
            10,
            3,
            30);

        // Assert
        config.ProviderId.ShouldBe("provider_001");
        config.Enabled.ShouldBeTrue();
        config.RecipeRootUrl.ShouldBe("https://example.com/recipes");
        config.DiscoveryStrategy.ShouldBe(DiscoveryStrategy.Dynamic);
        config.BatchSize.ShouldBe(10);
        config.TimeWindow.ShouldBe(TimeSpan.FromMinutes(15));
        config.MinDelay.ShouldBe(TimeSpan.FromSeconds(2.0));
        config.MaxRequestsPerMinute.ShouldBe(10);
        config.RetryCount.ShouldBe(3);
        config.RequestTimeout.ShouldBe(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void ProviderConfiguration_ThrowsException_WhenEndpointIsNull()
    {
        // Arrange
        var discovery = new DiscoveryConfig(DiscoveryStrategy.Dynamic);
        var batching = new BatchingConfig(10, 15);
        var rateLimit = new RateLimitConfig(2.0, 10, 3, 30);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new ProviderConfiguration(
            "provider_001",
            true,
            null!,
            discovery,
            batching,
            rateLimit));
    }

    [Fact]
    public void ProviderConfiguration_ThrowsException_WhenDiscoveryIsNull()
    {
        // Arrange
        var endpoint = new EndpointInfo("https://example.com/recipes");
        var batching = new BatchingConfig(10, 15);
        var rateLimit = new RateLimitConfig(2.0, 10, 3, 30);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new ProviderConfiguration(
            "provider_001",
            true,
            endpoint,
            null!,
            batching,
            rateLimit));
    }

    [Fact]
    public void ProviderConfiguration_ThrowsException_WhenBatchingIsNull()
    {
        // Arrange
        var endpoint = new EndpointInfo("https://example.com/recipes");
        var discovery = new DiscoveryConfig(DiscoveryStrategy.Dynamic);
        var rateLimit = new RateLimitConfig(2.0, 10, 3, 30);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new ProviderConfiguration(
            "provider_001",
            true,
            endpoint,
            discovery,
            null!,
            rateLimit));
    }

    [Fact]
    public void ProviderConfiguration_ThrowsException_WhenRateLimitIsNull()
    {
        // Arrange
        var endpoint = new EndpointInfo("https://example.com/recipes");
        var discovery = new DiscoveryConfig(DiscoveryStrategy.Dynamic);
        var batching = new BatchingConfig(10, 15);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new ProviderConfiguration(
            "provider_001",
            true,
            endpoint,
            discovery,
            batching,
            null!));
    }
}
