using EasyMeals.RecipeEngine.Application.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace EasyMeals.RecipeEngine.Infrastructure.HealthChecks;

public class RateLimiterHealthCheck : IHealthCheck
{
    private readonly IRateLimiter _rateLimiter;

    public RateLimiterHealthCheck(IRateLimiter rateLimiter) => _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if rate limiter can provide status information
            var testProviderId = "health-check";
            RateLimitStatus status = await _rateLimiter.GetStatusAsync(testProviderId, cancellationToken);

            if (status.RemainingRequests >= 0)
                return HealthCheckResult.Healthy(
                    "Rate limiter is functioning correctly",
                    new Dictionary<string, object>
                    {
                        { "remainingRequests", status.RemainingRequests },
                        { "isLimited", status.IsLimited }
                    });

            return HealthCheckResult.Degraded(
                "Rate limiter configuration may be invalid");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Rate limiter is not functioning",
                ex);
        }
    }
}