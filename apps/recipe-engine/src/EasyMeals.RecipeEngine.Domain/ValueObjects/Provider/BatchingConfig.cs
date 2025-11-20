namespace EasyMeals.RecipeEngine.Domain.ValueObjects.Provider;

/// <summary>
///     Immutable value object representing batching configuration settings.
/// </summary>
public sealed record BatchingConfig
{
    /// <summary>
    ///     Number of recipes to process in a single batch.
    /// </summary>
    public int BatchSize { get; }

    /// <summary>
    ///     Time window for batch processing.
    /// </summary>
    public TimeSpan TimeWindow { get; }

    public BatchingConfig(int batchSize, int timeWindowMinutes)
    {
        if (batchSize <= 0)
            throw new ArgumentException("BatchSize must be positive", nameof(batchSize));

        if (timeWindowMinutes <= 0)
            throw new ArgumentException("TimeWindow must be positive", nameof(timeWindowMinutes));

        BatchSize = batchSize;
        TimeWindow = TimeSpan.FromMinutes(timeWindowMinutes);
    }
}
