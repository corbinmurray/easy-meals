namespace EasyMeals.Domain.ProviderConfiguration;

public sealed class ProviderConfiguration
{
    public string ProviderName { get; init; }
    public string DisplayName { get; init; }
    public string BaseUrl { get; init; }
    public bool IsEnabled { get; private set; } = true;
    public DiscoveryStrategy DiscoveryStrategy { get; init; }
    public FetchingStrategy FetchingStrategy { get; init; }
    public ExtractionSelectors Selectors { get; init; }

    public ProviderConfiguration(
        string providerName,
        string displayName,
        string baseUrl,
        DiscoveryStrategy discoveryStrategy,
        FetchingStrategy fetchingStrategy,
        ExtractionSelectors selectors)
    {
        ProviderName = providerName ?? throw new ArgumentNullException(nameof(providerName));
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        BaseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
        DiscoveryStrategy = discoveryStrategy;
        FetchingStrategy = fetchingStrategy;
        Selectors = selectors ?? throw new ArgumentNullException(nameof(selectors));
    }

    public void Disable() => IsEnabled = false;
    public void Enable() => IsEnabled = true;
}
