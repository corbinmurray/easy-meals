using EasyMeals.Shared.Data.Attributes;
using MongoDB.Bson.Serialization.Attributes;

namespace EasyMeals.Shared.Data.Documents;

/// <summary>
///     MongoDB document for Crawl State persistence with embedded configuration
///     Stores the current state of crawling operations for resumability
///     Supports distributed crawling scenarios and fault tolerance
/// </summary>
[BsonCollection("crawlstates")]
public class CrawlStateDocument : BaseDocument
{
    /// <summary>
    ///     List of pending URLs to crawl
    ///     Supports resumable crawling operations across service restarts
    /// </summary>
    [BsonElement("pendingUrls")]
    public List<string> PendingUrls { get; set; } = new();

    /// <summary>
    ///     Set of completed recipe IDs
    ///     Prevents duplicate processing and supports idempotent operations
    /// </summary>
    [BsonElement("completedRecipeIds")]
    public HashSet<string> CompletedRecipeIds { get; set; } = new();

    /// <summary>
    ///     List of failed URLs with error information
    ///     Supports error tracking and retry mechanisms
    /// </summary>
    [BsonElement("failedUrls")]
    public List<FailedUrlDocument> FailedUrls { get; set; } = new();

    /// <summary>
    ///     Last time crawling was performed
    ///     Supports scheduling and rate limiting business requirements
    /// </summary>
    [BsonElement("lastCrawlTime")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime LastCrawlTime { get; set; } = DateTime.MinValue;

    /// <summary>
    ///     Total number of URLs processed
    ///     Business metric for monitoring and reporting
    /// </summary>
    [BsonElement("totalProcessed")]
    public int TotalProcessed { get; set; } = 0;

    /// <summary>
    ///     Total number of successful crawls
    ///     Success rate calculation for monitoring
    /// </summary>
    [BsonElement("totalSuccessful")]
    public int TotalSuccessful { get; set; } = 0;

    /// <summary>
    ///     Total number of failed crawls
    ///     Error rate tracking for operational monitoring
    /// </summary>
    [BsonElement("totalFailed")]
    public int TotalFailed { get; set; } = 0;

    /// <summary>
    ///     Provider source (e.g., "HelloFresh")
    ///     Supports multi-provider crawling architecture
    /// </summary>
    [BsonElement("sourceProvider")]
    [BsonRequired]
    public string SourceProvider { get; set; } = string.Empty;

    /// <summary>
    ///     Indicates if crawling is currently in progress
    ///     Supports distributed locking and prevents concurrent crawls
    /// </summary>
    [BsonElement("isActive")]
    public bool IsActive { get; set; } = false;

    /// <summary>
    ///     Embedded crawl configuration
    ///     Provider-specific settings and parameters
    /// </summary>
    [BsonElement("configuration")]
    [BsonIgnoreIfNull]
    public CrawlConfigurationDocument? Configuration { get; set; }

    /// <summary>
    ///     Current crawl session identifier
    ///     Supports tracking and monitoring of individual crawl sessions
    /// </summary>
    [BsonElement("currentSessionId")]
    [BsonIgnoreIfNull]
    public string? CurrentSessionId { get; set; }

    /// <summary>
    ///     Priority level for this crawl state (1-10, 10 being highest)
    ///     Supports prioritized crawling based on business requirements
    /// </summary>
    [BsonElement("priority")]
    public int Priority { get; set; } = 5;

    /// <summary>
    ///     Next scheduled crawl time
    ///     Supports automated scheduling and rate limiting
    /// </summary>
    [BsonElement("nextScheduledCrawl")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    [BsonIgnoreIfNull]
    public DateTime? NextScheduledCrawl { get; set; }

    /// <summary>
    ///     Calculated success rate for monitoring
    ///     Business metric exposed as computed property
    /// </summary>
    [BsonIgnore]
    public double SuccessRate => TotalProcessed > 0 ? (double)TotalSuccessful / TotalProcessed : 0.0;

    /// <summary>
    ///     Gets the number of pending URLs
    ///     Useful for monitoring and capacity planning
    /// </summary>
    [BsonIgnore]
    public int PendingUrlCount => PendingUrls.Count;

    /// <summary>
    ///     Gets the number of failed URLs
    ///     Error monitoring metric
    /// </summary>
    [BsonIgnore]
    public int FailedUrlCount => FailedUrls.Count;

    /// <summary>
    ///     Indicates if the crawl state has any work remaining
    ///     Business logic for scheduling and completion detection
    /// </summary>
    [BsonIgnore]
    public bool HasPendingWork => PendingUrls.Count > 0 && IsActive;
}

/// <summary>
///     Embedded document representing a failed URL with error information
///     Supports detailed error tracking and retry logic
/// </summary>
public class FailedUrlDocument
{
    /// <summary>
    ///     The URL that failed to be processed
    /// </summary>
    [BsonElement("url")]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    ///     Error message or reason for failure
    /// </summary>
    [BsonElement("errorMessage")]
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    ///     Timestamp when the failure occurred
    /// </summary>
    [BsonElement("failedAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime FailedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     Number of retry attempts made
    /// </summary>
    [BsonElement("retryCount")]
    public int RetryCount { get; set; } = 0;

    /// <summary>
    ///     HTTP status code if applicable
    /// </summary>
    [BsonElement("httpStatusCode")]
    [BsonIgnoreIfNull]
    public int? HttpStatusCode { get; set; }

    /// <summary>
    ///     Additional error details or stack trace
    /// </summary>
    [BsonElement("errorDetails")]
    [BsonIgnoreIfNull]
    public string? ErrorDetails { get; set; }
}