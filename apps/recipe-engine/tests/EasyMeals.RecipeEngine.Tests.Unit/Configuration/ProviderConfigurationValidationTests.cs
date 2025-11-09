using EasyMeals.RecipeEngine.Domain.ValueObjects;
using FluentAssertions;

namespace EasyMeals.RecipeEngine.Tests.Unit.Configuration;

public class ProviderConfigurationValidationTests
{
	[Fact]
	public void Constructor_CreatesValidConfiguration_WithValidInputs()
	{
		// Arrange & Act
		var config = CreateValidConfiguration();

		// Assert
		config.Should().NotBeNull();
		config.ProviderId.Should().Be("provider_001");
		config.Enabled.Should().BeTrue();
		config.RecipeRootUrl.Should().Be("https://example.com/recipes");
		config.BatchSize.Should().Be(10);
		config.TimeWindow.Should().Be(TimeSpan.FromMinutes(10));
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

		act.Should().Throw<ArgumentException>()
			.WithMessage("*ProviderId is required*");
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

		act.Should().Throw<ArgumentException>()
			.WithMessage("*RecipeRootUrl is required*");
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

		act.Should().Throw<ArgumentException>()
			.WithMessage("*RecipeRootUrl must be a valid absolute URL*");
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

		act.Should().Throw<ArgumentException>()
			.WithMessage("*RecipeRootUrl must use HTTPS*");
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

		act.Should().Throw<ArgumentException>()
			.WithMessage("*BatchSize must be positive*");
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

		act.Should().Throw<ArgumentException>()
			.WithMessage("*TimeWindow must be positive*");
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

		act.Should().Throw<ArgumentException>()
			.WithMessage("*MinDelay cannot be negative*");
	}

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
			0.0,  // Zero is allowed
			10,
			3,
			30);

		// Assert
		config.MinDelay.Should().Be(TimeSpan.Zero);
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

		act.Should().Throw<ArgumentException>()
			.WithMessage("*MaxRequestsPerMinute must be positive*");
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

		act.Should().Throw<ArgumentException>()
			.WithMessage("*RetryCount cannot be negative*");
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
			0,  // Zero retries is allowed
			30);

		// Assert
		config.RetryCount.Should().Be(0);
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

		act.Should().Throw<ArgumentException>()
			.WithMessage("*RequestTimeout must be positive*");
	}

	[Fact]
	public void Equals_ReturnTrue_WhenProviderIdsMatch()
	{
		// Arrange
		var config1 = CreateValidConfiguration();
		var config2 = CreateValidConfiguration();

		// Act & Assert
		config1.Equals(config2).Should().BeTrue();
		config1.GetHashCode().Should().Be(config2.GetHashCode());
	}

	[Fact]
	public void Equals_ReturnFalse_WhenProviderIdsDiffer()
	{
		// Arrange
		var config1 = CreateValidConfiguration();
		var config2 = new ProviderConfiguration(
			"provider_002",  // Different provider ID
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
		config1.Equals(config2).Should().BeFalse();
		config1.GetHashCode().Should().NotBe(config2.GetHashCode());
	}

	private static ProviderConfiguration CreateValidConfiguration()
	{
		return new ProviderConfiguration(
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
	}
}
