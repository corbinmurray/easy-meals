using EasyMeals.RecipeEngine.Domain.ValueObjects.Fingerprint;

namespace EasyMeals.RecipeEngine.Domain.Events;

/// <summary>
///     Event raised when a URL is successfully scraped and fingerprinted
/// </summary>
/// <param name="FingerprintId">ID of the fingerprint</param>
/// <param name="Url">URL that was scraped</param>
/// <param name="SourceProvider">Source provider name</param>
/// <param name="Quality">Quality of the scraped content</param>
/// <param name="ContentHash">Hash of the scraped content</param>
public sealed record FingerprintCreatedEvent(
    Guid FingerprintId,
    string Url,
    string SourceProvider,
    ScrapingQuality Quality,
    string ContentHash) : BaseDomainEvent;

/// <summary>
///     Event raised when scraping fails
/// </summary>
/// <param name="FingerprintId">ID of the fingerprint</param>
/// <param name="Url">URL that failed to scrape</param>
/// <param name="SourceProvider">Source provider name</param>
/// <param name="ErrorMessage">Error message describing the failure</param>
public sealed record ScrapingFailedEvent(
    Guid FingerprintId,
    string Url,
    string SourceProvider,
    string ErrorMessage) : BaseDomainEvent;

/// <summary>
///     Event raised when content change is detected
/// </summary>
/// <param name="FingerprintId">ID of the new fingerprint</param>
/// <param name="Url">URL where content changed</param>
/// <param name="OldContentHash">Previous content hash</param>
/// <param name="NewContentHash">New content hash</param>
/// <param name="SourceProvider">Source provider name</param>
public sealed record ContentChangedEvent(
    Guid FingerprintId,
    string Url,
    string OldContentHash,
    string NewContentHash,
    string SourceProvider) : BaseDomainEvent;

/// <summary>
///     Event raised when fingerprint is marked as processed
/// </summary>
/// <param name="FingerprintId">ID of the fingerprint</param>
/// <param name="Url">URL that was processed</param>
/// <param name="RecipeId">ID of the resulting recipe (if successful)</param>
public sealed record FingerprintProcessedEvent(
    Guid FingerprintId,
    string Url,
    Guid? RecipeId) : BaseDomainEvent;

/// <summary>
///     Event raised when fingerprint processing is retried
/// </summary>
/// <param name="FingerprintId">ID of the fingerprint</param>
/// <param name="Url">URL being retried</param>
/// <param name="RetryAttempt">Current retry attempt number</param>
/// <param name="Reason">Reason for retry</param>
public sealed record FingerprintRetryEvent(
    Guid FingerprintId,
    string Url,
    int RetryAttempt,
    string Reason) : BaseDomainEvent;

/// <summary>
///     Event raised when fingerprint quality is updated/assessed
/// </summary>
/// <param name="FingerprintId">ID of the fingerprint</param>
/// <param name="Url">URL of the fingerprint</param>
/// <param name="OldQuality">Previous quality assessment</param>
/// <param name="NewQuality">New quality assessment</param>
/// <param name="Reason">Reason for quality change</param>
public sealed record QualityAssessedEvent(
    Guid FingerprintId,
    string Url,
    ScrapingQuality OldQuality,
    ScrapingQuality NewQuality,
    string Reason) : BaseDomainEvent;