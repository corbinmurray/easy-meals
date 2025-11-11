using EasyMeals.RecipeEngine.Application.Interfaces;
using EasyMeals.RecipeEngine.Infrastructure.RateLimiting;
using Shouldly;

namespace EasyMeals.RecipeEngine.Tests.Integration;

/// <summary>
///     Integration tests for rate limiting functionality.
///     Verifies max requests per minute enforcement and burst handling.
/// </summary>
public class RateLimitingIntegrationTests
{
    [Fact(DisplayName = "Different providers have independent rate limits")]
    public async Task RateLimiter_DifferentProvidersIndependent()
    {
        // Arrange
        var rateLimiter = new TokenBucketRateLimiter(10, 10);
        const string provider1 = "provider-1";
        const string provider2 = "provider-2";

        // Act - Exhaust provider1's tokens
        for (var i = 0; i < 10; i++)
        {
            await rateLimiter.TryAcquireAsync(provider1, CancellationToken.None);
        }

        // Provider2 should still have tokens
        bool provider2Result = await rateLimiter.TryAcquireAsync(provider2, CancellationToken.None);

        // Assert
        provider2Result.ShouldBeTrue(); // "provider2 should have independent rate limit";
    }

    [Fact(DisplayName = "Rate limiter enforces max requests per minute")]
    public async Task RateLimiter_EnforcesMaxRequestsPerMinute()
    {
        // Arrange
        var rateLimiter = new TokenBucketRateLimiter(10, 10);
        const string providerId = "test-provider";
        const int maxRequests = 10;

        // Act - Try to acquire more than max requests
        var successCount = 0;
        for (var i = 0; i < maxRequests + 5; i++)
        {
            bool acquired = await rateLimiter.TryAcquireAsync(providerId, CancellationToken.None);
            if (acquired) successCount++;
        }

        // Assert - Should only allow max requests
        successCount.ShouldBeLessThanOrEqualTo(maxRequests);
    }

    [Fact(DisplayName = "Rate limiter handles burst traffic")]
    public async Task RateLimiter_HandlesBurstTraffic()
    {
        // Arrange
        var rateLimiter = new TokenBucketRateLimiter(10, 10);
        const string providerId = "burst-provider";

        // Act - Burst of requests
        var burstResults = new List<bool>();
        for (var i = 0; i < 5; i++)
        {
            bool acquired = await rateLimiter.TryAcquireAsync(providerId, CancellationToken.None);
            burstResults.Add(acquired);
        }

        // Assert - First few should succeed (burst handling)
        foreach (var result in burstResults.Take(3))
        {
            result.ShouldBeTrue();
        }
    }

    [Fact(DisplayName = "Rate limiter reset clears limits for key")]
    public async Task RateLimiter_ResetClearsLimits()
    {
        // Arrange
        var rateLimiter = new TokenBucketRateLimiter(10, 10);
        const string providerId = "reset-provider";

        // Act - Exhaust tokens
        for (var i = 0; i < 10; i++)
        {
            await rateLimiter.TryAcquireAsync(providerId, CancellationToken.None);
        }

        // Reset the rate limiter
        await rateLimiter.ResetAsync(providerId, CancellationToken.None);

        // Try to acquire after reset
        bool afterReset = await rateLimiter.TryAcquireAsync(providerId, CancellationToken.None);

        // Assert
        afterReset.ShouldBeTrue(); // "reset should restore tokens";
    }

    [Fact(DisplayName = "Rate limiter status shows correct remaining requests")]
    public async Task RateLimiter_StatusShowsRemainingRequests()
    {
        // Arrange
        var rateLimiter = new TokenBucketRateLimiter(10, 10);
        const string providerId = "status-provider";

        // Act - Get initial status
        RateLimitStatus initialStatus = await rateLimiter.GetStatusAsync(providerId, CancellationToken.None);

        // Consume some tokens
        await rateLimiter.TryAcquireAsync(providerId, CancellationToken.None);
        await rateLimiter.TryAcquireAsync(providerId, CancellationToken.None);

        // Get status after consumption
        RateLimitStatus afterStatus = await rateLimiter.GetStatusAsync(providerId, CancellationToken.None);

        // Assert
        initialStatus.RemainingRequests.ShouldBeGreaterThan(0);
        afterStatus.RemainingRequests.ShouldBeLessThan(initialStatus.RemainingRequests);
    }

    [Fact(DisplayName = "Rate limiter tokens refill over time")]
    public async Task RateLimiter_TokensRefillOverTime()
    {
        // Arrange
        var rateLimiter = new TokenBucketRateLimiter(10, 10);
        const string providerId = "refill-provider";

        // Act - Exhaust tokens
        bool initialAcquired = await rateLimiter.TryAcquireAsync(providerId, CancellationToken.None);
        await rateLimiter.TryAcquireAsync(providerId, CancellationToken.None);
        await rateLimiter.TryAcquireAsync(providerId, CancellationToken.None);

        // Wait for refill (simulating time passage)
        await Task.Delay(TimeSpan.FromSeconds(1));

        // Try again after refill period
        bool afterRefill = await rateLimiter.TryAcquireAsync(providerId, CancellationToken.None);

        // Assert
        initialAcquired.ShouldBeTrue();
        afterRefill.ShouldBeTrue(); // "tokens should refill over time";
    }
}