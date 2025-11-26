using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace EasyMeals.RecipeEngine.Infrastructure.Metrics;

/// <summary>
/// Metrics for provider configuration operations.
/// Provides observability for cache performance and configuration loading.
/// </summary>
public sealed class ProviderConfigurationMetrics : IDisposable
{
    /// <summary>
    /// Meter name for provider configuration metrics.
    /// </summary>
    public const string MeterName = "EasyMeals.RecipeEngine.ProviderConfiguration";

    private readonly Meter _meter;
    private readonly Counter<long> _cacheHitCounter;
    private readonly Counter<long> _cacheMissCounter;
    private readonly Counter<long> _validationErrorCounter;
    private readonly Histogram<double> _loadDurationHistogram;

    public ProviderConfigurationMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        _meter = meterFactory.Create(MeterName);

        _cacheHitCounter = _meter.CreateCounter<long>(
            "provider_config.cache.hits",
            unit: "hits",
            description: "Number of cache hits for provider configuration lookups");

        _cacheMissCounter = _meter.CreateCounter<long>(
            "provider_config.cache.misses",
            unit: "misses",
            description: "Number of cache misses for provider configuration lookups");

        _validationErrorCounter = _meter.CreateCounter<long>(
            "provider_config.validation.errors",
            unit: "errors",
            description: "Number of provider configuration validation errors");

        _loadDurationHistogram = _meter.CreateHistogram<double>(
            "provider_config.load.duration",
            unit: "ms",
            description: "Duration of provider configuration load operations");
    }

    /// <summary>
    /// Records a cache hit.
    /// </summary>
    /// <param name="operationType">The type of operation (e.g., "GetById", "GetAllEnabled").</param>
    public void RecordCacheHit(string operationType)
    {
        _cacheHitCounter.Add(1, new KeyValuePair<string, object?>("operation", operationType));
    }

    /// <summary>
    /// Records a cache miss.
    /// </summary>
    /// <param name="operationType">The type of operation (e.g., "GetById", "GetAllEnabled").</param>
    public void RecordCacheMiss(string operationType)
    {
        _cacheMissCounter.Add(1, new KeyValuePair<string, object?>("operation", operationType));
    }

    /// <summary>
    /// Records a validation error.
    /// </summary>
    /// <param name="errorType">The type of validation error.</param>
    public void RecordValidationError(string errorType)
    {
        _validationErrorCounter.Add(1, new KeyValuePair<string, object?>("error_type", errorType));
    }

    /// <summary>
    /// Records the duration of a load operation.
    /// </summary>
    /// <param name="durationMs">Duration in milliseconds.</param>
    /// <param name="operationType">The type of operation.</param>
    public void RecordLoadDuration(double durationMs, string operationType)
    {
        _loadDurationHistogram.Record(durationMs, new KeyValuePair<string, object?>("operation", operationType));
    }

    /// <summary>
    /// Creates a timing scope for measuring operation duration.
    /// </summary>
    /// <param name="operationType">The type of operation being timed.</param>
    /// <returns>A disposable timing scope.</returns>
    public TimingScope StartTiming(string operationType) => new(this, operationType);

    public void Dispose()
    {
        _meter.Dispose();
    }

    /// <summary>
    /// A timing scope that records duration on disposal.
    /// </summary>
    public readonly struct TimingScope : IDisposable
    {
        private readonly ProviderConfigurationMetrics _metrics;
        private readonly string _operationType;
        private readonly Stopwatch _stopwatch;

        internal TimingScope(ProviderConfigurationMetrics metrics, string operationType)
        {
            _metrics = metrics;
            _operationType = operationType;
            _stopwatch = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            _metrics.RecordLoadDuration(_stopwatch.Elapsed.TotalMilliseconds, _operationType);
        }
    }
}
