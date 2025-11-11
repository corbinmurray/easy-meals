using System.Collections.Concurrent;
using EasyMeals.RecipeEngine.Application.Interfaces;

namespace EasyMeals.RecipeEngine.Infrastructure.RateLimiting;

/// <summary>
///     Token bucket rate limiter implementation for controlling request frequency.
///     Implements the token bucket algorithm with automatic token refill.
///     Supports provider-specific rate limiting by maintaining separate token buckets per provider ID.
/// </summary>
public class TokenBucketRateLimiter : IRateLimiter
{
	private readonly int _maxTokens;
	private readonly double _refillRatePerSecond;

	// Provider-specific token buckets: key = ProviderId, value = TokenBucket
	// Each provider gets its own isolated token bucket for rate limiting
	private readonly ConcurrentDictionary<string, TokenBucket> _buckets;

	public TokenBucketRateLimiter(int maxTokens, int refillRatePerMinute)
	{
		if (maxTokens <= 0)
			throw new ArgumentException("Max tokens must be positive", nameof(maxTokens));

		if (refillRatePerMinute <= 0)
			throw new ArgumentException("Refill rate must be positive", nameof(refillRatePerMinute));

		_maxTokens = maxTokens;
		_refillRatePerSecond = refillRatePerMinute / 60.0;
		_buckets = new ConcurrentDictionary<string, TokenBucket>();
	}

    /// <summary>
    ///     Attempts to acquire a single permit for the specified provider.
    ///     Each provider has its own isolated token bucket for rate limiting.
    /// </summary>
    /// <param name="key">Provider identifier (e.g., "provider_001")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if permit was acquired, false if rate limit exceeded</returns>
    public Task<bool> TryAcquireAsync(string key, CancellationToken cancellationToken = default) => TryAcquireAsync(key, 1, cancellationToken);

    /// <summary>
    ///     Attempts to acquire multiple permits for the specified provider.
    ///     Each provider has its own isolated token bucket for rate limiting.
    /// </summary>
    /// <param name="key">Provider identifier (e.g., "provider_001")</param>
    /// <param name="permits">Number of permits to acquire</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if all permits were acquired, false if rate limit exceeded</returns>
    public Task<bool> TryAcquireAsync(string key, int permits, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(key))
			throw new ArgumentException("Key cannot be empty", nameof(key));

		if (permits <= 0)
			throw new ArgumentException("Permits must be positive", nameof(permits));

		// Get or create provider-specific bucket
		TokenBucket bucket = GetOrCreateBucketForProvider(key);
		bool acquired = bucket.TryConsume(permits);

		return Task.FromResult(acquired);
	}

    /// <summary>
    ///     Gets the current rate limit status for the specified provider.
    /// </summary>
    /// <param name="key">Provider identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Rate limit status including remaining requests and reset time</returns>
    public Task<RateLimitStatus> GetStatusAsync(string key, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(key))
			throw new ArgumentException("Key cannot be empty", nameof(key));

		TokenBucket bucket = GetOrCreateBucketForProvider(key);
		int remaining = bucket.GetAvailableTokens();
		TimeSpan resetTime = bucket.GetResetTime();
		bool isLimited = remaining == 0;

		var status = new RateLimitStatus(
			remaining,
			resetTime,
			isLimited
		);

		return Task.FromResult(status);
	}

    /// <summary>
    ///     Resets the rate limit for the specified provider by refilling its token bucket.
    /// </summary>
    /// <param name="key">Provider identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public Task ResetAsync(string key, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(key))
			throw new ArgumentException("Key cannot be empty", nameof(key));

		TokenBucket bucket = GetOrCreateBucketForProvider(key);
		bucket.Reset();

		return Task.CompletedTask;
	}

    /// <summary>
    ///     Gets or creates a provider-specific token bucket.
    ///     Each provider gets its own isolated bucket to prevent cross-provider rate limit interference.
    /// </summary>
    /// <param name="providerId">Provider identifier</param>
    /// <returns>Token bucket for the specified provider</returns>
    private TokenBucket GetOrCreateBucketForProvider(string providerId)
	{
		return _buckets.GetOrAdd(providerId, _ => new TokenBucket(_maxTokens, _refillRatePerSecond));
	}

    /// <summary>
    ///     Internal class representing a single token bucket for a specific key.
    /// </summary>
    private class TokenBucket
	{
		private readonly int _maxTokens;
		private readonly double _refillRatePerSecond;
		private readonly object _lock = new();

		private double _availableTokens;
		private DateTime _lastRefillTime;

		public TokenBucket(int maxTokens, double refillRatePerSecond)
		{
			_maxTokens = maxTokens;
			_refillRatePerSecond = refillRatePerSecond;
			_availableTokens = maxTokens;
			_lastRefillTime = DateTime.UtcNow;
		}

		public bool TryConsume(int permits)
		{
			lock (_lock)
			{
				RefillTokens();

				if (_availableTokens >= permits)
				{
					_availableTokens -= permits;
					return true;
				}

				return false;
			}
		}

		public int GetAvailableTokens()
		{
			lock (_lock)
			{
				RefillTokens();
				return (int)Math.Floor(_availableTokens);
			}
		}

		public TimeSpan GetResetTime()
		{
			lock (_lock)
			{
				RefillTokens();

				if (_availableTokens >= _maxTokens)
					return TimeSpan.Zero;

				// Calculate time until next token is available
				double tokensNeeded = 1 - (_availableTokens - Math.Floor(_availableTokens));
				double secondsUntilNextToken = tokensNeeded / _refillRatePerSecond;

				return TimeSpan.FromSeconds(secondsUntilNextToken);
			}
		}

		public void Reset()
		{
			lock (_lock)
			{
				_availableTokens = _maxTokens;
				_lastRefillTime = DateTime.UtcNow;
			}
		}

		private void RefillTokens()
		{
			DateTime now = DateTime.UtcNow;
			double elapsed = (now - _lastRefillTime).TotalSeconds;

			if (elapsed > 0)
			{
				double tokensToAdd = elapsed * _refillRatePerSecond;
				_availableTokens = Math.Min(_maxTokens, _availableTokens + tokensToAdd);
				_lastRefillTime = now;
			}
		}
	}
}