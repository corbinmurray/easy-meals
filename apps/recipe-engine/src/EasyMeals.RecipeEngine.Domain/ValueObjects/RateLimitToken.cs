namespace EasyMeals.RecipeEngine.Domain.ValueObjects;

/// <summary>
///     Value object representing rate limit token state for a provider.
///     Implements token bucket algorithm for rate limiting.
/// </summary>
public class RateLimitToken
{
	public int AvailableTokens { get; private set; }
	public int MaxTokens { get; }
	public TimeSpan RefillRate { get; }
	public DateTime LastRefillAt { get; private set; }

	public RateLimitToken(int maxTokens, TimeSpan refillRate)
	{
		if (maxTokens <= 0)
			throw new ArgumentException("MaxTokens must be positive", nameof(maxTokens));

		if (refillRate <= TimeSpan.Zero)
			throw new ArgumentException("RefillRate must be positive", nameof(refillRate));

		MaxTokens = maxTokens;
		RefillRate = refillRate;
		AvailableTokens = maxTokens;
		LastRefillAt = DateTime.UtcNow;
	}

    /// <summary>
    ///     Consume one token from the bucket.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when no tokens are available.</exception>
    public void ConsumeToken()
	{
		if (AvailableTokens <= 0)
			throw new InvalidOperationException("No tokens available. Rate limit exceeded.");

		AvailableTokens--;
	}

    /// <summary>
    ///     Refill tokens based on elapsed time since last refill.
    /// </summary>
    public void RefillTokens()
	{
		DateTime now = DateTime.UtcNow;
		TimeSpan elapsed = now - LastRefillAt;
		var tokensToAdd = (int)(elapsed / RefillRate);

		if (tokensToAdd > 0)
		{
			AvailableTokens = Math.Min(AvailableTokens + tokensToAdd, MaxTokens);
			LastRefillAt = now;
		}
	}

    /// <summary>
    ///     Check if at least one token is available without consuming it.
    /// </summary>
    public bool HasAvailableToken()
	{
		RefillTokens();
		return AvailableTokens > 0;
	}
}