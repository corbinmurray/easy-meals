using System.Collections.Concurrent;
using EasyMeals.RecipeEngine.Application.Interfaces;
using EasyMeals.RecipeEngine.Domain.ValueObjects;
using EasyMeals.RecipeEngine.Infrastructure.Documents;
using EasyMeals.Shared.Data.Repositories;

namespace EasyMeals.RecipeEngine.Infrastructure.Services;

/// <summary>
///     Loads provider configurations from MongoDB with in-memory caching.
/// </summary>
public class ProviderConfigurationLoader(IMongoRepository<ProviderConfigurationDocument> repository) : IProviderConfigurationLoader
{
	private readonly IMongoRepository<ProviderConfigurationDocument> _repository = repository ?? throw new ArgumentNullException(nameof(repository));
	private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
	private readonly TimeSpan _cacheTtl = TimeSpan.FromHours(1); // 1 hour TTL as per T087

	public async Task<ProviderConfiguration?> GetByProviderIdAsync(
		string providerId,
		CancellationToken cancellationToken = default)
	{
		// Check cache first
		if (_cache.TryGetValue(providerId, out CacheEntry? entry) && !entry.IsExpired) return entry.Configuration;

		// Load from MongoDB if not cached or expired
		ProviderConfigurationDocument? document = await _repository.GetFirstOrDefaultAsync(
			d => d.ProviderId == providerId && d.Enabled,
			cancellationToken);

		if (document == null)
		{
			// Cache negative result (not found) to avoid repeated DB queries
			_cache[providerId] = new CacheEntry(null, _cacheTtl);
			return null;
		}

		ProviderConfiguration config = ToDomain(document);

		// Cache the result
		_cache[providerId] = new CacheEntry(config, _cacheTtl);

		return config;
	}

	public async Task<IEnumerable<ProviderConfiguration>> GetAllEnabledAsync(
		CancellationToken cancellationToken = default)
	{
		// Note: For GetAllEnabledAsync, we don't cache the full collection
		// because it's typically called once at startup via LoadConfigurationsAsync
		// Individual provider configs are cached via GetByProviderIdAsync
		IEnumerable<ProviderConfigurationDocument> documents = await _repository.GetAllAsync(d => d.Enabled, cancellationToken);

		return documents.Select(ToDomain).ToList();
	}

	public async Task LoadConfigurationsAsync(CancellationToken cancellationToken = default)
	{
		// Load all configurations to validate they parse correctly
		IEnumerable<ProviderConfiguration> configs = await GetAllEnabledAsync(cancellationToken);
		List<ProviderConfiguration> configList = configs.ToList();

		if (configList.Count == 0)
			throw new InvalidOperationException(
				"No enabled provider configurations found in MongoDB. " +
				"Please seed the provider_configurations collection.");

		// Populate cache with all configurations for faster subsequent access
		foreach (ProviderConfiguration config in configList)
		{
			_cache[config.ProviderId] = new CacheEntry(config, _cacheTtl);
		}

		// Log successful load (in production, use ILogger)
		Console.WriteLine($"Loaded {configList.Count} provider configuration(s) from MongoDB and cached them");
	}

	/// <summary>
	///     Invalidates the cache for a specific provider, forcing reload on next access.
	/// </summary>
	/// <param name="providerId">Provider ID to invalidate</param>
	public void InvalidateCache(string providerId)
	{
		_cache.TryRemove(providerId, out _);
	}

	/// <summary>
	///     Clears the entire cache, forcing reload for all providers.
	/// </summary>
	public void ClearCache()
	{
		_cache.Clear();
	}

	private static ProviderConfiguration ToDomain(ProviderConfigurationDocument document)
	{
		// Parse discovery strategy from nested discovery config
		if (!Enum.TryParse(document.Discovery.Strategy, true, out DiscoveryStrategy strategy))
			throw new InvalidOperationException($"Invalid DiscoveryStrategy value: {document.Discovery.Strategy}");

		return new ProviderConfiguration(
			document.ProviderId,
			document.Enabled,
			strategy,
			document.Endpoint.RecipeRootUrl,
			document.Batching.BatchSize,
			document.Batching.TimeWindowMinutes,
			document.RateLimit.MinDelaySeconds,
			document.RateLimit.MaxRequestsPerMinute,
			document.RateLimit.RetryCount,
			document.RateLimit.RequestTimeoutSeconds,
			document.Discovery.RecipeUrlPattern,
			document.Discovery.CategoryUrlPattern
		);
	}

	/// <summary>
	///     Cache entry with expiration support.
	/// </summary>
	private class CacheEntry
	{
		public ProviderConfiguration? Configuration { get; }
		public DateTime ExpiresAt { get; }

		public CacheEntry(ProviderConfiguration? configuration, TimeSpan ttl)
		{
			Configuration = configuration;
			ExpiresAt = DateTime.UtcNow.Add(ttl);
		}

		public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
	}
}