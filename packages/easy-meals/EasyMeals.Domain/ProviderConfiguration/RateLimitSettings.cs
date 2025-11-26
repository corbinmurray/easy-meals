namespace EasyMeals.Domain.ProviderConfiguration;

public sealed class RateLimitSettings
{
    public int RequestsPerMinute { get; init; }
    public TimeSpan DelayBetweenRequests { get; init; } = TimeSpan.Zero;
    public int MaxRetries { get; init; } = 0;

    public RateLimitSettings(int requestsPerMinute)
    {
        if (requestsPerMinute <= 0) throw new ArgumentOutOfRangeException(nameof(requestsPerMinute));
        RequestsPerMinute = requestsPerMinute;
    }
}
