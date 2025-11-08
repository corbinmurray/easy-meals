namespace EasyMeals.RecipeEngine.Application.Interfaces;

/// <summary>
///     Rate limiting contract for controlling request frequency.
/// </summary>
public interface IRateLimiter
{
	Task<bool> TryAcquireAsync(string key, CancellationToken cancellationToken = default);
	Task<bool> TryAcquireAsync(string key, int permits, CancellationToken cancellationToken = default);
	Task<RateLimitStatus> GetStatusAsync(string key, CancellationToken cancellationToken = default);
	Task ResetAsync(string key, CancellationToken cancellationToken = default);
}

public record RateLimitStatus(
	int RemainingRequests,
	TimeSpan ResetTime,
	bool IsLimited);