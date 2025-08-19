using System.ComponentModel.DataAnnotations;

namespace EasyMeals.Crawler.Domain.Configurations;

/// <summary>
/// Configuration options for the crawler application
/// Supports multiple source providers to enable extensibility beyond HelloFresh
/// </summary>
public class CrawlerOptions
{
    public const string SectionName = "Crawler";

    /// <summary>
    /// The source provider name for crawled recipes (e.g., "HelloFresh", "BlueApron", "EveryPlate")
    /// This is used to identify the origin of recipes in the database
    /// </summary>
    [Required]
    public string SourceProvider { get; set; } = "HelloFresh";

    /// <summary>
    /// The base priority for crawl state management
    /// Higher numbers indicate higher priority for processing
    /// </summary>
    public int DefaultPriority { get; set; } = 1;

    /// <summary>
    /// Delay between requests in seconds to be respectful to the source provider
    /// </summary>
    public int DelayBetweenRequestsSeconds { get; set; } = 2;

    /// <summary>
    /// Request timeout in seconds
    /// </summary>
    public int RequestTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum number of retries for failed requests
    /// </summary>
    public int MaxRetries { get; set; } = 3;
}
