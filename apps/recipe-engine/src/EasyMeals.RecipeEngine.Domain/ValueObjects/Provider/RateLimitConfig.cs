namespace EasyMeals.RecipeEngine.Domain.ValueObjects.Provider;

/// <summary>
///     Immutable value object representing rate limiting configuration settings.
/// </summary>
public sealed record RateLimitConfig
{
    /// <summary>
    ///     Minimum delay between requests to the provider.
    /// </summary>
    public TimeSpan MinDelay { get; }

    /// <summary>
    ///     Maximum number of requests allowed per minute.
    /// </summary>
    public int MaxRequestsPerMinute { get; }

    /// <summary>
    ///     Number of retry attempts for failed requests.
    /// </summary>
    public int RetryCount { get; }

    /// <summary>
    ///     Timeout for each request to the provider.
    /// </summary>
    public TimeSpan RequestTimeout { get; }

    public RateLimitConfig(
        double minDelaySeconds,
        int maxRequestsPerMinute,
        int retryCount,
        int requestTimeoutSeconds)
    {
        if (minDelaySeconds < 0)
            throw new ArgumentException("MinDelay cannot be negative", nameof(minDelaySeconds));

        if (maxRequestsPerMinute <= 0)
            throw new ArgumentException("MaxRequestsPerMinute must be positive", nameof(maxRequestsPerMinute));

        if (retryCount < 0)
            throw new ArgumentException("RetryCount cannot be negative", nameof(retryCount));

        if (requestTimeoutSeconds <= 0)
            throw new ArgumentException("RequestTimeout must be positive", nameof(requestTimeoutSeconds));

        MinDelay = TimeSpan.FromSeconds(minDelaySeconds);
        MaxRequestsPerMinute = maxRequestsPerMinute;
        RetryCount = retryCount;
        RequestTimeout = TimeSpan.FromSeconds(requestTimeoutSeconds);
    }
}
