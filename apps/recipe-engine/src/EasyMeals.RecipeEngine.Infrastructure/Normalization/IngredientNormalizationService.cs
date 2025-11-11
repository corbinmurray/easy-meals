using System.Collections.Concurrent;
using EasyMeals.RecipeEngine.Application.Interfaces;
using EasyMeals.RecipeEngine.Domain.Entities;
using EasyMeals.RecipeEngine.Domain.Events;
using EasyMeals.RecipeEngine.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace EasyMeals.RecipeEngine.Infrastructure.Normalization;

/// <summary>
///     Service for normalizing provider-specific ingredient codes to canonical forms.
///     Implements caching strategy to minimize database queries for frequently used ingredients.
/// </summary>
public class IngredientNormalizationService : IIngredientNormalizer
{
	private readonly IIngredientMappingRepository _repository;
	private readonly ILogger<IngredientNormalizationService> _logger;
	private readonly IEventBus _eventBus;

	// LRU cache for ingredient mappings
	// Cache key format: "providerId:providerCode"
	private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
	private readonly LinkedList<string> _cacheOrder = new();
	private readonly object _cacheLock = new();
	private const int MaxCacheSize = 1000;
	private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);

	public IngredientNormalizationService(
		IIngredientMappingRepository repository,
		ILogger<IngredientNormalizationService> logger,
		IEventBus eventBus)
	{
		_repository = repository ?? throw new ArgumentNullException(nameof(repository));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
	}

	public async Task<string?> NormalizeAsync(
		string providerId,
		string providerCode,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(providerId))
			throw new ArgumentException("Provider ID cannot be null or whitespace", nameof(providerId));

		if (string.IsNullOrWhiteSpace(providerCode))
			throw new ArgumentException("Provider code cannot be null or whitespace", nameof(providerCode));

		string cacheKey = GetCacheKey(providerId, providerCode);

		// Check cache first
		if (TryGetFromCache(cacheKey, out string? cachedValue))
		{
			_logger.LogDebug(
				"Ingredient normalization cache hit for provider '{ProviderId}', code '{ProviderCode}'",
				providerId,
				providerCode);
			return cachedValue;
		}

		// Query database
		IngredientMapping? mapping = await _repository.GetByCodeAsync(providerId, providerCode, cancellationToken);

		if (mapping is null)
		{
			_logger.LogWarning(
				"No mapping found for provider '{ProviderId}', code '{ProviderCode}'",
				providerId,
				providerCode);

			// Publish event for unmapped ingredient (RecipeUrl is set to empty string as we don't have context here)
			_eventBus.Publish(new IngredientMappingMissingEvent(providerId, providerCode, string.Empty));

			// Cache null result to avoid repeated database queries
			AddToCache(cacheKey, null);
			return null;
		}

		string canonicalForm = mapping.CanonicalForm;

		_logger.LogDebug(
			"Mapped ingredient '{ProviderCode}' to canonical form '{CanonicalForm}' for provider '{ProviderId}'",
			providerCode,
			canonicalForm,
			providerId);

		// Cache the result
		AddToCache(cacheKey, canonicalForm);

		return canonicalForm;
	}

	public async Task<IDictionary<string, string?>> NormalizeBatchAsync(
		string providerId,
		IEnumerable<string> providerCodes,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(providerId))
			throw new ArgumentException("Provider ID cannot be null or whitespace", nameof(providerId));

		if (providerCodes is null)
			throw new ArgumentNullException(nameof(providerCodes));

		var result = new Dictionary<string, string?>();
		List<string> uniqueCodes = providerCodes.Distinct().ToList();

		if (uniqueCodes.Count == 0) return result;

		_logger.LogInformation(
			"Normalizing batch of {Count} unique ingredients for provider '{ProviderId}'",
			uniqueCodes.Count,
			providerId);

		foreach (string code in uniqueCodes)
		{
			string? canonicalForm = await NormalizeAsync(providerId, code, cancellationToken);
			result[code] = canonicalForm;
		}

		int mappedCount = result.Values.Count(v => v is not null);
		int unmappedCount = result.Values.Count(v => v is null);

		_logger.LogInformation(
			"Batch normalization complete for provider '{ProviderId}': {MappedCount} mapped, {UnmappedCount} unmapped",
			providerId,
			mappedCount,
			unmappedCount);

		return result;
	}

	private static string GetCacheKey(string providerId, string providerCode)
		=> $"{providerId}:{providerCode}";

	private bool TryGetFromCache(string cacheKey, out string? value)
	{
		if (_cache.TryGetValue(cacheKey, out CacheEntry? entry))
		{
			// Check if entry is still valid (not expired)
			if (DateTime.UtcNow - entry.Timestamp < CacheTtl)
			{
				// Update access order for LRU
				lock (_cacheLock)
				{
					_cacheOrder.Remove(cacheKey);
					_cacheOrder.AddFirst(cacheKey);
				}

				value = entry.CanonicalForm;
				return true;
			}

			// Entry expired, remove from cache
			_cache.TryRemove(cacheKey, out _);
			lock (_cacheLock) _cacheOrder.Remove(cacheKey);
		}

		value = null;
		return false;
	}

	private void AddToCache(string cacheKey, string? canonicalForm)
	{
		lock (_cacheLock)
		{
			// Evict oldest entry if cache is full
			if (_cache.Count >= MaxCacheSize && !_cache.ContainsKey(cacheKey))
			{
				string? oldestKey = _cacheOrder.Last?.Value;
				if (oldestKey is not null)
				{
					_cache.TryRemove(oldestKey, out _);
					_cacheOrder.RemoveLast();
				}
			}

			var entry = new CacheEntry(canonicalForm, DateTime.UtcNow);
			_cache[cacheKey] = entry;

			// Update LRU order
			_cacheOrder.Remove(cacheKey);
			_cacheOrder.AddFirst(cacheKey);
		}
	}

	private record CacheEntry(string? CanonicalForm, DateTime Timestamp);
}