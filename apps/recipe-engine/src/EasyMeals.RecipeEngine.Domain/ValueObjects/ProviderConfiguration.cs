namespace EasyMeals.RecipeEngine.Domain.ValueObjects;

/// <summary>
/// Immutable value object representing provider-specific configuration.
/// Loaded from MongoDB at runtime for security (URLs never committed to GitHub).
/// </summary>
public class ProviderConfiguration
{
    public string ProviderId { get; }
    public bool Enabled { get; }
    public DiscoveryStrategy DiscoveryStrategy { get; }
    public string RecipeRootUrl { get; }
    public int BatchSize { get; }
    public TimeSpan TimeWindow { get; }
    public TimeSpan MinDelay { get; }
    public int MaxRequestsPerMinute { get; }
    public int RetryCount { get; }
    public TimeSpan RequestTimeout { get; }

    public ProviderConfiguration(
        string providerId,
        bool enabled,
        DiscoveryStrategy discoveryStrategy,
        string recipeRootUrl,
        int batchSize,
        int timeWindowMinutes,
        double minDelaySeconds,
        int maxRequestsPerMinute,
        int retryCount,
        int requestTimeoutSeconds)
    {
        if (string.IsNullOrWhiteSpace(providerId))
            throw new ArgumentException("ProviderId is required", nameof(providerId));

        if (string.IsNullOrWhiteSpace(recipeRootUrl))
            throw new ArgumentException("RecipeRootUrl is required", nameof(recipeRootUrl));

        if (!Uri.IsWellFormedUriString(recipeRootUrl, UriKind.Absolute))
            throw new ArgumentException("RecipeRootUrl must be a valid absolute URL", nameof(recipeRootUrl));

        if (!recipeRootUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("RecipeRootUrl must use HTTPS", nameof(recipeRootUrl));

        if (batchSize <= 0)
            throw new ArgumentException("BatchSize must be positive", nameof(batchSize));

        if (timeWindowMinutes <= 0)
            throw new ArgumentException("TimeWindow must be positive", nameof(timeWindowMinutes));

        if (minDelaySeconds < 0)
            throw new ArgumentException("MinDelay cannot be negative", nameof(minDelaySeconds));

        if (maxRequestsPerMinute <= 0)
            throw new ArgumentException("MaxRequestsPerMinute must be positive", nameof(maxRequestsPerMinute));

        if (retryCount < 0)
            throw new ArgumentException("RetryCount cannot be negative", nameof(retryCount));

        if (requestTimeoutSeconds <= 0)
            throw new ArgumentException("RequestTimeout must be positive", nameof(requestTimeoutSeconds));

        ProviderId = providerId;
        Enabled = enabled;
        DiscoveryStrategy = discoveryStrategy;
        RecipeRootUrl = recipeRootUrl;
        BatchSize = batchSize;
        TimeWindow = TimeSpan.FromMinutes(timeWindowMinutes);
        MinDelay = TimeSpan.FromSeconds(minDelaySeconds);
        MaxRequestsPerMinute = maxRequestsPerMinute;
        RetryCount = retryCount;
        RequestTimeout = TimeSpan.FromSeconds(requestTimeoutSeconds);
    }

    public override bool Equals(object? obj)
    {
        return obj is ProviderConfiguration other && ProviderId == other.ProviderId;
    }

    public override int GetHashCode()
    {
        return ProviderId.GetHashCode();
    }
}
