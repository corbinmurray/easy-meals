namespace EasyMeals.RecipeEngine.Domain.ValueObjects.Fingerprint;

/// <summary>
///     Represents the status of a scraping operation
/// </summary>
public enum FingerprintStatus
{
    /// <summary>
    ///     Content was successfully scraped
    /// </summary>
    Success,

    /// <summary>
    ///     Scraping failed due to an error
    /// </summary>
    Failed,

    /// <summary>
    ///     Content was skipped (e.g., not a recipe page)
    /// </summary>
    Skipped,

    /// <summary>
    ///     Content is being processed
    /// </summary>
    Processing,

    /// <summary>
    ///     Content was blocked by anti-bot measures
    /// </summary>
    Blocked
}