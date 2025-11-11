namespace EasyMeals.RecipeEngine.Domain.ValueObjects.Fingerprint;

/// <summary>
///     Represents the quality assessment of scraped content
/// </summary>
public enum ScrapingQuality
{
    /// <summary>
    ///     Quality assessment unknown
    /// </summary>
    Unknown = 0,

    /// <summary>
    ///     Poor quality - missing key elements
    /// </summary>
    Poor = 1,

    /// <summary>
    ///     Acceptable quality - has basic recipe data
    /// </summary>
    Acceptable = 2,

    /// <summary>
    ///     Good quality - has most recipe elements
    /// </summary>
    Good = 3,

    /// <summary>
    ///     Excellent quality - complete recipe with rich metadata
    /// </summary>
    Excellent = 4
}