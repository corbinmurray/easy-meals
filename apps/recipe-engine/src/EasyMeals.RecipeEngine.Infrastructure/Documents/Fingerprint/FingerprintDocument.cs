using EasyMeals.RecipeEngine.Domain.ValueObjects.Fingerprint;
using EasyMeals.Shared.Data.Attributes;
using EasyMeals.Shared.Data.Documents;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EasyMeals.RecipeEngine.Infrastructure.Documents.Fingerprint;

/// <summary>
///     MongoDB document for tracking web scraping fingerprints and metadata
///     before content is processed into recipes. Enables change detection,
///     deduplication, and comprehensive scraping audit trails.
///     This document represents the boundary between raw scraped content
///     and processed recipe data, following DDD principles.
/// </summary>
[BsonCollection("fingerprints")]
public class FingerprintDocument : BaseDocument
{
    /// <summary>
    ///     The original URL that was scraped
    ///     Indexed for efficient lookups and deduplication
    /// </summary>
    [BsonElement("url")]
    [BsonRequired]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    ///     Hash of the scraped content for change detection
    ///     Uses SHA256 of key content elements (title, ingredients, instructions)
    /// </summary>
    [BsonElement("contentHash")]
    [BsonRequired]
    public string ContentHash { get; set; } = string.Empty;

    /// <summary>
    ///     When this content was scraped
    ///     Supports audit trails and staleness detection
    /// </summary>
    [BsonElement("scrapedAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    [BsonRequired]
    public DateTime ScrapedAt { get; set; }

    /// <summary>
    ///     Source provider that scraped this content
    ///     Enables provider-specific processing strategies and audit trails
    /// </summary>
    [BsonElement("sourceProvider")]
    [BsonRequired]
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>
    ///     Current status of the scraping operation
    ///     Enables workflow tracking and error handling
    /// </summary>
    [BsonElement("status")]
    [BsonRepresentation(BsonType.String)]
    public FingerprintStatus Status { get; set; } = FingerprintStatus.Success;

    /// <summary>
    ///     Quality assessment of the scraped content
    ///     Enables filtering and prioritization of processing
    /// </summary>
    [BsonElement("quality")]
    [BsonRepresentation(BsonType.String)]
    public ScrapingQuality Quality { get; set; } = ScrapingQuality.Unknown;

    /// <summary>
    ///     Error message if scraping failed
    ///     Supports debugging and monitoring of scraping issues
    /// </summary>
    [BsonElement("errorMessage")]
    [BsonIgnoreIfNull]
    public string? ErrorMessage { get; set; }

    /// <summary>
    ///     Additional metadata captured during scraping
    ///     Flexible storage for provider-specific data and debugging information
    /// </summary>
    [BsonElement("metadata")]
    [BsonIgnoreIfNull]
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    ///     Reference to the processed Recipe document (if successfully processed)
    ///     Links fingerprint to final recipe for audit trails
    /// </summary>
    [BsonElement("recipeId")]
    [BsonIgnoreIfNull]
    public string? RecipeId { get; set; }

    /// <summary>
    ///     When this fingerprint was processed into a recipe
    ///     Supports processing workflow tracking
    /// </summary>
    [BsonElement("processedAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    [BsonIgnoreIfNull]
    public DateTime? ProcessedAt { get; set; }

    /// <summary>
    ///     Number of times this URL has been scraped
    ///     Supports retry logic and staleness detection
    /// </summary>
    [BsonElement("scrapeCount")]
    public int ScrapeCount { get; set; } = 1;

    /// <summary>
    ///     HTTP status code from the scraping request
    ///     Supports debugging and quality assessment
    /// </summary>
    [BsonElement("httpStatusCode")]
    [BsonIgnoreIfNull]
    public int? HttpStatusCode { get; set; }

    /// <summary>
    ///     Response time for the scraping request in milliseconds
    ///     Supports performance monitoring and provider assessment
    /// </summary>
    [BsonElement("responseTimeMs")]
    [BsonIgnoreIfNull]
    public long? ResponseTimeMs { get; set; }

    /// <summary>
    ///     Size of the scraped content in bytes
    ///     Supports quality assessment and resource monitoring
    /// </summary>
    [BsonElement("contentSizeBytes")]
    [BsonIgnoreIfNull]
    public long? ContentSizeBytes { get; set; }

    /// <summary>
    ///     User agent string used for scraping
    ///     Supports anti-detection tracking and debugging
    /// </summary>
    [BsonElement("userAgent")]
    [BsonIgnoreIfNull]
    public string? UserAgent { get; set; }

    /// <summary>
    ///     IP address used for scraping (if using proxy rotation)
    ///     Supports anti-detection tracking and debugging
    /// </summary>
    [BsonElement("ipAddress")]
    [BsonIgnoreIfNull]
    public string? IpAddress { get; set; }

    /// <summary>
    ///     Indicates if this fingerprint represents successfully scraped content
    ///     ready for recipe processing
    /// </summary>
    [BsonIgnore]
    public bool IsReadyForProcessing =>
        Status == FingerprintStatus.Success &&
        Quality >= ScrapingQuality.Acceptable &&
        !string.IsNullOrEmpty(ContentHash);

    /// <summary>
    ///     Indicates if this is a failed scraping attempt
    /// </summary>
    [BsonIgnore]
    public bool HasFailed => Status == FingerprintStatus.Failed;

    /// <summary>
    ///     Indicates if this fingerprint has been processed into a recipe
    /// </summary>
    [BsonIgnore]
    public bool IsProcessed => !string.IsNullOrEmpty(RecipeId) && ProcessedAt.HasValue;

    /// <summary>
    ///     Gets the age of this fingerprint in hours
    ///     Useful for staleness detection and cleanup policies
    /// </summary>
    [BsonIgnore]
    public double AgeInHours => (DateTime.UtcNow - ScrapedAt).TotalHours;

    /// <summary>
    ///     Marks this fingerprint as processed with a recipe reference
    /// </summary>
    public void MarkAsProcessed(string recipeId)
    {
        RecipeId = recipeId;
        ProcessedAt = DateTime.UtcNow;
        MarkAsModified();
    }

    /// <summary>
    ///     Increments the scrape count for retry tracking
    /// </summary>
    public void IncrementScrapeCount()
    {
        ScrapeCount++;
        MarkAsModified();
    }

    /// <summary>
    ///     Updates the status and marks as modified
    /// </summary>
    public void UpdateStatus(FingerprintStatus newStatus, string? errorMessage = null)
    {
        Status = newStatus;
        ErrorMessage = errorMessage;
        MarkAsModified();
    }
}