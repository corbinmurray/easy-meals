namespace EasyMeals.RecipeEngine.Domain.Interfaces;

/// <summary>
///     Service for calculating randomized delays to avoid predictable crawling patterns.
/// </summary>
public interface IRandomizedDelayService
{
    /// <summary>
    ///     Calculates a randomized delay with ±20% variance around the minimum delay.
    /// </summary>
    /// <param name="minDelay">The baseline minimum delay</param>
    /// <returns>A randomized delay within ±20% of the minimum delay</returns>
    TimeSpan CalculateDelay(TimeSpan minDelay);
}