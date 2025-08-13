using System.Text.RegularExpressions;
using EasyMeals.Crawler.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace EasyMeals.Crawler.Infrastructure.Services;

/// <summary>
///     HTTP service for fetching content from HelloFresh website
///     Handles rate limiting, user agent rotation, and retry logic
/// </summary>
public class HelloFreshHttpService : IHelloFreshHttpService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HelloFreshHttpService> _logger;
    private readonly TimeSpan _minRequestInterval = TimeSpan.FromSeconds(1);
    private readonly SemaphoreSlim _rateLimitSemaphore;

    private readonly string[] _userAgents =
    [
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36",
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:89.0) Gecko/20100101 Firefox/89.0",
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/14.1.1 Safari/605.1.15"
    ];

    private DateTime _lastRequestTime = DateTime.MinValue;

    public HelloFreshHttpService(HttpClient httpClient, ILogger<HelloFreshHttpService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _rateLimitSemaphore = new SemaphoreSlim(1, 1);

        // Configure HttpClient
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
        _httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate");
        _httpClient.DefaultRequestHeaders.Add("DNT", "1");
        _httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
        _httpClient.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
    }

    /// <inheritdoc />
    public async Task<string?> FetchRecipeHtmlAsync(string recipeUrl, CancellationToken cancellationToken = default)
    {
        if (!IsValidHelloFreshUrl(recipeUrl))
        {
            _logger.LogWarning("Invalid HelloFresh URL: {Url}", recipeUrl);
            return null;
        }

        await _rateLimitSemaphore.WaitAsync(cancellationToken);
        
        try
        {
            // Enforce rate limiting
            await EnforceRateLimit(cancellationToken);

            _logger.LogDebug("Fetching recipe HTML from: {Url}", recipeUrl);

            // Set a random user agent
            SetRandomUserAgent();

            using HttpResponseMessage response = await _httpClient.GetAsync(recipeUrl, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("HTTP request failed for {Url}: {StatusCode} {ReasonPhrase}",
                    recipeUrl, response.StatusCode, response.ReasonPhrase);
                return null;
            }

            string content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogWarning("Received empty content from {Url}", recipeUrl);
                return null;
            }

            _logger.LogDebug("Successfully fetched {ContentLength} characters from {Url}",
                content.Length, recipeUrl);

            return content;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request exception for {Url}: {Message}", recipeUrl, ex.Message);
            return null;
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Request cancelled for {Url}", recipeUrl);
            return null;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Request timeout for {Url}", recipeUrl);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching {Url}: {Message}", recipeUrl, ex.Message);
            return null;
        }
        finally
        {
            _rateLimitSemaphore.Release();
        }
    }

    /// <inheritdoc />
    public async Task<List<string>> DiscoverRecipeUrlsAsync(int maxResults = 50, CancellationToken cancellationToken = default)
    {
        var discoveredUrls = new List<string>();

        try
        {
            _logger.LogDebug("Discovering HelloFresh recipe URLs, max results: {MaxResults}", maxResults);

            // Fetch main recipes page
            string? recipesPageContent = await FetchRecipeHtmlAsync("https://www.hellofresh.com/recipes", cancellationToken);
            if (string.IsNullOrEmpty(recipesPageContent))
            {
                _logger.LogWarning("Could not fetch recipes page content");
                return discoveredUrls;
            }

            // Extract recipe URLs from the page
            List<string> urls = ExtractRecipeUrlsFromHtml(recipesPageContent);
            discoveredUrls.AddRange(urls.Take(maxResults));

            _logger.LogDebug("Discovered {Count} recipe URLs", discoveredUrls.Count);
            return discoveredUrls;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering recipe URLs: {Message}", ex.Message);
            return discoveredUrls;
        }
    }

    /// <summary>
    ///     Disposes the HTTP client
    /// </summary>
    public void Dispose()
    {
        _httpClient?.Dispose();
        _rateLimitSemaphore?.Dispose();
    }

    /// <summary>
    ///     Validates if the URL is a valid HelloFresh recipe URL
    /// </summary>
    private static bool IsValidHelloFreshUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;

        try
        {
            var uri = new Uri(url);
            return uri.Host.EndsWith("hellofresh.com", StringComparison.OrdinalIgnoreCase) &&
                   uri.AbsolutePath.StartsWith("/recipes", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    ///     Enforces rate limiting between requests
    /// </summary>
    private async Task EnforceRateLimit(CancellationToken cancellationToken)
    {
        TimeSpan timeSinceLastRequest = DateTime.UtcNow - _lastRequestTime;
        if (timeSinceLastRequest < _minRequestInterval)
        {
            TimeSpan delay = _minRequestInterval - timeSinceLastRequest;
            _logger.LogDebug("Rate limiting: waiting {DelayMs}ms", delay.TotalMilliseconds);
            await Task.Delay(delay, cancellationToken);
        }

        _lastRequestTime = DateTime.UtcNow;
    }

    /// <summary>
    ///     Sets a random user agent to avoid detection
    /// </summary>
    private void SetRandomUserAgent()
    {
        var random = new Random();
        string userAgent = _userAgents[random.Next(_userAgents.Length)];

        // Remove existing user agent
        _httpClient.DefaultRequestHeaders.Remove("User-Agent");

        // Add new user agent
        _httpClient.DefaultRequestHeaders.Add("User-Agent", userAgent);
    }

    /// <summary>
    ///     Extracts recipe URLs from HTML content
    /// </summary>
    private List<string> ExtractRecipeUrlsFromHtml(string htmlContent)
    {
        var urls = new List<string>();

        try
        {
            // Look for HelloFresh recipe URLs in the HTML
            var pattern = @"href=[""']([^""']*hellofresh\.com/recipes/[^""']+)[""']";
            MatchCollection matches = Regex.Matches(htmlContent, pattern, RegexOptions.IgnoreCase);

            foreach (Match match in matches)
            {
                string url = match.Groups[1].Value;

                // Clean up the URL
                url = url.Split('?')[0]; // Remove query parameters
                url = url.Split('#')[0]; // Remove fragments

                // Ensure it's a full URL
                if (!url.StartsWith("http")) url = "https://www.hellofresh.com" + (url.StartsWith("/") ? url : "/" + url);

                // Validate and add unique URLs
                if (IsValidHelloFreshUrl(url) && !urls.Contains(url)) urls.Add(url);
            }

            _logger.LogDebug("Extracted {Count} recipe URLs from HTML", urls.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting recipe URLs from HTML: {Message}", ex.Message);
        }

        return urls;
    }
}