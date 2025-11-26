namespace EasyMeals.Domain.ProviderConfiguration;

/// <summary>
/// How recipe URLs are discovered from a provider.
/// </summary>
public enum DiscoveryStrategy
{
    /// <summary>Discover recipes via the provider's API.</summary>
    Api = 0,

    /// <summary>Discover recipes by crawling HTML pages.</summary>
    Crawl = 1
}
