using EasyMeals.RecipeEngine.Application.Interfaces;
using EasyMeals.RecipeEngine.Domain.Interfaces;
using EasyMeals.RecipeEngine.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace EasyMeals.RecipeEngine.Infrastructure.Stealth;

/// <summary>
///     T103, T104: HTTP client implementation with stealth measures for IP ban avoidance.
///     Implements rotating user agents, randomized delays, and respectful headers.
/// </summary>
public class StealthyHttpClient : IStealthyHttpClient
{
	private readonly IHttpClientFactory _httpClientFactory;
	private readonly IRandomizedDelayService _delayService;
	private readonly IUserAgentRotationService _userAgentService;
	private readonly IProviderConfigurationLoader _configLoader;
	private readonly IRateLimiter _rateLimiter;
	private readonly ILogger<StealthyHttpClient> _logger;

	public StealthyHttpClient(
		IHttpClientFactory httpClientFactory,
		IRandomizedDelayService delayService,
		IUserAgentRotationService userAgentService,
		IProviderConfigurationLoader configLoader,
		IRateLimiter rateLimiter,
		ILogger<StealthyHttpClient> logger)
	{
		_httpClientFactory = httpClientFactory;
		_delayService = delayService;
		_userAgentService = userAgentService;
		_configLoader = configLoader;
		_rateLimiter = rateLimiter;
		_logger = logger;
	}

	/// <summary>
	///     T103: Sends a GET request with stealth measures applied.
	/// </summary>
	public async Task<HttpResponseMessage> GetAsync(string url, string providerId, CancellationToken cancellationToken = default)
	{
		// Load provider configuration for rate limiting settings
		ProviderConfiguration? config = await _configLoader.GetByProviderIdAsync(providerId, cancellationToken);
		if (config == null) throw new InvalidOperationException($"Provider configuration not found for {providerId}");

		// T100: Calculate randomized delay before request
		TimeSpan randomizedDelay = _delayService.CalculateDelay(config.MinDelay);

		// T104: Log delay variance for monitoring (debug level only)
		if (_logger.IsEnabled(LogLevel.Debug))
		{
			double variance = (randomizedDelay.TotalSeconds / config.MinDelay.TotalSeconds - 1.0) * 100;
			_logger.LogDebug(
				"Applying randomized delay: {Delay:F2}s (base: {MinDelay:F2}s, variance: {Variance:+0.0;-0.0}%)",
				randomizedDelay.TotalSeconds,
				config.MinDelay.TotalSeconds,
				variance);
		}

		// Apply randomized delay
		await Task.Delay(randomizedDelay, cancellationToken);

		// Wait for rate limit token
		bool acquired = await _rateLimiter.TryAcquireAsync(providerId, cancellationToken);
		if (!acquired)
		{
			_logger.LogWarning("Rate limit reached for provider {ProviderId}, waiting before retry", providerId);
			await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
			acquired = await _rateLimiter.TryAcquireAsync(providerId, cancellationToken);

			if (!acquired) throw new InvalidOperationException($"Rate limit exceeded for provider {providerId}");
		}

		// Create HTTP client with configured policies
		HttpClient client = _httpClientFactory.CreateClient("RecipeEngineHttpClient");

		// T103: Get rotating user agent
		string userAgent = _userAgentService.GetNextUserAgent();

		// T104: Log user agent used (debug level only)
		_logger.LogDebug("Using user agent: {UserAgent}", userAgent);

		// T103: Set user agent header (must be set via request, not client default headers)
		using var request = new HttpRequestMessage(HttpMethod.Get, url);
		request.Headers.Add("User-Agent", userAgent);

		// T103: Additional respectful headers (already set in HttpClient configuration)
		// Accept-Language: en-US,en;q=0.9
		// Accept-Encoding: gzip, deflate, br

		// Send request
		DateTime startTime = DateTime.UtcNow;
		HttpResponseMessage response = await client.SendAsync(request, cancellationToken);
		TimeSpan duration = DateTime.UtcNow - startTime;

		// T104: Log request details for monitoring
		using (_logger.BeginScope(new Dictionary<string, object>
		       {
			       ["Url"] = url,
			       ["ProviderId"] = providerId,
			       ["StatusCode"] = (int)response.StatusCode,
			       ["Duration"] = duration.TotalMilliseconds,
			       ["DelayApplied"] = randomizedDelay.TotalMilliseconds
		       }))
		{
			_logger.LogInformation(
				"HTTP GET {Url} completed with {StatusCode} in {Duration:F2}ms (delay: {Delay:F2}s)",
				url,
				(int)response.StatusCode,
				duration.TotalMilliseconds,
				randomizedDelay.TotalSeconds);
		}

		return response;
	}

	/// <summary>
	///     Sends a GET request and returns the response as a string.
	/// </summary>
	public async Task<string> GetStringAsync(string url, string providerId, CancellationToken cancellationToken = default)
	{
		using HttpResponseMessage response = await GetAsync(url, providerId, cancellationToken);
		response.EnsureSuccessStatusCode();
		return await response.Content.ReadAsStringAsync(cancellationToken);
	}
}