using System.ComponentModel.DataAnnotations;

namespace EasyMeals.Shared.Data.Entities;

/// <summary>
/// EF Core entity for Crawl State persistence
/// Stores the current state of crawling operations for resumability
/// Supports distributed crawling scenarios and fault tolerance
/// </summary>
public class CrawlStateEntity : BaseEntity
{
    /// <summary>
    /// JSON serialized list of pending URLs to crawl
    /// Supports resumable crawling operations across service restarts
    /// </summary>
    public string PendingUrlsJson { get; set; } = "[]";

    /// <summary>
    /// JSON serialized set of completed recipe IDs
    /// Prevents duplicate processing and supports idempotent operations
    /// </summary>
    public string CompletedRecipeIdsJson { get; set; } = "[]";

    /// <summary>
    /// JSON serialized set of failed URLs
    /// Supports error tracking and retry mechanisms
    /// </summary>
    public string FailedUrlsJson { get; set; } = "[]";

    /// <summary>
    /// Last time crawling was performed
    /// Supports scheduling and rate limiting business requirements
    /// </summary>
    public DateTime LastCrawlTime { get; set; } = DateTime.MinValue;

    /// <summary>
    /// Total number of URLs processed
    /// Business metric for monitoring and reporting
    /// </summary>
    public int TotalProcessed { get; set; } = 0;

    /// <summary>
    /// Total number of successful crawls
    /// Success rate calculation for monitoring
    /// </summary>
    public int TotalSuccessful { get; set; } = 0;

    /// <summary>
    /// Total number of failed crawls
    /// Error rate tracking for operational monitoring
    /// </summary>
    public int TotalFailed { get; set; } = 0;

    /// <summary>
    /// Provider source (e.g., "HelloFresh")
    /// Supports multi-provider crawling architecture
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string SourceProvider { get; set; } = string.Empty;

    /// <summary>
    /// Calculated success rate for monitoring
    /// Business metric exposed as computed property
    /// </summary>
    public double SuccessRate => TotalProcessed > 0 ? (double)TotalSuccessful / TotalProcessed : 0.0;

    /// <summary>
    /// Indicates if crawling is currently in progress
    /// Supports distributed locking and prevents concurrent crawls
    /// </summary>
    public bool IsActive { get; set; } = false;
}
