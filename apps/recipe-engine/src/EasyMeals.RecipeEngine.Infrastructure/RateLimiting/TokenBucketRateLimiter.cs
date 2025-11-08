using EasyMeals.RecipeEngine.Application.Interfaces;
using System.Collections.Concurrent;

namespace EasyMeals.RecipeEngine.Infrastructure.RateLimiting;

/// <summary>
/// Token bucket rate limiter implementation for controlling request frequency.
/// Implements the token bucket algorithm with automatic token refill.
/// </summary>
public class TokenBucketRateLimiter : IRateLimiter
{
    private readonly int _maxTokens;
    private readonly double _refillRatePerSecond;
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

    public Task<bool> TryAcquireAsync(string key, CancellationToken cancellationToken = default)
    {
        return TryAcquireAsync(key, permits: 1, cancellationToken);
    }

    public Task<bool> TryAcquireAsync(string key, int permits, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be empty", nameof(key));

        if (permits <= 0)
            throw new ArgumentException("Permits must be positive", nameof(permits));

        var bucket = GetOrCreateBucket(key);
        var acquired = bucket.TryConsume(permits);

        return Task.FromResult(acquired);
    }

    public Task<RateLimitStatus> GetStatusAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be empty", nameof(key));

        var bucket = GetOrCreateBucket(key);
        var remaining = bucket.GetAvailableTokens();
        var resetTime = bucket.GetResetTime();
        var isLimited = remaining == 0;

        var status = new RateLimitStatus(
            RemainingRequests: remaining,
            ResetTime: resetTime,
            IsLimited: isLimited
        );

        return Task.FromResult(status);
    }

    public Task ResetAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be empty", nameof(key));

        var bucket = GetOrCreateBucket(key);
        bucket.Reset();

        return Task.CompletedTask;
    }

    private TokenBucket GetOrCreateBucket(string key)
    {
        return _buckets.GetOrAdd(key, _ => new TokenBucket(_maxTokens, _refillRatePerSecond));
    }

    /// <summary>
    /// Internal class representing a single token bucket for a specific key.
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
                var tokensNeeded = 1 - (_availableTokens - Math.Floor(_availableTokens));
                var secondsUntilNextToken = tokensNeeded / _refillRatePerSecond;

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
            var now = DateTime.UtcNow;
            var elapsed = (now - _lastRefillTime).TotalSeconds;

            if (elapsed > 0)
            {
                var tokensToAdd = elapsed * _refillRatePerSecond;
                _availableTokens = Math.Min(_maxTokens, _availableTokens + tokensToAdd);
                _lastRefillTime = now;
            }
        }
    }
}
