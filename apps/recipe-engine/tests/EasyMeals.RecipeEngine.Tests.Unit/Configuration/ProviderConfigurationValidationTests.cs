using EasyMeals.RecipeEngine.Domain.ValueObjects;
using Shouldly;

namespace EasyMeals.RecipeEngine.Tests.Unit.Configuration;

public class ProviderConfigurationValidationTests
{
    private static ProviderConfiguration CreateValidConfiguration() =>
        new(
            "provider_001",
            true,
            DiscoveryStrategy.Dynamic,
            "https://example.com/recipes",
            10,
            10,
            2.0,
            10,
            3,
            30);

    [Fact]
    public void Constructor_AllowsZeroMinDelay()
    {
        // Arrange & Act
        var config = new ProviderConfiguration(
            "provider_001",
            true,
            DiscoveryStrategy.Dynamic,
            "https://example.com/recipes",
            10,
            10,
            0.0, // Zero is allowed
            10,
            3,
            30);

        // Assert
        config.MinDelay.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void Constructor_AllowsZeroRetryCount()
    {
        // Arrange & Act
        var config = new ProviderConfiguration(
            "provider_001",
            true,
            DiscoveryStrategy.Dynamic,
            "https://example.com/recipes",
            10,
            10,
            2.0,
            10,
            0, // Zero retries is allowed
            30);

        // Assert
        config.RetryCount.ShouldBe(0);
    }

    [Fact]
    public void Constructor_CreatesValidConfiguration_WithValidInputs()
    {
        // Arrange & Act
        ProviderConfiguration config = CreateValidConfiguration();

        // Assert
        config.ShouldNotBeNull();
        config.ProviderId.ShouldBe("provider_001");
        config.Enabled.ShouldBeTrue();
        config.RecipeRootUrl.ShouldBe("https://example.com/recipes");
        config.BatchSize.ShouldBe(10);
        config.TimeWindow.ShouldBe(TimeSpan.FromMinutes(10));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Constructor_ThrowsException_WhenBatchSizeIsNotPositive(int invalidBatchSize)
    {
        // Act & Assert
        Action act = () => new ProviderConfiguration(
            "provider_001",
            true,
            DiscoveryStrategy.Dynamic,
            "https://example.com/recipes",
            invalidBatchSize,
            10,
            2.0,
            10,
            3,
            30);

        Should.Throw<ArgumentException>(act); // TODO: Shouldly uses .ShouldContain() on exception message - verify: "*BatchSize must be positive*";
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Constructor_ThrowsException_WhenMaxRequestsPerMinuteIsNotPositive(int invalidMaxRequests)
    {
        // Act & Assert
        Action act = () => new ProviderConfiguration(
            "provider_001",
            true,
            DiscoveryStrategy.Dynamic,
            "https://example.com/recipes",
            10,
            10,
            2.0,
            invalidMaxRequests,
            3,
            30);

        Should.Throw<ArgumentException>(act); // TODO: Shouldly uses .ShouldContain() on exception message - verify: "*MaxRequestsPerMinute must be positive*";
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Constructor_ThrowsException_WhenMinDelayIsNegative(double invalidMinDelay)
    {
        // Act & Assert
        Action act = () => new ProviderConfiguration(
            "provider_001",
            true,
            DiscoveryStrategy.Dynamic,
            "https://example.com/recipes",
            10,
            10,
            invalidMinDelay,
            10,
            3,
            30);

        Should.Throw<ArgumentException>(act); // TODO: Shouldly uses .ShouldContain() on exception message - verify: "*MinDelay cannot be negative*";
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ThrowsException_WhenProviderIdIsInvalid(string? invalidProviderId)
    {
        // Act & Assert
        Action act = () => new ProviderConfiguration(
            invalidProviderId!,
            true,
            DiscoveryStrategy.Dynamic,
            "https://example.com/recipes",
            10,
            10,
            2.0,
            10,
            3,
            30);

        Should.Throw<ArgumentException>(act); // TODO: Shouldly uses .ShouldContain() on exception message - verify: "*ProviderId is required*";
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ThrowsException_WhenRecipeRootUrlIsEmpty(string? invalidUrl)
    {
        // Act & Assert
        Action act = () => new ProviderConfiguration(
            "provider_001",
            true,
            DiscoveryStrategy.Dynamic,
            invalidUrl!,
            10,
            10,
            2.0,
            10,
            3,
            30);

        Should.Throw<ArgumentException>(act); // TODO: Shouldly uses .ShouldContain() on exception message - verify: "*RecipeRootUrl is required*";
    }

    [Theory]
    [InlineData("http://example.com/recipes")]
    [InlineData("ftp://example.com/recipes")]
    public void Constructor_ThrowsException_WhenRecipeRootUrlIsNotHttps(string nonHttpsUrl)
    {
        // Act & Assert
        Action act = () => new ProviderConfiguration(
            "provider_001",
            true,
            DiscoveryStrategy.Dynamic,
            nonHttpsUrl,
            10,
            10,
            2.0,
            10,
            3,
            30);

        Should.Throw<ArgumentException>(act); // TODO: Shouldly uses .ShouldContain() on exception message - verify: "*RecipeRootUrl must use HTTPS*";
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("example.com")]
    [InlineData("relative/path")]
    public void Constructor_ThrowsException_WhenRecipeRootUrlIsNotValidAbsoluteUrl(string invalidUrl)
    {
        // Act & Assert
        Action act = () => new ProviderConfiguration(
            "provider_001",
            true,
            DiscoveryStrategy.Dynamic,
            invalidUrl,
            10,
            10,
            2.0,
            10,
            3,
            30);

        Should.Throw<ArgumentException>(act); // TODO: Shouldly uses .ShouldContain() on exception message - verify: "*RecipeRootUrl must be a valid absolute URL*";
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Constructor_ThrowsException_WhenRequestTimeoutIsNotPositive(int invalidTimeout)
    {
        // Act & Assert
        Action act = () => new ProviderConfiguration(
            "provider_001",
            true,
            DiscoveryStrategy.Dynamic,
            "https://example.com/recipes",
            10,
            10,
            2.0,
            10,
            3,
            invalidTimeout);

        Should.Throw<ArgumentException>(act); // TODO: Shouldly uses .ShouldContain() on exception message - verify: "*RequestTimeout must be positive*";
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Constructor_ThrowsException_WhenRetryCountIsNegative(int invalidRetryCount)
    {
        // Act & Assert
        Action act = () => new ProviderConfiguration(
            "provider_001",
            true,
            DiscoveryStrategy.Dynamic,
            "https://example.com/recipes",
            10,
            10,
            2.0,
            10,
            invalidRetryCount,
            30);

        Should.Throw<ArgumentException>(act); // TODO: Shouldly uses .ShouldContain() on exception message - verify: "*RetryCount cannot be negative*";
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Constructor_ThrowsException_WhenTimeWindowIsNotPositive(int invalidTimeWindow)
    {
        // Act & Assert
        Action act = () => new ProviderConfiguration(
            "provider_001",
            true,
            DiscoveryStrategy.Dynamic,
            "https://example.com/recipes",
            10,
            invalidTimeWindow,
            2.0,
            10,
            3,
            30);

        Should.Throw<ArgumentException>(act); // TODO: Shouldly uses .ShouldContain() on exception message - verify: "*TimeWindow must be positive*";
    }

    [Fact]
    public void Equals_ReturnFalse_WhenProviderIdsDiffer()
    {
        // Arrange
        ProviderConfiguration config1 = CreateValidConfiguration();
        var config2 = new ProviderConfiguration(
            "provider_002", // Different provider ID
            true,
            DiscoveryStrategy.Dynamic,
            "https://example.com/recipes",
            10,
            10,
            2.0,
            10,
            3,
            30);

        // Act & Assert
        config1.Equals(config2).ShouldBeFalse();
        config1.GetHashCode().ShouldNotBe(config2.GetHashCode());
    }

    [Fact]
    public void Equals_ReturnTrue_WhenProviderIdsMatch()
    {
        // Arrange
        ProviderConfiguration config1 = CreateValidConfiguration();
        ProviderConfiguration config2 = CreateValidConfiguration();

        // Act & Assert
        config1.Equals(config2).ShouldBeTrue();
        config1.GetHashCode().ShouldBe(config2.GetHashCode());
    }

    [Fact]
    public void Constructor_AcceptsValidRegexPatterns()
    {
        // Arrange & Act
        var config = new ProviderConfiguration(
            "provider_001",
            true,
            DiscoveryStrategy.Static,
            "https://example.com/recipes",
            10,
            10,
            2.0,
            10,
            3,
            30,
            recipeUrlPattern: @"\/recipe\/[a-z0-9\-]+\-[a-f0-9]{24}$",
            categoryUrlPattern: @"\/recipes\/(category|tag)\/[a-z\-]+"
        );

        // Assert
        config.ShouldNotBeNull();
        config.RecipeUrlPattern.ShouldBe(@"\/recipe\/[a-z0-9\-]+\-[a-f0-9]{24}$");
        config.CategoryUrlPattern.ShouldBe(@"\/recipes\/(category|tag)\/[a-z\-]+");
    }

    [Fact]
    public void Constructor_AcceptsNullRegexPatterns()
    {
        // Arrange & Act
        var config = new ProviderConfiguration(
            "provider_001",
            true,
            DiscoveryStrategy.Static,
            "https://example.com/recipes",
            10,
            10,
            2.0,
            10,
            3,
            30,
            recipeUrlPattern: null,
            categoryUrlPattern: null
        );

        // Assert
        config.ShouldNotBeNull();
        config.RecipeUrlPattern.ShouldBeNull();
        config.CategoryUrlPattern.ShouldBeNull();
    }

    [Fact]
    public void Constructor_AcceptsEmptyStringRegexPatterns()
    {
        // Arrange & Act
        var config = new ProviderConfiguration(
            "provider_001",
            true,
            DiscoveryStrategy.Static,
            "https://example.com/recipes",
            10,
            10,
            2.0,
            10,
            3,
            30,
            recipeUrlPattern: "",
            categoryUrlPattern: "   "
        );

        // Assert
        config.ShouldNotBeNull();
        config.RecipeUrlPattern.ShouldBe("");
        config.CategoryUrlPattern.ShouldBe("   ");
    }

    [Theory]
    [InlineData("[invalid")]
    [InlineData("(?<invalid")]
    [InlineData("(unclosed")]
    public void Constructor_ThrowsException_WhenRecipeUrlPatternIsInvalidRegex(string invalidPattern)
    {
        // Act & Assert
        Action act = () => new ProviderConfiguration(
            "provider_001",
            true,
            DiscoveryStrategy.Static,
            "https://example.com/recipes",
            10,
            10,
            2.0,
            10,
            3,
            30,
            recipeUrlPattern: invalidPattern,
            categoryUrlPattern: null
        );

        var ex = Should.Throw<ArgumentException>(act);
        ex.ParamName.ShouldBe("recipeUrlPattern");
        ex.Message.ShouldContain("not a valid regex");
    }

    [Theory]
    [InlineData("[invalid")]
    [InlineData("(?<invalid")]
    [InlineData("(unclosed")]
    public void Constructor_ThrowsException_WhenCategoryUrlPatternIsInvalidRegex(string invalidPattern)
    {
        // Act & Assert
        Action act = () => new ProviderConfiguration(
            "provider_001",
            true,
            DiscoveryStrategy.Static,
            "https://example.com/recipes",
            10,
            10,
            2.0,
            10,
            3,
            30,
            recipeUrlPattern: null,
            categoryUrlPattern: invalidPattern
        );

        var ex = Should.Throw<ArgumentException>(act);
        ex.ParamName.ShouldBe("categoryUrlPattern");
        ex.Message.ShouldContain("not a valid regex");
    }
}