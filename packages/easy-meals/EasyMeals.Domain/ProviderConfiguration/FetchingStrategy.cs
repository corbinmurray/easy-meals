namespace EasyMeals.Domain.ProviderConfiguration;

/// <summary>
/// How content will be fetched from the provider.
/// </summary>
public enum FetchingStrategy
{
    Api,
    StaticHtml,
    DynamicHtml
}
