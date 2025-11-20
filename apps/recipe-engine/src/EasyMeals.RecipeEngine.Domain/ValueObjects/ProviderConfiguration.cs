using EasyMeals.RecipeEngine.Domain.ValueObjects.Provider;

namespace EasyMeals.RecipeEngine.Domain.ValueObjects;

/// <summary>
///     Immutable value object representing provider-specific configuration.
///     Loaded from MongoDB at runtime for security (URLs never committed to GitHub).
///     Refactored to use nested value objects for better organization and extensibility.
/// </summary>
public class ProviderConfiguration
{
    public string ProviderId { get; }
    public bool Enabled { get; }
    public EndpointInfo Endpoint { get; }
    public DiscoveryConfig Discovery { get; }
    public BatchingConfig Batching { get; }
    public RateLimitConfig RateLimit { get; }

    public ProviderConfiguration(
        string providerId,
        bool enabled,
        EndpointInfo endpoint,
        DiscoveryConfig discovery,
        BatchingConfig batching,
        RateLimitConfig rateLimit)
    {
        if (string.IsNullOrWhiteSpace(providerId))
            throw new ArgumentException("ProviderId is required", nameof(providerId));

        ProviderId = providerId;
        Enabled = enabled;
        Endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
        Discovery = discovery ?? throw new ArgumentNullException(nameof(discovery));
        Batching = batching ?? throw new ArgumentNullException(nameof(batching));
        RateLimit = rateLimit ?? throw new ArgumentNullException(nameof(rateLimit));
    }

    // Backward compatibility constructor for existing code
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
        int requestTimeoutSeconds,
        string? recipeUrlPattern = null,
        string? categoryUrlPattern = null)
        : this(
            providerId,
            enabled,
            new EndpointInfo(recipeRootUrl),
            new DiscoveryConfig(discoveryStrategy, recipeUrlPattern, categoryUrlPattern),
            new BatchingConfig(batchSize, timeWindowMinutes),
            new RateLimitConfig(minDelaySeconds, maxRequestsPerMinute, retryCount, requestTimeoutSeconds))
    {
    }

    // Convenience properties for backward compatibility
    public DiscoveryStrategy DiscoveryStrategy => Discovery.Strategy;
    public string RecipeRootUrl => Endpoint.RecipeRootUrl;
    public int BatchSize => Batching.BatchSize;
    public TimeSpan TimeWindow => Batching.TimeWindow;
    public TimeSpan MinDelay => RateLimit.MinDelay;
    public int MaxRequestsPerMinute => RateLimit.MaxRequestsPerMinute;
    public int RetryCount => RateLimit.RetryCount;
    public TimeSpan RequestTimeout => RateLimit.RequestTimeout;
    public string? RecipeUrlPattern => Discovery.RecipeUrlPattern;
    public string? CategoryUrlPattern => Discovery.CategoryUrlPattern;

    public override bool Equals(object? obj) => obj is ProviderConfiguration other && ProviderId == other.ProviderId;

    public override int GetHashCode() => ProviderId.GetHashCode();
}