namespace EasyMeals.RecipeEngine.Domain.ValueObjects.Discovery;

/// <summary>
///     Value object representing a discovered recipe URL with metadata
///     Immutable and self-validating following DDD principles
/// </summary>
public sealed record DiscoveredUrl
{
    /// <summary>
    ///     Creates a new discovered URL with validation
    /// </summary>
    /// <param name="url">The discovered URL</param>
    /// <param name="provider">Source provider name</param>
    /// <param name="discoveredAt">When the URL was discovered</param>
    /// <param name="depth">Discovery depth level</param>
    /// <param name="confidence">Confidence score that this is a recipe URL (0.0-1.0)</param>
    /// <param name="parentUrl">URL where this was discovered from</param>
    /// <param name="metadata">Additional discovery metadata</param>
    public DiscoveredUrl(
        string url,
        string provider,
        DateTime discoveredAt,
        int depth = 0,
        decimal confidence = 0.8m,
        string? parentUrl = null,
        Dictionary<string, object>? metadata = null)
    {
        Url = ValidateUrl(url);
        Provider = ValidateProvider(provider);
        DiscoveredAt = discoveredAt;
        Depth = ValidateDepth(depth);
        Confidence = ValidateConfidence(confidence);
        ParentUrl = string.IsNullOrWhiteSpace(parentUrl) ? null : ValidateUrl(parentUrl);
        Metadata = metadata != null
            ? new Dictionary<string, object>(metadata)
            : new Dictionary<string, object>();
    }

    /// <summary>The discovered URL</summary>
    public string Url { get; init; }

    /// <summary>Source provider name</summary>
    public string Provider { get; init; }

    /// <summary>When the URL was discovered</summary>
    public DateTime DiscoveredAt { get; init; }

    /// <summary>Discovery depth level (0 = seed URL)</summary>
    public int Depth { get; init; }

    /// <summary>Confidence score that this is a recipe URL (0.0-1.0)</summary>
    public decimal Confidence { get; init; }

    /// <summary>URL where this was discovered from (null for seed URLs)</summary>
    public string? ParentUrl { get; init; }

    /// <summary>Additional discovery metadata</summary>
    public IReadOnlyDictionary<string, object> Metadata { get; init; }

    /// <summary>
    ///     Indicates if this URL has high confidence of being a recipe
    /// </summary>
    public bool IsHighConfidence => Confidence >= 0.8m;

    /// <summary>
    ///     Indicates if this URL was directly seeded (not discovered through crawling)
    /// </summary>
    public bool IsSeedUrl => Depth == 0 && ParentUrl == null;

    /// <summary>
    ///     Gets the domain of the discovered URL
    /// </summary>
    public string Domain
    {
        get
        {
            if (Uri.TryCreate(Url, UriKind.Absolute, out var uri))
                return uri.Host;
            return string.Empty;
        }
    }

    /// <summary>
    ///     Gets metadata value by key with type safety
    /// </summary>
    public T? GetMetadata<T>(string key)
    {
        if (Metadata.TryGetValue(key, out var value) && value is T typedValue)
            return typedValue;
        return default;
    }

    /// <summary>
    ///     Creates a copy with updated confidence score
    /// </summary>
    public DiscoveredUrl WithConfidence(decimal newConfidence)
    {
        return this with { Confidence = ValidateConfidence(newConfidence) };
    }

    /// <summary>
    ///     Creates a copy with additional metadata
    /// </summary>
    public DiscoveredUrl WithMetadata(string key, object value)
    {
        var newMetadata = new Dictionary<string, object>(Metadata) { [key] = value };
        return this with { Metadata = newMetadata };
    }

    /// <summary>
    ///     Factory method for creating seed URLs
    /// </summary>
    public static DiscoveredUrl CreateSeed(string url, string provider, decimal confidence = 1.0m)
    {
        return new DiscoveredUrl(url, provider, DateTime.UtcNow, 0, confidence);
    }

    /// <summary>
    ///     Factory method for creating discovered URLs from crawling
    /// </summary>
    public static DiscoveredUrl CreateDiscovered(
        string url,
        string provider,
        string parentUrl,
        int depth,
        decimal confidence = 0.7m,
        Dictionary<string, object>? metadata = null)
    {
        return new DiscoveredUrl(url, provider, DateTime.UtcNow, depth, confidence, parentUrl, metadata);
    }

    #region Validation Methods

    private static string ValidateUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("URL cannot be empty", nameof(url));

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new ArgumentException("URL must be a valid absolute URL", nameof(url));

        if (uri.Scheme != "http" && uri.Scheme != "https")
            throw new ArgumentException("URL must use HTTP or HTTPS protocol", nameof(url));

        return url;
    }

    private static string ValidateProvider(string provider)
    {
        if (string.IsNullOrWhiteSpace(provider))
            throw new ArgumentException("Provider cannot be empty", nameof(provider));

        return provider.Trim();
    }

    private static int ValidateDepth(int depth)
    {
        if (depth < 0)
            throw new ArgumentOutOfRangeException(nameof(depth), "Depth cannot be negative");

        if (depth > 10)
            throw new ArgumentOutOfRangeException(nameof(depth), "Depth cannot exceed 10");

        return depth;
    }

    private static decimal ValidateConfidence(decimal confidence)
    {
        if (confidence < 0.0m || confidence > 1.0m)
            throw new ArgumentOutOfRangeException(nameof(confidence), "Confidence must be between 0.0 and 1.0");

        return confidence;
    }

    #endregion
}