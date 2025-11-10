using EasyMeals.RecipeEngine.Infrastructure.Stealth;
using Xunit;

namespace EasyMeals.RecipeEngine.Tests.Unit.Stealth;

/// <summary>
/// T093: Unit test for randomized delay calculation (delay varies ±20% around MinDelay)
/// </summary>
public class RandomizedDelayTests
{
	[Theory]
	[InlineData(1000)] // 1 second
	[InlineData(2000)] // 2 seconds
	[InlineData(5000)] // 5 seconds
	public void CalculateDelay_ReturnsDelayWithin20PercentVariance_ForGivenMinDelay(int minDelayMs)
	{
		// Arrange
		var service = new RandomizedDelayService();
		var minDelay = TimeSpan.FromMilliseconds(minDelayMs);
		var lowerBound = minDelayMs * 0.8; // -20%
		var upperBound = minDelayMs * 1.2; // +20%
		
		// Act & Assert - Test multiple times to verify randomization
		for (var i = 0; i < 100; i++)
		{
			var delay = service.CalculateDelay(minDelay);
			
			Assert.InRange(delay.TotalMilliseconds, lowerBound, upperBound);
		}
	}
	
	[Fact]
	public void CalculateDelay_ProducesVariedDelays_AcrossMultipleInvocations()
	{
		// Arrange
		var service = new RandomizedDelayService();
		var minDelay = TimeSpan.FromSeconds(2);
		var delays = new HashSet<double>();
		
		// Act - Generate many delays
		for (var i = 0; i < 50; i++)
		{
			var delay = service.CalculateDelay(minDelay);
			delays.Add(delay.TotalMilliseconds);
		}
		
		// Assert - Should have multiple different values (not all the same)
		Assert.True(delays.Count > 10, "Delays should vary across invocations");
	}
	
	[Fact]
	public void CalculateDelay_WithZeroMinDelay_ReturnsZero()
	{
		// Arrange
		var service = new RandomizedDelayService();
		var minDelay = TimeSpan.Zero;
		
		// Act
		var delay = service.CalculateDelay(minDelay);
		
		// Assert
		Assert.Equal(TimeSpan.Zero, delay);
	}
	
	[Fact]
	public void CalculateDelay_NeverReturnsNegativeDelay()
	{
		// Arrange
		var service = new RandomizedDelayService();
		var minDelay = TimeSpan.FromMilliseconds(100);
		
		// Act & Assert
		for (var i = 0; i < 100; i++)
		{
			var delay = service.CalculateDelay(minDelay);
			Assert.True(delay >= TimeSpan.Zero, "Delay should never be negative");
		}
	}
}
