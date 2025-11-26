namespace EasyMeals.Domain.ProviderConfiguration;

/// <summary>
/// Settings for HTML crawl-based recipe discovery.
/// </summary>
public sealed class CrawlSettings
{
    /// <summary>Initial URLs to start crawling from.</summary>
    public IReadOnlyList<string> SeedUrls { get; private set; }

    /// <summary>URL patterns to include (regex). Only matching URLs are processed.</summary>
    public IReadOnlyList<string> IncludePatterns { get; private set; }

    /// <summary>URL patterns to exclude (regex). Matching URLs are skipped.</summary>
    public IReadOnlyList<string> ExcludePatterns { get; private set; }

    /// <summary>Maximum crawl depth from seed URLs.</summary>
    public int MaxDepth { get; private set; }

    /// <summary>CSS selector for finding links to follow.</summary>
    public string LinkSelector { get; private set; }

    /// <summary>
    /// Creates a new instance of CrawlSettings.
    /// </summary>
    /// <param name="seedUrls">Initial URLs to start crawling from.</param>
    /// <param name="includePatterns">URL patterns to include (regex).</param>
    /// <param name="excludePatterns">URL patterns to exclude (regex).</param>
    /// <param name="maxDepth">Maximum crawl depth (default: 3).</param>
    /// <param name="linkSelector">CSS selector for finding links (default: "a[href]").</param>
    public CrawlSettings(
        IReadOnlyList<string> seedUrls,
        IReadOnlyList<string>? includePatterns = null,
        IReadOnlyList<string>? excludePatterns = null,
        int maxDepth = 3,
        string linkSelector = "a[href]")
    {
        SeedUrls = seedUrls ?? throw new ArgumentNullException(nameof(seedUrls));
        IncludePatterns = includePatterns ?? [];
        ExcludePatterns = excludePatterns ?? [];
        MaxDepth = maxDepth >= 0 ? maxDepth : throw new ArgumentOutOfRangeException(nameof(maxDepth), "Cannot be negative.");
        LinkSelector = linkSelector ?? throw new ArgumentNullException(nameof(linkSelector));
    }

    /// <summary>
    /// Validates that seed URLs are provided and well-formed.
    /// </summary>
    public bool HasValidSeedUrls()
    {
        if (SeedUrls.Count == 0)
            return false;

        foreach (var url in SeedUrls)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return false;
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                return false;
        }
        return true;
    }
}
