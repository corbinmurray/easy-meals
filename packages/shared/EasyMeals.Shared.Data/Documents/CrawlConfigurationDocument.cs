using MongoDB.Bson.Serialization.Attributes;

namespace EasyMeals.Shared.Data.Documents;

/// <summary>
/// Embedded document representing crawl configuration settings
/// Supports provider-specific crawling parameters and settings
/// </summary>
public class CrawlConfigurationDocument
{
    /// <summary>
    /// Maximum number of URLs to process per crawl session
    /// Rate limiting configuration
    /// </summary>
    [BsonElement("maxUrlsPerSession")]
    public int MaxUrlsPerSession { get; set; } = 100;

    /// <summary>
    /// Delay between requests in milliseconds
    /// Respectful crawling and rate limiting
    /// </summary>
    [BsonElement("delayBetweenRequestsMs")]
    public int DelayBetweenRequestsMs { get; set; } = 1000;

    /// <summary>
    /// Request timeout in seconds
    /// Network configuration
    /// </summary>
    [BsonElement("requestTimeoutSeconds")]
    public int RequestTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum retry attempts for failed requests
    /// Fault tolerance configuration
    /// </summary>
    [BsonElement("maxRetryAttempts")]
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// User agent string for web requests
    /// Crawler identification
    /// </summary>
    [BsonElement("userAgent")]
    public string UserAgent { get; set; } = "EasyMeals-Crawler/1.0";

    /// <summary>
    /// Base URLs for the crawling session
    /// Starting points for discovery
    /// </summary>
    [BsonElement("baseUrls")]
    public List<string> BaseUrls { get; set; } = new();

    /// <summary>
    /// URL patterns to include in crawling
    /// Regex patterns for URL filtering
    /// </summary>
    [BsonElement("includePatterns")]
    public List<string> IncludePatterns { get; set; } = new();

    /// <summary>
    /// URL patterns to exclude from crawling
    /// Regex patterns for URL filtering
    /// </summary>
    [BsonElement("excludePatterns")]
    public List<string> ExcludePatterns { get; set; } = new();

    /// <summary>
    /// Custom headers to include in requests
    /// Authentication and provider-specific requirements
    /// </summary>
    [BsonElement("customHeaders")]
    [BsonIgnoreIfNull]
    public Dictionary<string, string>? CustomHeaders { get; set; }

    /// <summary>
    /// Provider-specific configuration options
    /// Flexible configuration for different crawling strategies
    /// </summary>
    [BsonElement("providerOptions")]
    [BsonIgnoreIfNull]
    public Dictionary<string, object>? ProviderOptions { get; set; }
}
