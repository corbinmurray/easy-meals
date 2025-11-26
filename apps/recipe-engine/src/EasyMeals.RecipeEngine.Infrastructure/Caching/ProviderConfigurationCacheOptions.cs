namespace EasyMeals.RecipeEngine.Infrastructure.Caching;

/// <summary>
/// Configuration options for provider configuration caching.
/// </summary>
public class ProviderConfigurationCacheOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "ProviderConfigurationCache";

    /// <summary>
    /// Time-to-live for cached provider configurations.
    /// Default: 30 seconds.
    /// </summary>
    public TimeSpan CacheTtl { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Whether caching is enabled.
    /// Default: true.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
