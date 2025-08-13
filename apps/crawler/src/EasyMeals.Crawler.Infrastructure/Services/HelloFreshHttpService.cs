using System.Text.RegularExpressions;
using EasyMeals.Crawler.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using HtmlAgilityPack;

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

        // Configure HttpClient for proper content handling
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        
        // Clear any existing headers to avoid conflicts
        _httpClient.DefaultRequestHeaders.Clear();
        
        // Set standard browser headers - but let HttpClient handle compression automatically
        _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
        // Don't set Accept-Encoding manually - let HttpClientHandler manage this
        _httpClient.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
        _httpClient.DefaultRequestHeaders.Add("Pragma", "no-cache");
        _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "document");
        _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "navigate");
        _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Site", "none");
        _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-User", "?1");
        _httpClient.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
        _httpClient.DefaultRequestHeaders.Add("DNT", "1");
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

            // Log response headers for debugging
            _logger.LogDebug("Response status: {StatusCode}, Content-Type: {ContentType}, Content-Encoding: {ContentEncoding}",
                response.StatusCode,
                response.Content.Headers.ContentType?.ToString() ?? "unknown",
                response.Content.Headers.ContentEncoding?.FirstOrDefault() ?? "none");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("HTTP request failed for {Url}: {StatusCode} {ReasonPhrase}",
                    recipeUrl, response.StatusCode, response.ReasonPhrase);
                return null;
            }

            // Read content using multiple approaches to handle encoding issues
            string content;
            
            try
            {
                // First try the standard approach - HttpClient should handle compression automatically
                content = await response.Content.ReadAsStringAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read content as string directly, trying byte array approach");
                
                // Fallback: read as byte array and manually decode
                byte[] contentBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                
                // Try to detect encoding from Content-Type header
                var contentType = response.Content.Headers.ContentType;
                var encoding = System.Text.Encoding.UTF8; // Default to UTF-8
                
                if (contentType?.CharSet is not null)
                {
                    try
                    {
                        encoding = System.Text.Encoding.GetEncoding(contentType.CharSet);
                    }
                    catch (ArgumentException)
                    {
                        _logger.LogWarning("Unknown charset '{CharSet}', using UTF-8", contentType.CharSet);
                    }
                }
                
                content = encoding.GetString(contentBytes);
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogWarning("Received empty content from {Url}", recipeUrl);
                return null;
            }

            // Log the first few characters for debugging
            string contentPreview = content.Length > 200 ? content[..200] : content;
            _logger.LogDebug("Content preview (first 200 chars): {ContentPreview}", contentPreview);

            // Validate that we received HTML content
            string trimmedContent = content.TrimStart();
            if (!trimmedContent.StartsWith("<", StringComparison.OrdinalIgnoreCase) && 
                !content.Contains("<html", StringComparison.OrdinalIgnoreCase) &&
                !content.Contains("<!DOCTYPE", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Content from {Url} does not appear to be HTML. First 100 chars: {Content}", 
                    recipeUrl, content.Length > 100 ? content[..100] : content);
                
                // Log additional debugging info
                _logger.LogDebug("Content length: {Length}, Content type: {ContentType}", 
                    content.Length, response.Content.Headers.ContentType);
                
                return null;
            }

            // Validate using HtmlAgilityPack to ensure it's parseable HTML
            try
            {
                var doc = new HtmlDocument();
                doc.LoadHtml(content);
                
                // Check if the document has basic HTML structure
                if (doc.DocumentNode.SelectSingleNode("//html") is null && 
                    doc.DocumentNode.SelectSingleNode("//head") is null &&
                    doc.DocumentNode.SelectSingleNode("//body") is null)
                {
                    _logger.LogWarning("HTML content from {Url} lacks basic HTML structure", recipeUrl);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse HTML content from {Url}", recipeUrl);
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
    ///     Extracts recipe URLs from HTML content using HtmlAgilityPack
    /// </summary>
    private List<string> ExtractRecipeUrlsFromHtml(string htmlContent)
    {
        var urls = new List<string>();

        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(htmlContent);

            // Look for recipe links using multiple selectors
            var selectors = new[]
            {
                "//a[contains(@href, '/recipes/')]/@href",
                "//a[contains(@href, 'hellofresh.com/recipes')]/@href",
                "//link[@rel='canonical' and contains(@href, '/recipes/')]/@href"
            };

            foreach (string selector in selectors)
            {
                HtmlNodeCollection nodes = doc.DocumentNode.SelectNodes(selector);
                if (nodes is null) continue;

                foreach (HtmlNode node in nodes)
                {
                    string? href = node.GetAttributeValue("href", null);
                    if (string.IsNullOrEmpty(href)) continue;

                    // Clean up the URL
                    string cleanUrl = CleanUrl(href);

                    // Validate and add unique URLs
                    if (IsValidHelloFreshUrl(cleanUrl) && !urls.Contains(cleanUrl))
                    {
                        urls.Add(cleanUrl);
                    }
                }
            }

            // Fallback to regex if XPath selection doesn't work well
            if (urls.Count == 0)
            {
                _logger.LogDebug("XPath selection found no URLs, falling back to regex");
                var pattern = @"href=[""']([^""']*(?:hellofresh\.com)?/recipes/[^""']+)[""']";
                MatchCollection matches = Regex.Matches(htmlContent, pattern, RegexOptions.IgnoreCase);

                foreach (Match match in matches)
                {
                    string url = match.Groups[1].Value;
                    string cleanUrl = CleanUrl(url);

                    if (IsValidHelloFreshUrl(cleanUrl) && !urls.Contains(cleanUrl))
                    {
                        urls.Add(cleanUrl);
                    }
                }
            }

            _logger.LogDebug("Extracted {Count} recipe URLs from HTML", urls.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting recipe URLs from HTML: {Message}", ex.Message);
        }

        return urls;
    }

    /// <summary>
    ///     Cleans and normalizes a URL
    /// </summary>
    private static string CleanUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return string.Empty;

        // Remove query parameters and fragments
        url = url.Split('?')[0].Split('#')[0];

        // Ensure it's a full URL
        if (!url.StartsWith("http"))
        {
            url = "https://www.hellofresh.com" + (url.StartsWith("/") ? url : "/" + url);
        }

        return url;
    }
}