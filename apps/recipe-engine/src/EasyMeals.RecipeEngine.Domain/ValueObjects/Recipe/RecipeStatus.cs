namespace EasyMeals.RecipeEngine.Domain.ValueObjects.Recipe;

/// <summary>
///     Represents the lifecycle status of a recipe in the content pipeline
/// </summary>
public enum RecipeStatus
{
    /// <summary>
    ///     Recipe is being extracted/built (in-progress)
    /// </summary>
    Draft = 0,

    /// <summary>
    ///     Recipe is complete and available in the shared collection
    /// </summary>
    Published = 1,

    /// <summary>
    ///     Recipe has been removed from active use
    ///     (e.g., source URL died, duplicate found, quality issues)
    /// </summary>
    Archived = 2
}