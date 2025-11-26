namespace EasyMeals.Domain.ProviderConfiguration;

/// <summary>
/// How recipe content is fetched from a provider.
/// </summary>
public enum FetchingStrategy
{
    /// <summary>Fetch structured data from the provider's API.</summary>
    Api = 0,

    /// <summary>Fetch HTML via simple HTTP GET (no JavaScript rendering).</summary>
    StaticHtml = 1,

    /// <summary>Fetch HTML using browser automation (JavaScript rendering required).</summary>
    DynamicHtml = 2
}
