namespace EasyMeals.Domain.ProviderConfiguration;

/// <summary>
/// Settings for HTML crawl-based recipe discovery.
/// </summary>
public sealed record CrawlSettings
{
    /// <summary>Initial URLs to start crawling from.</summary>
    public required IReadOnlyList<string> SeedUrls { get; init; }

    /// <summary>URL patterns to include (regex). Only matching URLs are processed.</summary>
    public IReadOnlyList<string> IncludePatterns { get; init; } = [];

    /// <summary>URL patterns to exclude (regex). Matching URLs are skipped.</summary>
    public IReadOnlyList<string> ExcludePatterns { get; init; } = [];

    /// <summary>Maximum crawl depth from seed URLs.</summary>
    public int MaxDepth { get; init; } = 3;

    /// <summary>CSS selector for finding links to follow.</summary>
    public string LinkSelector { get; init; } = "a[href]";

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
