using DomainProviderConfiguration = EasyMeals.Domain.ProviderConfiguration.ProviderConfiguration;
using DomainDiscoveryStrategy = EasyMeals.Domain.ProviderConfiguration.DiscoveryStrategy;
using DomainFetchingStrategy = EasyMeals.Domain.ProviderConfiguration.FetchingStrategy;
using DomainAuthMethod = EasyMeals.Domain.ProviderConfiguration.AuthMethod;
using DomainExtractionSelectors = EasyMeals.Domain.ProviderConfiguration.ExtractionSelectors;
using DomainRateLimitSettings = EasyMeals.Domain.ProviderConfiguration.RateLimitSettings;
using DomainApiSettings = EasyMeals.Domain.ProviderConfiguration.ApiSettings;
using DomainCrawlSettings = EasyMeals.Domain.ProviderConfiguration.CrawlSettings;

namespace EasyMeals.Persistence.Mongo.Documents.ProviderConfiguration;

/// <summary>
/// Maps between domain entities and MongoDB documents for provider configurations.
/// </summary>
public static class ProviderConfigurationMapper
{
    /// <summary>
    /// Converts a MongoDB document to a domain entity.
    /// </summary>
    public static DomainProviderConfiguration ToDomain(ProviderConfigurationDocument doc)
    {
        ArgumentNullException.ThrowIfNull(doc);

        return DomainProviderConfiguration.Reconstitute(
            id: doc.Id,
            providerName: doc.ProviderName,
            displayName: doc.DisplayName,
            baseUrl: doc.BaseUrl,
            isEnabled: doc.IsEnabled,
            priority: doc.Priority,
            discoveryStrategy: ParseDiscoveryStrategy(doc.DiscoveryStrategy),
            fetchingStrategy: ParseFetchingStrategy(doc.FetchingStrategy),
            extractionSelectors: MapExtractionSelectorsToDomain(doc.ExtractionSelectors),
            rateLimitSettings: MapRateLimitSettingsToDomain(doc.RateLimitSettings),
            apiSettings: doc.ApiSettings is not null ? MapApiSettingsToDomain(doc.ApiSettings) : null,
            crawlSettings: doc.CrawlSettings is not null ? MapCrawlSettingsToDomain(doc.CrawlSettings) : null,
            concurrencyToken: doc.ConcurrencyToken,
            createdAt: doc.CreatedAt,
            updatedAt: doc.UpdatedAt,
            isDeleted: doc.IsDeleted,
            deletedAt: doc.DeletedAt
        );
    }

    /// <summary>
    /// Converts a domain entity to a MongoDB document.
    /// </summary>
    public static ProviderConfigurationDocument ToDocument(DomainProviderConfiguration entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new ProviderConfigurationDocument
        {
            Id = entity.Id,
            ProviderName = entity.ProviderName,
            DisplayName = entity.DisplayName,
            BaseUrl = entity.BaseUrl,
            IsEnabled = entity.IsEnabled,
            Priority = entity.Priority,
            DiscoveryStrategy = entity.DiscoveryStrategy.ToString(),
            FetchingStrategy = entity.FetchingStrategy.ToString(),
            ExtractionSelectors = MapExtractionSelectorsToDocument(entity.ExtractionSelectors),
            RateLimitSettings = MapRateLimitSettingsToDocument(entity.RateLimitSettings),
            ApiSettings = entity.ApiSettings is not null ? MapApiSettingsToDocument(entity.ApiSettings) : null,
            CrawlSettings = entity.CrawlSettings is not null ? MapCrawlSettingsToDocument(entity.CrawlSettings) : null,
            ConcurrencyToken = entity.ConcurrencyToken,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            IsDeleted = entity.IsDeleted,
            DeletedAt = entity.DeletedAt
        };
    }

    private static DomainDiscoveryStrategy ParseDiscoveryStrategy(string value)
    {
        return Enum.TryParse<DomainDiscoveryStrategy>(value, ignoreCase: true, out var result)
            ? result
            : DomainDiscoveryStrategy.Crawl;
    }

    private static DomainFetchingStrategy ParseFetchingStrategy(string value)
    {
        return Enum.TryParse<DomainFetchingStrategy>(value, ignoreCase: true, out var result)
            ? result
            : DomainFetchingStrategy.StaticHtml;
    }

    private static DomainAuthMethod ParseAuthMethod(string value)
    {
        return Enum.TryParse<DomainAuthMethod>(value, ignoreCase: true, out var result)
            ? result
            : DomainAuthMethod.None;
    }

    private static DomainExtractionSelectors MapExtractionSelectorsToDomain(ExtractionSelectorsDocument doc)
    {
        return new DomainExtractionSelectors(
            titleSelector: doc.TitleSelector,
            descriptionSelector: doc.DescriptionSelector,
            ingredientsSelector: doc.IngredientsSelector,
            instructionsSelector: doc.InstructionsSelector,
            titleFallbackSelector: doc.TitleFallbackSelector,
            descriptionFallbackSelector: doc.DescriptionFallbackSelector,
            prepTimeSelector: doc.PrepTimeSelector,
            cookTimeSelector: doc.CookTimeSelector,
            totalTimeSelector: doc.TotalTimeSelector,
            servingsSelector: doc.ServingsSelector,
            imageUrlSelector: doc.ImageUrlSelector,
            authorSelector: doc.AuthorSelector,
            cuisineSelector: doc.CuisineSelector,
            difficultySelector: doc.DifficultySelector,
            nutritionSelector: doc.NutritionSelector
        );
    }

    private static ExtractionSelectorsDocument MapExtractionSelectorsToDocument(DomainExtractionSelectors entity)
    {
        return new ExtractionSelectorsDocument
        {
            TitleSelector = entity.TitleSelector,
            TitleFallbackSelector = entity.TitleFallbackSelector,
            DescriptionSelector = entity.DescriptionSelector,
            DescriptionFallbackSelector = entity.DescriptionFallbackSelector,
            IngredientsSelector = entity.IngredientsSelector,
            InstructionsSelector = entity.InstructionsSelector,
            PrepTimeSelector = entity.PrepTimeSelector,
            CookTimeSelector = entity.CookTimeSelector,
            TotalTimeSelector = entity.TotalTimeSelector,
            ServingsSelector = entity.ServingsSelector,
            ImageUrlSelector = entity.ImageUrlSelector,
            AuthorSelector = entity.AuthorSelector,
            CuisineSelector = entity.CuisineSelector,
            DifficultySelector = entity.DifficultySelector,
            NutritionSelector = entity.NutritionSelector
        };
    }

    private static DomainRateLimitSettings MapRateLimitSettingsToDomain(RateLimitSettingsDocument doc)
    {
        return new DomainRateLimitSettings(
            requestsPerMinute: doc.RequestsPerMinute,
            delayBetweenRequests: TimeSpan.FromMilliseconds(doc.DelayBetweenRequestsMs),
            maxConcurrentRequests: doc.MaxConcurrentRequests,
            maxRetries: doc.MaxRetries,
            retryDelay: TimeSpan.FromMilliseconds(doc.RetryDelayMs)
        );
    }

    private static RateLimitSettingsDocument MapRateLimitSettingsToDocument(DomainRateLimitSettings entity)
    {
        return new RateLimitSettingsDocument
        {
            RequestsPerMinute = entity.RequestsPerMinute,
            DelayBetweenRequestsMs = (int)entity.DelayBetweenRequests.TotalMilliseconds,
            MaxConcurrentRequests = entity.MaxConcurrentRequests,
            MaxRetries = entity.MaxRetries,
            RetryDelayMs = (int)entity.RetryDelay.TotalMilliseconds
        };
    }

    private static DomainApiSettings MapApiSettingsToDomain(ApiSettingsDocument doc)
    {
        return new DomainApiSettings(
            endpoint: doc.Endpoint,
            authMethod: ParseAuthMethod(doc.AuthMethod),
            headers: new Dictionary<string, string>(doc.Headers),
            pageSizeParam: doc.PageSizeParam,
            pageNumberParam: doc.PageNumberParam,
            defaultPageSize: doc.DefaultPageSize
        );
    }

    private static ApiSettingsDocument MapApiSettingsToDocument(DomainApiSettings entity)
    {
        return new ApiSettingsDocument
        {
            Endpoint = entity.Endpoint,
            AuthMethod = entity.AuthMethod.ToString(),
            Headers = new Dictionary<string, string>(entity.Headers),
            PageSizeParam = entity.PageSizeParam,
            PageNumberParam = entity.PageNumberParam,
            DefaultPageSize = entity.DefaultPageSize
        };
    }

    private static DomainCrawlSettings MapCrawlSettingsToDomain(CrawlSettingsDocument doc)
    {
        return new DomainCrawlSettings(
            seedUrls: doc.SeedUrls.AsReadOnly(),
            includePatterns: doc.IncludePatterns.AsReadOnly(),
            excludePatterns: doc.ExcludePatterns.AsReadOnly(),
            maxDepth: doc.MaxDepth,
            linkSelector: doc.LinkSelector
        );
    }

    private static CrawlSettingsDocument MapCrawlSettingsToDocument(DomainCrawlSettings entity)
    {
        return new CrawlSettingsDocument
        {
            SeedUrls = entity.SeedUrls.ToList(),
            IncludePatterns = entity.IncludePatterns.ToList(),
            ExcludePatterns = entity.ExcludePatterns.ToList(),
            MaxDepth = entity.MaxDepth,
            LinkSelector = entity.LinkSelector
        };
    }
}
