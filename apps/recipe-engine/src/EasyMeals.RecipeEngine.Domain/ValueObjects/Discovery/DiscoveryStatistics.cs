namespace EasyMeals.RecipeEngine.Domain.ValueObjects.Discovery;

/// <summary>
///     Value object containing discovery performance statistics
/// </summary>
public sealed record DiscoveryStatistics(
    int TotalUrlsDiscovered,
    int RecipeUrlsFound,
    int FailedRequests,
    decimal AverageConfidence,
    TimeSpan AverageDiscoveryTime,
    Dictionary<int, int> UrlsByDepth,
    DateTime GeneratedAt)
{
    /// <summary>
    ///     Success rate of discovery operations
    /// </summary>
    public decimal SuccessRate => TotalUrlsDiscovered > 0
        ? (decimal)(TotalUrlsDiscovered - FailedRequests) / TotalUrlsDiscovered
        : 0m;

    /// <summary>
    ///     Recipe discovery efficiency (recipes found / total URLs)
    /// </summary>
    public decimal RecipeDiscoveryRate => TotalUrlsDiscovered > 0
        ? (decimal)RecipeUrlsFound / TotalUrlsDiscovered
        : 0m;

    /// <summary>
    ///     Most productive discovery depth
    /// </summary>
    public int MostProductiveDepth => UrlsByDepth.Count > 0
        ? UrlsByDepth.OrderByDescending(kvp => kvp.Value).First().Key
        : 0;
}