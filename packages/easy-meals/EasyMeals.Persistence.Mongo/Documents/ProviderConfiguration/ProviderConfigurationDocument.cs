using EasyMeals.Persistence.Mongo.Attributes;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EasyMeals.Persistence.Mongo.Documents.ProviderConfiguration;

/// <summary>
/// MongoDB document for provider configuration data.
/// Extends BaseSoftDeletableDocument to support soft deletion and audit trails.
/// </summary>
[BsonCollection("provider_configurations")]
public class ProviderConfigurationDocument : BaseSoftDeletableDocument
{
    /// <summary>
    /// Unique identifier for the provider (e.g., "hellofresh", "allrecipes").
    /// Used as a business key; immutable after creation.
    /// </summary>
    [BsonElement("providerName")]
    [BsonRequired]
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>
    /// Human-friendly display name.
    /// </summary>
    [BsonElement("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Base URL for the provider's website.
    /// </summary>
    [BsonElement("baseUrl")]
    [BsonRequired]
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Whether this provider is currently active for recipe discovery.
    /// </summary>
    [BsonElement("isEnabled")]
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Processing priority (higher = processed first).
    /// </summary>
    [BsonElement("priority")]
    public int Priority { get; set; }

    /// <summary>
    /// How recipe URLs are discovered.
    /// </summary>
    [BsonElement("discoveryStrategy")]
    [BsonRepresentation(BsonType.String)]
    public string DiscoveryStrategy { get; set; } = string.Empty;

    /// <summary>
    /// How recipe content is fetched.
    /// </summary>
    [BsonElement("fetchingStrategy")]
    [BsonRepresentation(BsonType.String)]
    public string FetchingStrategy { get; set; } = string.Empty;

    /// <summary>
    /// CSS selectors for extracting recipe properties.
    /// </summary>
    [BsonElement("extractionSelectors")]
    public ExtractionSelectorsDocument ExtractionSelectors { get; set; } = new();

    /// <summary>
    /// Rate limiting configuration.
    /// </summary>
    [BsonElement("rateLimitSettings")]
    public RateLimitSettingsDocument RateLimitSettings { get; set; } = new();

    /// <summary>
    /// API-specific settings (null if not using API strategy).
    /// </summary>
    [BsonElement("apiSettings")]
    [BsonIgnoreIfNull]
    public ApiSettingsDocument? ApiSettings { get; set; }

    /// <summary>
    /// Crawl-specific settings (null if not using Crawl strategy).
    /// </summary>
    [BsonElement("crawlSettings")]
    [BsonIgnoreIfNull]
    public CrawlSettingsDocument? CrawlSettings { get; set; }
}
