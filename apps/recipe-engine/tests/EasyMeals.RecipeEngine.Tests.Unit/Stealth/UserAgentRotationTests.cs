using EasyMeals.RecipeEngine.Infrastructure.Stealth;
using Microsoft.Extensions.Options;
using Xunit;

namespace EasyMeals.RecipeEngine.Tests.Unit.Stealth;

/// <summary>
/// T094: Unit test for user agent rotation (round-robin or random selection from list)
/// </summary>
public class UserAgentRotationTests
{
	[Fact]
	public void GetNextUserAgent_ReturnsUserAgentFromConfiguredList()
	{
		// Arrange
		var userAgents = new List<string>
		{
			"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36",
			"Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36",
			"Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36"
		};
		
		var options = Options.Create(new UserAgentOptions { UserAgents = userAgents });
		var service = new UserAgentRotationService(options);
		
		// Act
		var userAgent = service.GetNextUserAgent();
		
		// Assert
		Assert.Contains(userAgent, userAgents);
	}
	
	[Fact]
	public void GetNextUserAgent_RotatesThroughAllUserAgents()
	{
		// Arrange
		var userAgents = new List<string>
		{
			"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36",
			"Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36",
			"Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36"
		};
		
		var options = Options.Create(new UserAgentOptions { UserAgents = userAgents });
		var service = new UserAgentRotationService(options);
		var retrievedUserAgents = new HashSet<string>();
		
		// Act - Get user agents multiple times
		for (var i = 0; i < userAgents.Count * 3; i++)
		{
			var userAgent = service.GetNextUserAgent();
			retrievedUserAgents.Add(userAgent);
		}
		
		// Assert - Should have seen all user agents
		Assert.Equal(userAgents.Count, retrievedUserAgents.Count);
		foreach (var ua in userAgents)
		{
			Assert.Contains(ua, retrievedUserAgents);
		}
	}
	
	[Fact]
	public void GetNextUserAgent_WithSingleUserAgent_ReturnsTheSameAgent()
	{
		// Arrange
		var userAgents = new List<string>
		{
			"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36"
		};
		
		var options = Options.Create(new UserAgentOptions { UserAgents = userAgents });
		var service = new UserAgentRotationService(options);
		
		// Act & Assert
		for (var i = 0; i < 10; i++)
		{
			var userAgent = service.GetNextUserAgent();
			Assert.Equal(userAgents[0], userAgent);
		}
	}
	
	[Fact]
	public void GetNextUserAgent_WithEmptyList_ThrowsInvalidOperationException()
	{
		// Arrange
		var options = Options.Create(new UserAgentOptions { UserAgents = new List<string>() });
		var service = new UserAgentRotationService(options);
		
		// Act & Assert
		Assert.Throws<InvalidOperationException>(() => service.GetNextUserAgent());
	}
	
	[Fact]
	public void GetNextUserAgent_ThreadSafe_HandlesMultipleThreads()
	{
		// Arrange
		var userAgents = new List<string>
		{
			"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36",
			"Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36",
			"Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36"
		};
		
		var options = Options.Create(new UserAgentOptions { UserAgents = userAgents });
		var service = new UserAgentRotationService(options);
		var retrievedUserAgents = new System.Collections.Concurrent.ConcurrentBag<string>();
		
		// Act - Get user agents from multiple threads
		Parallel.For(0, 100, i =>
		{
			var userAgent = service.GetNextUserAgent();
			retrievedUserAgents.Add(userAgent);
		});
		
		// Assert - All retrieved user agents should be from the configured list
		foreach (var ua in retrievedUserAgents)
		{
			Assert.Contains(ua, userAgents);
		}
	}
}
