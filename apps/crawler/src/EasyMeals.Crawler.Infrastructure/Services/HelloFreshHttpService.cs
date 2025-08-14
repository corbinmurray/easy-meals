using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using EasyMeals.Crawler.Domain.Interfaces;
using HtmlAgilityPack;
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
    private const string HelloFreshBaseUrl = "https://www.hellofresh.com";
    private const string HelloFreshRecipeDiscoveryUrl = HelloFreshBaseUrl + "/recipes";

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
        if (!IsValidHelloFreshUrl(recipeUrl) && !IsValidHelloFreshCategoryUrl(recipeUrl))
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
                MediaTypeHeaderValue? contentType = response.Content.Headers.ContentType;
                Encoding encoding = Encoding.UTF8; // Default to UTF-8

                if (contentType?.CharSet is not null)
                    try
                    {
                        encoding = Encoding.GetEncoding(contentType.CharSet);
                    }
                    catch (ArgumentException)
                    {
                        _logger.LogWarning("Unknown charset '{CharSet}', using UTF-8", contentType.CharSet);
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
        var discoveredUrls = new HashSet<string>(); // Use HashSet to avoid duplicates

        try
        {
            _logger.LogDebug("Discovering HelloFresh recipe URLs using multi-level strategy, max results: {MaxResults}", maxResults);

            // Strategy 1: Start with the main recipes page
            await DiscoverFromPage(HelloFreshRecipeDiscoveryUrl, discoveredUrls, cancellationToken);

            // Strategy 2: Discover and crawl category pages
            List<string> categoryUrls = await DiscoverCategoryUrlsAsync(cancellationToken);

            _logger.LogDebug("Found {CategoryCount} category pages to explore", categoryUrls.Count);

            // Crawl each category page for more recipes
            foreach (string categoryUrl in categoryUrls)
            {
                if (discoveredUrls.Count >= maxResults)
                    break;

                await DiscoverFromPage(categoryUrl, discoveredUrls, cancellationToken);

                // Add small delay between category pages to be respectful
                await Task.Delay(500, cancellationToken);
            }

            _logger.LogDebug("Discovered {Count} total recipe URLs from {CategoryCount} category pages",
                discoveredUrls.Count, categoryUrls.Count + 1);

            return discoveredUrls.Take(maxResults).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering recipe URLs: {Message}", ex.Message);
            return discoveredUrls.Take(maxResults).ToList();
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
    ///     Discovers category URLs from the main recipes page
    /// </summary>
    private async Task<List<string>> DiscoverCategoryUrlsAsync(CancellationToken cancellationToken = default)
    {
        var categoryUrls = new List<string>();

        try
        {
            _logger.LogDebug("Discovering category URLs from main recipes page");

            string? mainPageContent = await FetchRecipeHtmlAsync("https://www.hellofresh.com/recipes", cancellationToken);
            if (string.IsNullOrEmpty(mainPageContent))
            {
                _logger.LogWarning("Could not fetch main recipes page for category discovery");
                return categoryUrls;
            }

            var doc = new HtmlDocument();
            doc.LoadHtml(mainPageContent);

            // Look for category links - these are typically in navigation menus or filter sections
            var selectors = new[]
            {
                "//a[contains(@href, '/recipes/') and not(contains(@href, '/recipes/recipes'))]/@href", // Category links
                "//nav//a[contains(@href, '/recipes/')]/@href", // Navigation links
                "//div[contains(@class, 'filter') or contains(@class, 'category')]//a[contains(@href, '/recipes/')]/@href", // Filter/category sections
                "//a[contains(@href, 'recipes') and (contains(text(), 'recipes') or contains(@aria-label, 'recipes'))]/@href" // Fallback
            };

            foreach (string selector in selectors)
            {
                HtmlNodeCollection nodes = doc.DocumentNode.SelectNodes(selector);
                if (nodes is null) continue;

                foreach (HtmlNode node in nodes)
                {
                    string? href = node.GetAttributeValue("href", null);
                    if (string.IsNullOrEmpty(href)) continue;

                    string cleanUrl = CleanUrl(href);

                    // Only add if it's a valid category URL and not already discovered
                    if (IsValidHelloFreshCategoryUrl(cleanUrl) &&
                        !categoryUrls.Contains(cleanUrl) &&
                        cleanUrl != "https://www.hellofresh.com/recipes") // Skip main page
                        categoryUrls.Add(cleanUrl);
                }
            }

            // Add some common category URLs that might not be in navigation
            var commonCategories = new[]
            {
                "https://www.hellofresh.com/recipes/quick-easy",
                "https://www.hellofresh.com/recipes/vegetarian",
                "https://www.hellofresh.com/recipes/american-recipes",
                "https://www.hellofresh.com/recipes/italian-recipes",
                "https://www.hellofresh.com/recipes/mexican-recipes",
                "https://www.hellofresh.com/recipes/asian-recipes",
                "https://www.hellofresh.com/recipes/mediterranean-recipes",
                "https://www.hellofresh.com/recipes/comfort-food",
                "https://www.hellofresh.com/recipes/healthy-recipes",
                "https://www.hellofresh.com/recipes/family-friendly"
            };

            foreach (string categoryUrl in commonCategories)
            {
                if (!categoryUrls.Contains(categoryUrl)) categoryUrls.Add(categoryUrl);
            }

            _logger.LogDebug("Discovered {Count} category URLs", categoryUrls.Count);
            return categoryUrls;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering category URLs: {Message}", ex.Message);
            return categoryUrls;
        }
    }

    /// <summary>
    ///     Discovers recipe URLs from a specific page (could be main page or category page)
    /// </summary>
    private async Task DiscoverFromPage(string pageUrl, HashSet<string> discoveredUrls, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Discovering recipes from page: {PageUrl}", pageUrl);

            string? pageContent = await FetchRecipeHtmlAsync(pageUrl, cancellationToken);
            
            if (string.IsNullOrEmpty(pageContent))
            {
                _logger.LogWarning("Could not fetch content from page: {PageUrl}", pageUrl);
                return;
            }

            List<string> urls = ExtractRecipeUrlsFromHtml(pageContent);
            
            int newUrls = urls.Count(discoveredUrls.Add);

            _logger.LogDebug("Found {NewUrls} new recipe URLs from {PageUrl} (total: {TotalUrls})",
                newUrls, pageUrl, discoveredUrls.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error discovering recipes from page {PageUrl}: {Message}", pageUrl, ex.Message);
        }
    }

    /// <summary>
    ///     Validates if the URL is a valid HelloFresh recipe URL (individual recipe, not category)
    /// </summary>
    private static bool IsValidHelloFreshUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) 
            return false;

        try
        {
            var uri = new Uri(url);
            
            if (!uri.Host.EndsWith("hellofresh.com", StringComparison.OrdinalIgnoreCase) ||
                !uri.AbsolutePath.StartsWith("/recipes", StringComparison.OrdinalIgnoreCase))
                return false;

            // Check if this is an individual recipe URL (has more path segments after /recipes/)
            // Individual recipes typically look like: /recipes/recipe-name-with-id
            // Category pages look like: /recipes/american-recipes, /recipes/quick-easy, etc.
            string[] pathSegments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

            // Individual recipes should have at least 2 segments: ["recipes", "recipe-name"]
            // and the recipe name usually contains numbers/IDs or is longer
            if (pathSegments.Length < 2 || pathSegments[0] != "recipes") 
                return false;
            
            string recipePath = pathSegments[1];
            string potentialId = recipePath.Split("-")[^1];
            
            // Check if individual recipe by looking for a hexadecimal ID
            // HelloFresh uses hexadecimal IDs (like MongoDB ObjectIds) that are typically 24 characters
            // but could be other lengths. Check if it's a valid hex string with reasonable length.
            return IsValidHexadecimalId(potentialId);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    ///     Validates if a string is a valid hexadecimal identifier (like MongoDB ObjectId or similar)
    /// </summary>
    /// <param name="id">The ID string to validate</param>
    /// <returns>True if the string is a valid hexadecimal ID with reasonable length</returns>
    private static bool IsValidHexadecimalId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return false;
        
        // Check length - most hex IDs are between 16-32 characters
        // MongoDB ObjectIds are 24 characters, UUIDs without hyphens are 32
        if (id.Length < 16 || id.Length > 32)
            return false;
        
        // Check if all characters are valid hexadecimal
        return id.All(c => char.IsAsciiHexDigit(c));
    }

    /// <summary>
    ///     Validates if the URL is a valid HelloFresh category/listing page
    /// </summary>
    private static bool IsValidHelloFreshCategoryUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;

        try
        {
            var uri = new Uri(url);
            
            if (!uri.Host.EndsWith("hellofresh.com", StringComparison.OrdinalIgnoreCase) ||
                !uri.AbsolutePath.StartsWith("/recipes", StringComparison.OrdinalIgnoreCase))
                return false;

            // Category pages include:
            // /recipes (main page)
            // /recipes/american-recipes
            // /recipes/quick-easy
            // /recipes/vegetarian
            // But exclude individual recipe URLs
            return uri.AbsolutePath == "/recipes" ||
                   (uri.AbsolutePath.StartsWith("/recipes/") && !IsValidHelloFreshUrl(url));
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
        var urls = new HashSet<string>();

        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(htmlContent);

            // Look for recipe links using multiple comprehensive selectors
            var selectors = new[]
            {
                // Direct recipe links in href attributes
                "//a[contains(@href, '/recipes/') and not(contains(@href, '/recipes/recipes'))]/@href",

                // Recipe cards or containers
                "//div[contains(@class, 'recipe')]//a[contains(@href, '/recipes/')]/@href",
                "//article[contains(@class, 'recipe')]//a[contains(@href, '/recipes/')]/@href",

                // Image links that might be recipe thumbnails
                "//img[contains(@src, 'recipe') or contains(@alt, 'recipe')]/parent::a[contains(@href, '/recipes/')]/@href",

                // Links with recipe-related text or attributes
                "//a[contains(@href, '/recipes/') and (contains(@title, 'recipe') or contains(@aria-label, 'recipe'))]/@href",

                // JSON-LD structured data URLs
                "//script[@type='application/ld+json']",

                // Canonical links
                "//link[@rel='canonical' and contains(@href, '/recipes/')]/@href"
            };

            foreach (string selector in selectors)
            {
                if (selector.Contains("script[@type='application/ld+json']"))
                {
                    // Special handling for JSON-LD structured data
                    ExtractUrlsFromJsonLd(doc, urls);
                    continue;
                }

                HtmlNodeCollection nodes = doc.DocumentNode.SelectNodes(selector);
                if (nodes is null) continue;

                foreach (HtmlNode node in nodes)
                {
                    string? href = node.GetAttributeValue("href", null);
                    if (string.IsNullOrEmpty(href)) continue;

                    // Clean up the URL
                    string cleanUrl = CleanUrl(href);

                    // Validate and add unique URLs (only individual recipes, not categories)
                    if (IsValidHelloFreshUrl(cleanUrl)) 
                        urls.Add(cleanUrl);
                }
            }

            // Fallback to regex if XPath selection doesn't work well
            if (urls.Count == 0)
            {
                _logger.LogDebug("XPath selection found no URLs, falling back to regex");
                var patterns = new[]
                {
                    @"href=[""']([^""']*(?:hellofresh\.com)?/recipes/[^""'/]+[^""']*)[""']",
                    @"""url"":\s*[""']([^""']*hellofresh\.com/recipes/[^""']+)[""']",
                    @"window\.location\s*=\s*[""']([^""']*recipes/[^""']+)[""']"
                };

                foreach (string pattern in patterns)
                {
                    MatchCollection matches = Regex.Matches(htmlContent, pattern, RegexOptions.IgnoreCase);

                    foreach (Match match in matches)
                    {
                        string url = match.Groups[1].Value;
                        string cleanUrl = CleanUrl(url);

                        if (IsValidHelloFreshUrl(cleanUrl) && !urls.Contains(cleanUrl)) urls.Add(cleanUrl);
                    }
                }
            }

            _logger.LogDebug("Extracted {Count} recipe URLs from HTML", urls.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting recipe URLs from HTML: {Message}", ex.Message);
        }

        return urls.ToList();
    }

    /// <summary>
    ///     Extracts URLs from JSON-LD structured data in the HTML
    /// </summary>
    private void ExtractUrlsFromJsonLd(HtmlDocument doc, HashSet<string> urls)
    {
        try
        {
            HtmlNodeCollection jsonNodes = doc.DocumentNode.SelectNodes("//script[@type='application/ld+json']");
            if (jsonNodes is null) return;

            foreach (HtmlNode jsonNode in jsonNodes)
            {
                string jsonContent = jsonNode.InnerText;
                if (string.IsNullOrWhiteSpace(jsonContent)) continue;

                // Look for URL patterns in JSON-LD that might be recipe URLs
                const string urlPattern = @"""url"":\s*""([^""]*recipes/[^""]+)""";
                MatchCollection matches = Regex.Matches(jsonContent, urlPattern, RegexOptions.IgnoreCase);

                foreach (Match match in matches)
                {
                    string url = match.Groups[1].Value;
                    string cleanUrl = CleanUrl(url);

                    if (!IsValidHelloFreshUrl(cleanUrl)) continue;
                    
                    _logger.LogDebug("Found recipe URL in JSON-LD: {Url}", cleanUrl);
                    
                    urls.Add(cleanUrl);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error extracting URLs from JSON-LD: {Message}", ex.Message);
        }
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
        if (!url.StartsWith("http")) url = "https://www.hellofresh.com" + (url.StartsWith("/") ? url : "/" + url);

        return url;
    }
}