namespace EasyMeals.Domain.ProviderConfiguration;

/// <summary>
/// Rate limiting and retry configuration for provider requests.
/// </summary>
public sealed class RateLimitSettings
{
    /// <summary>Maximum requests per minute to this provider.</summary>
    public int RequestsPerMinute { get; private set; }

    /// <summary>Minimum delay between consecutive requests.</summary>
    public TimeSpan DelayBetweenRequests { get; private set; }

    /// <summary>Maximum concurrent requests to this provider.</summary>
    public int MaxConcurrentRequests { get; private set; }

    /// <summary>Maximum retry attempts on transient failures.</summary>
    public int MaxRetries { get; private set; }

    /// <summary>Base delay between retry attempts (may be multiplied for backoff).</summary>
    public TimeSpan RetryDelay { get; private set; }

    /// <summary>
    /// Creates a new instance of RateLimitSettings.
    /// </summary>
    /// <param name="requestsPerMinute">Maximum requests per minute (default: 60).</param>
    /// <param name="delayBetweenRequests">Delay between consecutive requests (default: 100ms).</param>
    /// <param name="maxConcurrentRequests">Maximum concurrent requests (default: 5).</param>
    /// <param name="maxRetries">Maximum retry attempts (default: 3).</param>
    /// <param name="retryDelay">Base delay between retries (default: 1 second).</param>
    public RateLimitSettings(
        int requestsPerMinute = 60,
        TimeSpan? delayBetweenRequests = null,
        int maxConcurrentRequests = 5,
        int maxRetries = 3,
        TimeSpan? retryDelay = null)
    {
        if (requestsPerMinute <= 0)
            throw new ArgumentOutOfRangeException(nameof(requestsPerMinute), "Must be greater than 0.");
        if (maxConcurrentRequests <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxConcurrentRequests), "Must be greater than 0.");
        if (maxRetries < 0)
            throw new ArgumentOutOfRangeException(nameof(maxRetries), "Cannot be negative.");

        RequestsPerMinute = requestsPerMinute;
        DelayBetweenRequests = delayBetweenRequests ?? TimeSpan.FromMilliseconds(100);
        MaxConcurrentRequests = maxConcurrentRequests;
        MaxRetries = maxRetries;
        RetryDelay = retryDelay ?? TimeSpan.FromSeconds(1);
    }

    /// <summary>
    /// Creates a default rate limit configuration suitable for most providers.
    /// </summary>
    public static RateLimitSettings Default => new();

    /// <summary>
    /// Creates a conservative rate limit configuration for providers with strict limits.
    /// </summary>
    public static RateLimitSettings Conservative => new(
        requestsPerMinute: 30,
        delayBetweenRequests: TimeSpan.FromMilliseconds(500),
        maxConcurrentRequests: 2,
        maxRetries: 3,
        retryDelay: TimeSpan.FromSeconds(2));
}
