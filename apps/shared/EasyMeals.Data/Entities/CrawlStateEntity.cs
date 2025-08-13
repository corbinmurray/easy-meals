using System.ComponentModel.DataAnnotations;

namespace EasyMeals.Data.Entities;

/// <summary>
/// EF Core entity for Crawl State persistence
/// Stores the current state of crawling operations for resumability
/// </summary>
public class CrawlStateEntity
{
    [Key]
    public string Id { get; set; } = "default";
    
    /// <summary>
    /// JSON serialized list of pending URLs to crawl
    /// </summary>
    public string PendingUrlsJson { get; set; } = "[]";
    
    /// <summary>
    /// JSON serialized set of completed recipe IDs
    /// </summary>
    public string CompletedRecipeIdsJson { get; set; } = "[]";
    
    /// <summary>
    /// JSON serialized set of failed URLs
    /// </summary>
    public string FailedUrlsJson { get; set; } = "[]";
    
    public DateTime LastCrawlTime { get; set; } = DateTime.MinValue;
    public int TotalProcessed { get; set; } = 0;
    public int TotalSuccessful { get; set; } = 0;
    public int TotalFailed { get; set; } = 0;
    
    /// <summary>
    /// Provider source (e.g., "HelloFresh")
    /// </summary>
    [MaxLength(100)]
    public string SourceProvider { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
