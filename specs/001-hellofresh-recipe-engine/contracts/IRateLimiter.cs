namespace EasyMeals.RecipeEngine.Application.Interfaces;

/// <summary>
/// Service interface for rate limiting HTTP requests per provider using the token bucket algorithm.
/// Enforces configured rate limits (e.g., max 10 requests per minute) to avoid provider IP bans.
/// </summary>
public interface IRateLimiter
{
    /// <summary>
    /// Waits until a rate limit token is available for the specified provider.
    /// Blocks execution if no tokens available until refill occurs.
    /// </summary>
    /// <param name="providerId">Provider identifier (e.g., "hellofresh")</param>
    /// <param name="cancellationToken">Cancellation token for graceful shutdown</param>
    /// <exception cref="OperationCanceledException">Thrown if cancellation requested</exception>
    Task WaitForTokenAsync(
        string providerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to acquire a rate limit token without blocking.
    /// Returns true if token acquired, false if no tokens available.
    /// </summary>
    /// <param name="providerId">Provider identifier</param>
    /// <returns>True if token acquired, false otherwise</returns>
    bool TryAcquireToken(string providerId);

    /// <summary>
    /// Configures rate limiting for a provider.
    /// Must be called before first request to the provider (typically during DI setup).
    /// </summary>
    /// <param name="providerId">Provider identifier</param>
    /// <param name="maxRequestsPerMinute">Maximum requests per minute (e.g., 10)</param>
    /// <param name="burstSize">Maximum burst size (optional, defaults to maxRequestsPerMinute)</param>
    void ConfigureProvider(string providerId, int maxRequestsPerMinute, int? burstSize = null);
}
