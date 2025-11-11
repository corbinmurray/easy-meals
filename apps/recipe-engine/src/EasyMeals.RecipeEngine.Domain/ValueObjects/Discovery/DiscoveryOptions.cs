namespace EasyMeals.RecipeEngine.Domain.ValueObjects.Discovery;

/// <summary>
///     Value object containing discovery configuration options
///     Immutable configuration for discovery operations
/// </summary>
public sealed record DiscoveryOptions
{
    /// <summary>
    ///     Creates discovery options with validation
    /// </summary>
    /// <param name="maxDepth">Maximum recursion depth</param>
    /// <param name="maxUrls">Maximum URLs to discover</param>
    /// <param name="minConfidence">Minimum confidence threshold</param>
    /// <param name="respectRobotsTxt">Whether to respect robots.txt</param>
    /// <param name="delayBetweenRequests">Delay between requests</param>
    /// <param name="userAgent">User agent string</param>
    /// <param name="includePatterns">URL patterns to include</param>
    /// <param name="excludePatterns">URL patterns to exclude</param>
    /// <param name="customSettings">Additional provider-specific settings</param>
    public DiscoveryOptions(
        int maxDepth = 3,
        int maxUrls = 1000,
        decimal minConfidence = 0.6m,
        bool respectRobotsTxt = true,
        TimeSpan? delayBetweenRequests = null,
        string? userAgent = null,
        IEnumerable<string>? includePatterns = null,
        IEnumerable<string>? excludePatterns = null,
        Dictionary<string, object>? customSettings = null)
    {
        MaxDepth = ValidateMaxDepth(maxDepth);
        MaxUrls = ValidateMaxUrls(maxUrls);
        MinConfidence = ValidateMinConfidence(minConfidence);
        RespectRobotsTxt = respectRobotsTxt;
        DelayBetweenRequests = delayBetweenRequests ?? TimeSpan.FromMilliseconds(1000);
        UserAgent = string.IsNullOrWhiteSpace(userAgent)
            ? "EasyMeals Recipe Engine/1.0"
            : userAgent.Trim();

        IncludePatterns = includePatterns?.ToList().AsReadOnly() ??
                          new List<string>().AsReadOnly();
        ExcludePatterns = excludePatterns?.ToList().AsReadOnly() ??
                          new List<string>().AsReadOnly();
        CustomSettings = customSettings != null
            ? new Dictionary<string, object>(customSettings)
            : new Dictionary<string, object>();
    }

    /// <summary>Maximum recursion depth for discovery</summary>
    public int MaxDepth { get; init; }

    /// <summary>Maximum number of URLs to discover</summary>
    public int MaxUrls { get; init; }

    /// <summary>Minimum confidence threshold for including URLs</summary>
    public decimal MinConfidence { get; init; }

    /// <summary>Whether to respect robots.txt files</summary>
    public bool RespectRobotsTxt { get; init; }

    /// <summary>Delay between HTTP requests to be respectful</summary>
    public TimeSpan DelayBetweenRequests { get; init; }

    /// <summary>User agent string for HTTP requests</summary>
    public string UserAgent { get; init; }

    /// <summary>URL patterns to include (glob patterns)</summary>
    public IReadOnlyList<string> IncludePatterns { get; init; }

    /// <summary>URL patterns to exclude (glob patterns)</summary>
    public IReadOnlyList<string> ExcludePatterns { get; init; }

    /// <summary>Additional provider-specific settings</summary>
    public IReadOnlyDictionary<string, object> CustomSettings { get; init; }

    /// <summary>
    ///     Indicates if discovery should be aggressive (high depth, low confidence threshold)
    /// </summary>
    public bool IsAggressive => MaxDepth > 3 && MinConfidence < 0.5m;

    /// <summary>
    ///     Indicates if discovery should be conservative (low depth, high confidence threshold)
    /// </summary>
    public bool IsConservative => MaxDepth <= 2 && MinConfidence >= 0.8m;

    /// <summary>
    ///     Gets a custom setting value with type safety
    /// </summary>
    public T? GetCustomSetting<T>(string key)
    {
        if (CustomSettings.TryGetValue(key, out object? value) && value is T typedValue)
            return typedValue;
        return default;
    }

    /// <summary>
    ///     Creates a copy with updated max depth
    /// </summary>
    public DiscoveryOptions WithMaxDepth(int maxDepth) => this with { MaxDepth = ValidateMaxDepth(maxDepth) };

    /// <summary>
    ///     Creates a copy with updated max URLs
    /// </summary>
    public DiscoveryOptions WithMaxUrls(int maxUrls) => this with { MaxUrls = ValidateMaxUrls(maxUrls) };

    /// <summary>
    ///     Creates a copy with additional include pattern
    /// </summary>
    public DiscoveryOptions WithIncludePattern(string pattern)
    {
        List<string> newPatterns = IncludePatterns.ToList();
        newPatterns.Add(pattern);
        return this with { IncludePatterns = newPatterns.AsReadOnly() };
    }

    /// <summary>
    ///     Creates a copy with additional exclude pattern
    /// </summary>
    public DiscoveryOptions WithExcludePattern(string pattern)
    {
        List<string> newPatterns = ExcludePatterns.ToList();
        newPatterns.Add(pattern);
        return this with { ExcludePatterns = newPatterns.AsReadOnly() };
    }

    /// <summary>
    ///     Predefined conservative discovery options
    /// </summary>
    public static DiscoveryOptions Conservative => new(
        2,
        100,
        0.8m,
        true,
        TimeSpan.FromMilliseconds(2000));

    /// <summary>
    ///     Predefined aggressive discovery options
    /// </summary>
    public static DiscoveryOptions Aggressive => new(
        5,
        5000,
        0.4m,
        true,
        TimeSpan.FromMilliseconds(500));

    /// <summary>
    ///     Default balanced discovery options
    /// </summary>
    public static DiscoveryOptions Default => new();

    #region Validation Methods

    private static int ValidateMaxDepth(int maxDepth)
    {
        if (maxDepth < 1)
            throw new ArgumentOutOfRangeException(nameof(maxDepth), "Max depth must be at least 1");

        if (maxDepth > 10)
            throw new ArgumentOutOfRangeException(nameof(maxDepth), "Max depth cannot exceed 10");

        return maxDepth;
    }

    private static int ValidateMaxUrls(int maxUrls)
    {
        if (maxUrls < 1)
            throw new ArgumentOutOfRangeException(nameof(maxUrls), "Max URLs must be at least 1");

        if (maxUrls > 100000)
            throw new ArgumentOutOfRangeException(nameof(maxUrls), "Max URLs cannot exceed 100,000");

        return maxUrls;
    }

    private static decimal ValidateMinConfidence(decimal minConfidence)
    {
        if (minConfidence < 0.0m || minConfidence > 1.0m)
            throw new ArgumentOutOfRangeException(nameof(minConfidence), "Min confidence must be between 0.0 and 1.0");

        return minConfidence;
    }

    #endregion
}