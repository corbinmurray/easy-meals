namespace EasyMeals.Domain.ProviderConfiguration;

/// <summary>
/// Rate limiting and retry configuration for provider requests.
/// </summary>
public sealed record RateLimitSettings
{
    /// <summary>Maximum requests per minute to this provider.</summary>
    public int RequestsPerMinute { get; init; } = 60;

    /// <summary>Minimum delay between consecutive requests.</summary>
    public TimeSpan DelayBetweenRequests { get; init; } = TimeSpan.FromMilliseconds(100);

    /// <summary>Maximum concurrent requests to this provider.</summary>
    public int MaxConcurrentRequests { get; init; } = 5;

    /// <summary>Maximum retry attempts on transient failures.</summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>Base delay between retry attempts (may be multiplied for backoff).</summary>
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Creates a default rate limit configuration suitable for most providers.
    /// </summary>
    public static RateLimitSettings Default => new();

    /// <summary>
    /// Creates a conservative rate limit configuration for providers with strict limits.
    /// </summary>
    public static RateLimitSettings Conservative => new()
    {
        RequestsPerMinute = 30,
        DelayBetweenRequests = TimeSpan.FromMilliseconds(500),
        MaxConcurrentRequests = 2,
        MaxRetries = 3,
        RetryDelay = TimeSpan.FromSeconds(2)
    };
}
