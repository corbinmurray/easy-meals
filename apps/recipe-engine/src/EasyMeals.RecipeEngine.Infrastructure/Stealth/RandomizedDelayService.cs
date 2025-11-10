using EasyMeals.RecipeEngine.Domain.Interfaces;

namespace EasyMeals.RecipeEngine.Infrastructure.Stealth;

/// <summary>
/// T097: Service for calculating randomized delays to avoid predictable crawling patterns.
/// Implements ±20% variance around the minimum delay to appear more human-like.
/// </summary>
public class RandomizedDelayService : IRandomizedDelayService
{
	private readonly Random _random = new();
	
	/// <summary>
	/// Calculates a randomized delay with ±20% variance around the minimum delay.
	/// Formula: MinDelay * (0.8 + Random(0, 0.4)) = MinDelay * [0.8, 1.2]
	/// </summary>
	/// <param name="minDelay">The baseline minimum delay</param>
	/// <returns>A randomized delay within ±20% of the minimum delay</returns>
	public TimeSpan CalculateDelay(TimeSpan minDelay)
	{
		if (minDelay == TimeSpan.Zero)
		{
			return TimeSpan.Zero;
		}
		
		// Generate a random factor between 0.8 and 1.2 (±20% variance)
		// 0.8 + (random value between 0.0 and 0.4)
		var randomFactor = 0.8 + (_random.NextDouble() * 0.4);
		
		var delayMs = minDelay.TotalMilliseconds * randomFactor;
		
		return TimeSpan.FromMilliseconds(Math.Max(0, delayMs));
	}
}
