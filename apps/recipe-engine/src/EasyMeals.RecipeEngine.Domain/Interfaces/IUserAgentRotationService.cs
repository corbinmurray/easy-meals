namespace EasyMeals.RecipeEngine.Domain.Interfaces;

/// <summary>
/// Service for rotating user agent strings to avoid detection.
/// </summary>
public interface IUserAgentRotationService
{
	/// <summary>
	/// Gets the next user agent string in rotation.
	/// </summary>
	/// <returns>A user agent string from the configured list</returns>
	string GetNextUserAgent();
}
