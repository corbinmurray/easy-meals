namespace EasyMeals.RecipeEngine.Domain.Interfaces;

/// <summary>
/// HTTP client that implements stealth measures to avoid IP bans.
/// Includes rotating user agents, randomized delays, and respectful headers.
/// </summary>
public interface IStealthyHttpClient
{
	/// <summary>
	/// Sends a GET request with stealth measures applied.
	/// Includes rotating user agent, randomized delay, and respectful headers.
	/// </summary>
	/// <param name="url">The URL to request</param>
	/// <param name="providerId">Provider identifier for rate limiting</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>HTTP response message</returns>
	Task<HttpResponseMessage> GetAsync(string url, string providerId, CancellationToken cancellationToken = default);

	/// <summary>
	/// Sends a GET request and returns the response as a string.
	/// </summary>
	/// <param name="url">The URL to request</param>
	/// <param name="providerId">Provider identifier for rate limiting</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>Response content as string</returns>
	Task<string> GetStringAsync(string url, string providerId, CancellationToken cancellationToken = default);
}