using EasyMeals.Domain.ProviderConfiguration;
using EasyMeals.Persistence.Abstractions.Repositories;
using EasyMeals.RecipeEngine.Infrastructure.Metrics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EasyMeals.RecipeEngine.Infrastructure.Caching;

/// <summary>
/// Caching decorator for the provider configuration repository.
/// Implements cache-aside pattern with configurable TTL.
/// </summary>
public class CachedProviderConfigurationRepository : ICacheableProviderConfigurationRepository
{
    private const string AllEnabledCacheKey = "provider_configs:all_enabled";
    private const string ByIdCacheKeyPrefix = "provider_configs:id:";
    private const string ByNameCacheKeyPrefix = "provider_configs:name:";

    private readonly IProviderConfigurationRepository _inner;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CachedProviderConfigurationRepository> _logger;
    private readonly ProviderConfigurationCacheOptions _options;
    private readonly ProviderConfigurationMetrics? _metrics;

    public CachedProviderConfigurationRepository(
        IProviderConfigurationRepository inner,
        IMemoryCache cache,
        IOptions<ProviderConfigurationCacheOptions> options,
        ILogger<CachedProviderConfigurationRepository> logger,
        ProviderConfigurationMetrics? metrics = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _metrics = metrics;
    }

    /// <inheritdoc />
    public async Task<ProviderConfiguration?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        if (!_options.Enabled)
            return await _inner.GetByIdAsync(id, ct);

        var cacheKey = $"{ByIdCacheKeyPrefix}{id}";

        if (_cache.TryGetValue(cacheKey, out ProviderConfiguration? cached))
        {
            _logger.LogDebug("Cache hit for provider configuration by ID: {Id}", id);
            _metrics?.RecordCacheHit("GetById");
            return cached;
        }

        _logger.LogDebug("Cache miss for provider configuration by ID: {Id}", id);
        _metrics?.RecordCacheMiss("GetById");

        using var timing = _metrics?.StartTiming("GetById");
        var result = await _inner.GetByIdAsync(id, ct);

        if (result is not null)
        {
            _cache.Set(cacheKey, result, _options.CacheTtl);
            _logger.LogDebug("Cached provider configuration: {ProviderName}", result.ProviderName);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProviderConfiguration>> GetAllEnabledAsync(CancellationToken ct = default)
    {
        if (!_options.Enabled)
            return await _inner.GetAllEnabledAsync(ct);

        if (_cache.TryGetValue(AllEnabledCacheKey, out IReadOnlyList<ProviderConfiguration>? cached))
        {
            _logger.LogDebug("Cache hit for all enabled provider configurations. Count: {Count}", cached?.Count ?? 0);
            _metrics?.RecordCacheHit("GetAllEnabled");
            return cached ?? [];
        }

        _logger.LogDebug("Cache miss for all enabled provider configurations");
        _metrics?.RecordCacheMiss("GetAllEnabled");

        using var timing = _metrics?.StartTiming("GetAllEnabled");
        var result = await _inner.GetAllEnabledAsync(ct);

        _cache.Set(AllEnabledCacheKey, result, _options.CacheTtl);
        _logger.LogDebug("Cached {Count} enabled provider configurations", result.Count);

        return result;
    }

    /// <inheritdoc />
    public async Task<ProviderConfiguration?> GetByProviderNameAsync(string providerName, CancellationToken ct = default)
    {
        if (!_options.Enabled)
            return await _inner.GetByProviderNameAsync(providerName, ct);

        var normalizedName = providerName?.ToLowerInvariant().Trim() ?? string.Empty;
        var cacheKey = $"{ByNameCacheKeyPrefix}{normalizedName}";

        if (_cache.TryGetValue(cacheKey, out ProviderConfiguration? cached))
        {
            _logger.LogDebug("Cache hit for provider configuration by name: {ProviderName}", normalizedName);
            _metrics?.RecordCacheHit("GetByName");
            return cached;
        }

        _logger.LogDebug("Cache miss for provider configuration by name: {ProviderName}", normalizedName);
        _metrics?.RecordCacheMiss("GetByName");

        using var timing = _metrics?.StartTiming("GetByName");
        var result = await _inner.GetByProviderNameAsync(providerName ?? string.Empty, ct);

        if (result is not null)
        {
            _cache.Set(cacheKey, result, _options.CacheTtl);
        }

        return result;
    }

    /// <inheritdoc />
    public Task<bool> ExistsByProviderNameAsync(string providerName, CancellationToken ct = default)
    {
        // Do not cache existence checks - they need to be real-time for uniqueness validation
        return _inner.ExistsByProviderNameAsync(providerName, ct);
    }

    /// <inheritdoc />
    public async Task<string> AddAsync(ProviderConfiguration entity, CancellationToken ct = default)
    {
        var result = await _inner.AddAsync(entity, ct);

        // Invalidate the all-enabled cache as a new provider may be enabled
        InvalidateCache();

        _logger.LogInformation("Added provider configuration: {ProviderName}, invalidated cache", entity.ProviderName);

        return result;
    }

    /// <inheritdoc />
    public async Task UpdateAsync(ProviderConfiguration entity, CancellationToken ct = default)
    {
        await _inner.UpdateAsync(entity, ct);

        // Invalidate relevant caches
        InvalidateCache();
        _cache.Remove($"{ByIdCacheKeyPrefix}{entity.Id}");
        _cache.Remove($"{ByNameCacheKeyPrefix}{entity.ProviderName}");

        _logger.LogInformation("Updated provider configuration: {ProviderName}, invalidated cache", entity.ProviderName);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        await _inner.DeleteAsync(id, ct);

        // Invalidate all caches
        InvalidateCache();
        _cache.Remove($"{ByIdCacheKeyPrefix}{id}");

        _logger.LogInformation("Deleted provider configuration: {Id}, invalidated cache", id);
    }

    /// <inheritdoc />
    public void ClearCache()
    {
        InvalidateCache();
        _logger.LogInformation("Provider configuration cache cleared");
    }

    private void InvalidateCache()
    {
        _cache.Remove(AllEnabledCacheKey);
    }
}
