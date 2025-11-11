using EasyMeals.RecipeEngine.Application.Interfaces;
using EasyMeals.RecipeEngine.Infrastructure.RateLimiting;
using Shouldly;

namespace EasyMeals.RecipeEngine.Tests.Unit.RateLimiting;

/// <summary>
///     Unit tests for TokenBucketRateLimiter implementation.
///     Tests token consumption, refill, and burst handling.
/// </summary>
public class TokenBucketRateLimiterTests
{
    [Fact(DisplayName = "Initial rate limiter should have full token capacity")]
    public async Task GetStatusAsync_InitialState_ReturnsFullCapacity()
    {
        // Arrange
        const int maxTokens = 10;
        const int refillRatePerMinute = 5;
        var rateLimiter = new TokenBucketRateLimiter(maxTokens, refillRatePerMinute);
        const string key = "test-provider";

        // Act
        RateLimitStatus status = await rateLimiter.GetStatusAsync(key);

        // Assert
        status.ShouldNotBeNull();
        status.RemainingRequests.ShouldBe(maxTokens);
        status.IsLimited.ShouldBeFalse();
    }

    [Fact(DisplayName = "Status shows limited when bucket is empty")]
    public async Task GetStatusAsync_WhenBucketEmpty_ShowsLimited()
    {
        // Arrange
        const int maxTokens = 2;
        const int refillRatePerMinute = 1;
        var rateLimiter = new TokenBucketRateLimiter(maxTokens, refillRatePerMinute);
        const string key = "test-provider";

        // Act - Deplete all tokens
        await rateLimiter.TryAcquireAsync(key);
        await rateLimiter.TryAcquireAsync(key);
        RateLimitStatus status = await rateLimiter.GetStatusAsync(key);

        // Assert
        status.IsLimited.ShouldBeTrue();
        status.RemainingRequests.ShouldBe(0);
    }

    [Fact(DisplayName = "Reset clears rate limit for key")]
    public async Task ResetAsync_AfterDepletion_RestoresFullCapacity()
    {
        // Arrange
        const int maxTokens = 5;
        const int refillRatePerMinute = 2;
        var rateLimiter = new TokenBucketRateLimiter(maxTokens, refillRatePerMinute);
        const string key = "test-provider";

        // Act - Deplete tokens
        await rateLimiter.TryAcquireAsync(key, 5);
        RateLimitStatus statusBefore = await rateLimiter.GetStatusAsync(key);

        // Reset
        await rateLimiter.ResetAsync(key);
        RateLimitStatus statusAfter = await rateLimiter.GetStatusAsync(key);

        // Assert
        statusBefore.RemainingRequests.ShouldBe(0);
        statusAfter.RemainingRequests.ShouldBe(maxTokens);
        statusAfter.IsLimited.ShouldBeFalse();
    }

    [Fact(DisplayName = "Token consumption reduces remaining tokens")]
    public async Task TryAcquireAsync_AfterConsumption_ReducesRemainingTokens()
    {
        // Arrange
        const int maxTokens = 10;
        const int refillRatePerMinute = 5;
        var rateLimiter = new TokenBucketRateLimiter(maxTokens, refillRatePerMinute);
        const string key = "test-provider";

        // Act
        await rateLimiter.TryAcquireAsync(key);
        RateLimitStatus status = await rateLimiter.GetStatusAsync(key);

        // Assert
        status.RemainingRequests.ShouldBe(maxTokens - 1);
    }

    [Fact(DisplayName = "Tokens refill over time")]
    public async Task TryAcquireAsync_AfterWait_TokensRefilled()
    {
        // Arrange
        const int maxTokens = 10;
        const int refillRatePerMinute = 60; // 1 per second for easier testing
        var rateLimiter = new TokenBucketRateLimiter(maxTokens, refillRatePerMinute);
        const string key = "test-provider";

        // Act - Consume some tokens
        await rateLimiter.TryAcquireAsync(key, 5);
        RateLimitStatus statusBefore = await rateLimiter.GetStatusAsync(key);

        // Wait for refill (1+ second = at least 1 token)
        await Task.Delay(TimeSpan.FromSeconds(1.1));

        RateLimitStatus statusAfter = await rateLimiter.GetStatusAsync(key);

        // Assert
        statusBefore.RemainingRequests.ShouldBe(5);
        statusAfter.RemainingRequests.ShouldBeGreaterThan(5);
        statusAfter.RemainingRequests.ShouldBeLessThanOrEqualTo(maxTokens);
    }

    [Fact(DisplayName = "Burst handling allows temporary excess")]
    public async Task TryAcquireAsync_BurstScenario_AllowsFullCapacity()
    {
        // Arrange
        const int maxTokens = 100;
        const int refillRatePerMinute = 10;
        var rateLimiter = new TokenBucketRateLimiter(maxTokens, refillRatePerMinute);
        const string key = "test-provider";

        // Act - Burst acquire up to max tokens
        var acquisitions = new List<bool>();
        for (var i = 0; i < maxTokens; i++)
        {
            acquisitions.Add(await rateLimiter.TryAcquireAsync(key));
        }

        // Assert
        acquisitions.ShouldAllBe(r => r);
        RateLimitStatus status = await rateLimiter.GetStatusAsync(key);
        status.RemainingRequests.ShouldBe(0);
    }

    [Fact(DisplayName = "Different keys have independent buckets")]
    public async Task TryAcquireAsync_DifferentKeys_IndependentBuckets()
    {
        // Arrange
        const int maxTokens = 5;
        const int refillRatePerMinute = 2;
        var rateLimiter = new TokenBucketRateLimiter(maxTokens, refillRatePerMinute);
        const string key1 = "provider-1";
        const string key2 = "provider-2";

        // Act - Deplete tokens for key1
        await rateLimiter.TryAcquireAsync(key1, 5);
        RateLimitStatus status1 = await rateLimiter.GetStatusAsync(key1);
        RateLimitStatus status2 = await rateLimiter.GetStatusAsync(key2);

        // Assert
        status1.RemainingRequests.ShouldBe(0);
        status2.RemainingRequests.ShouldBe(maxTokens); // key2 unaffected
    }

    [Fact(DisplayName = "Cannot acquire more tokens than available")]
    public async Task TryAcquireAsync_MorePermitsThanAvailable_ReturnsFalse()
    {
        // Arrange
        const int maxTokens = 5;
        const int refillRatePerMinute = 2;
        var rateLimiter = new TokenBucketRateLimiter(maxTokens, refillRatePerMinute);
        const string key = "test-provider";

        // Act
        bool acquired = await rateLimiter.TryAcquireAsync(key, 10);

        // Assert
        acquired.ShouldBeFalse();
    }

    [Fact(DisplayName = "Multiple token acquisition depletes bucket")]
    public async Task TryAcquireAsync_MultipleAcquisitions_DepletesTokens()
    {
        // Arrange
        const int maxTokens = 3;
        const int refillRatePerMinute = 1;
        var rateLimiter = new TokenBucketRateLimiter(maxTokens, refillRatePerMinute);
        const string key = "test-provider";

        // Act - Acquire all tokens
        await rateLimiter.TryAcquireAsync(key);
        await rateLimiter.TryAcquireAsync(key);
        await rateLimiter.TryAcquireAsync(key);
        RateLimitStatus finalStatus = await rateLimiter.GetStatusAsync(key);

        // Assert
        finalStatus.RemainingRequests.ShouldBe(0);
    }

    [Fact(DisplayName = "Acquire multiple tokens at once")]
    public async Task TryAcquireAsync_MultiplePermits_ReducesTokensByPermitCount()
    {
        // Arrange
        const int maxTokens = 10;
        const int refillRatePerMinute = 5;
        var rateLimiter = new TokenBucketRateLimiter(maxTokens, refillRatePerMinute);
        const string key = "test-provider";

        // Act
        await rateLimiter.TryAcquireAsync(key, 3);
        RateLimitStatus status = await rateLimiter.GetStatusAsync(key);

        // Assert
        status.RemainingRequests.ShouldBe(maxTokens - 3);
    }

    [Fact(DisplayName = "Acquisition fails when bucket is empty")]
    public async Task TryAcquireAsync_WhenBucketEmpty_ReturnsFalse()
    {
        // Arrange
        const int maxTokens = 2;
        const int refillRatePerMinute = 1;
        var rateLimiter = new TokenBucketRateLimiter(maxTokens, refillRatePerMinute);
        const string key = "test-provider";

        // Act - Deplete all tokens
        await rateLimiter.TryAcquireAsync(key);
        await rateLimiter.TryAcquireAsync(key);
        bool thirdAttempt = await rateLimiter.TryAcquireAsync(key);

        // Assert
        thirdAttempt.ShouldBeFalse();
    }

    [Fact(DisplayName = "Successfully acquire single token when tokens available")]
    public async Task TryAcquireAsync_WithAvailableTokens_ReturnsTrue()
    {
        // Arrange
        const int maxTokens = 10;
        const int refillRatePerMinute = 5;
        var rateLimiter = new TokenBucketRateLimiter(maxTokens, refillRatePerMinute);
        const string key = "test-provider";

        // Act
        bool acquired = await rateLimiter.TryAcquireAsync(key);

        // Assert
        acquired.ShouldBeTrue();
    }
}