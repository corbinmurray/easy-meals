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
        _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
        _httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");

        // Don't set Accept-Encoding manually - let HttpClientHandler manage this
        _httpClient.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
        _httpClient.DefaultRequestHeaders.Add("Pragma", "no-cache");
        _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "document");
        _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "navigate");
        _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Site", "none");
        _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-User", "?1");
        _httpClient.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
        _httpClient.DefaultRequestHeaders.Add("sec-ch-ua", "\"Chromium\";v=\"130\", \"Google Chrome\";v=\"130\", \"Not?A_Brand\";v=\"99\"");
        _httpClient.DefaultRequestHeaders.Add("sec-ch-ua-mobile", "?0");
        _httpClient.DefaultRequestHeaders.Add("sec-ch-ua-platform", "\"Windows\"");
        _httpClient.DefaultRequestHeaders.Add("DNT", "1");
    }

    /// <inheritdoc />
    public async Task<string?> FetchRecipeHtmlAsync(string recipeUrl, CancellationToken cancellationToken = default)
    {
        if (!IsValidHelloFreshRecipeUrl(recipeUrl) && !IsValidHelloFreshCategoryUrl(recipeUrl))
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
        var discoveredRecipes = new HashSet<string>();

        try
        {
            _logger.LogDebug("Discovering HelloFresh recipe URLs using breadth-first search with recipe priority, max results: {MaxResults}", maxResults);

            // Use breadth-first search with recipe priority
            await PerformBreadthFirstSearchAsync(HelloFreshRecipeDiscoveryUrl, discoveredRecipes, maxResults, cancellationToken);

            _logger.LogDebug("Discovered {Count} total recipe URLs using breadth-first search", discoveredRecipes.Count);

            return discoveredRecipes.Take(maxResults).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering recipe URLs: {Message}", ex.Message);
            return discoveredRecipes.Take(maxResults).ToList();
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
    ///     Performs breadth-first search with recipe priority to discover recipe URLs efficiently
    /// </summary>
    /// <param name="startUrl">The starting URL (root or category page)</param>
    /// <param name="discoveredRecipes">Collection to store discovered recipe URLs</param>
    /// <param name="maxResults">Maximum number of recipes to discover</param>
    /// <param name="cancellationToken">Cancellation token for graceful shutdown</param>
    private async Task PerformBreadthFirstSearchAsync(
        string startUrl,
        HashSet<string> discoveredRecipes,
        int maxResults,
        CancellationToken cancellationToken)
    {
        var pagesToVisit = new Queue<(string Url, int Depth)>();
        var visitedPages = new HashSet<string>();
        const int maxDepth = 3; // Limit category depth to prevent infinite exploration

        pagesToVisit.Enqueue((startUrl, 0));

        _logger.LogInformation("BFS: Starting breadth-first search from {StartUrl} with max depth {MaxDepth}",
            startUrl, maxDepth);

        while (pagesToVisit.Count > 0 && discoveredRecipes.Count < maxResults && !cancellationToken.IsCancellationRequested)
        {
            (string currentPageUrl, int currentDepth) = pagesToVisit.Dequeue();

            // Skip if we've already visited this page or exceeded max depth
            if (!visitedPages.Add(currentPageUrl) || currentDepth > maxDepth)
                continue;

            _logger.LogDebug("BFS: Exploring page {PageUrl} at depth {Depth} (recipes found: {RecipeCount}/{MaxResults})",
                currentPageUrl, currentDepth, discoveredRecipes.Count, maxResults);

            try
            {
                // Fetch page content
                string? pageContent = await FetchRecipeHtmlAsync(currentPageUrl, cancellationToken);
                if (string.IsNullOrEmpty(pageContent))
                {
                    _logger.LogWarning("BFS: Could not fetch content from {PageUrl}", currentPageUrl);
                    continue;
                }

                // Extract URLs from current page - prioritize recipes over categories
                List<string> extractedUrls = ExtractAllUrlsFromHtml(pageContent, maxResults - discoveredRecipes.Count);

                // PHASE 1: Collect ALL recipes from this page first (Recipe Priority!)
                var newRecipes = new List<string>();
                var newCategoryPages = new List<string>();

                foreach (string url in extractedUrls)
                {
                    if (IsValidHelloFreshRecipeUrl(url) && discoveredRecipes.Add(url))
                    {
                        newRecipes.Add(url);
                        _logger.LogDebug("BFS: Found new recipe: {RecipeUrl}", url);

                        // Early termination if we've reached our target
                        if (discoveredRecipes.Count >= maxResults)
                        {
                            _logger.LogInformation("BFS: Reached target of {MaxResults} recipes at depth {Depth}",
                                maxResults, currentDepth);
                            return;
                        }
                    }
                    else if (IsValidHelloFreshCategoryUrl(url) && !visitedPages.Contains(url) && currentDepth < maxDepth)
                    {
                        newCategoryPages.Add(url);
                    }
                }

                _logger.LogInformation("BFS: From {PageUrl} (depth {Depth}) found {RecipeCount} recipes, {CategoryCount} new category pages",
                    currentPageUrl, currentDepth, newRecipes.Count, newCategoryPages.Count);

                // PHASE 2: Only add category pages to queue if we still need more recipes
                if (discoveredRecipes.Count < maxResults && currentDepth < maxDepth)
                {
                    foreach (string categoryUrl in newCategoryPages)
                    {
                        pagesToVisit.Enqueue((categoryUrl, currentDepth + 1));
                    }

                    _logger.LogDebug("BFS: Added {CategoryCount} category pages to queue for depth {NextDepth}",
                        newCategoryPages.Count, currentDepth + 1);
                }
                else if (discoveredRecipes.Count >= maxResults)
                {
                    _logger.LogInformation("BFS: Target reached, skipping category exploration");
                    break;
                }
                else if (currentDepth >= maxDepth)
                {
                    _logger.LogDebug("BFS: Max depth reached, skipping deeper category exploration");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "BFS: Error processing page {PageUrl}: {Message}", currentPageUrl, ex.Message);
                // Continue with next page rather than failing completely
            }
        }

        // Log final results
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("BFS: Search cancelled gracefully. Found {RecipeCount} recipes before cancellation",
                discoveredRecipes.Count);
        }
        else if (discoveredRecipes.Count >= maxResults)
        {
            _logger.LogInformation("BFS: Successfully reached target of {MaxResults} recipes", maxResults);
        }
        else
        {
            _logger.LogInformation("BFS: Exhausted all pages up to depth {MaxDepth}. Found {RecipeCount} recipes total",
                maxDepth, discoveredRecipes.Count);
        }
    }

    /// <summary>
    ///     Validates if the URL is a valid HelloFresh recipe URL (individual recipe, not category)
    /// </summary>
    private static bool IsValidHelloFreshRecipeUrl(string url)
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
                   (uri.AbsolutePath.StartsWith("/recipes/") && !IsValidHelloFreshRecipeUrl(url));
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
    ///     Extracts all recipe and category URLs from HTML content using HtmlAgilityPack
    /// </summary>
    /// <param name="htmlContent">The HTML content to parse</param>
    /// <param name="maxUrls">Maximum number of URLs to extract</param>
    /// <returns>List of discovered URLs (both recipes and categories)</returns>
    private List<string> ExtractAllUrlsFromHtml(string htmlContent, int maxUrls = 50)
    {
        var urls = new HashSet<string>();

        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(htmlContent);

            // Debug: Log some info about the page structure
            HtmlNodeCollection? allLinks = doc.DocumentNode.SelectNodes("//a[@href]");
            HtmlNodeCollection? recipeLinks = doc.DocumentNode.SelectNodes("//a[contains(@href, '/recipes/')]");

            _logger.LogDebug("URL Extraction Debug: Found {AllLinks} total links, {RecipeLinks} recipe-related links",
                allLinks?.Count ?? 0, recipeLinks?.Count ?? 0);

            // Check if this looks like a JavaScript-heavy page
            bool hasReactOrNext = htmlContent.Contains("__NEXT_DATA__") || htmlContent.Contains("React") ||
                                  htmlContent.Contains("_app") || htmlContent.Contains("chunk");

            if (hasReactOrNext)
            {
                _logger.LogWarning("Detected JavaScript framework (React/Next.js) - content may be dynamically loaded");

                // Try to extract URLs from __NEXT_DATA__ if present
                ExtractUrlsFromNextData(htmlContent, urls);
            }

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
                if (urls.Count >= maxUrls)
                    break;

                if (selector.Contains("script[@type='application/ld+json']"))
                {
                    // Special handling for JSON-LD structured data
                    ExtractUrlsFromJsonLd(doc, urls);
                    continue;
                }

                HtmlNodeCollection nodes = doc.DocumentNode.SelectNodes(selector);
                if (nodes is null) continue;

                _logger.LogDebug("Selector '{Selector}' found {Count} nodes", selector, nodes.Count);

                foreach (HtmlNode node in nodes)
                {
                    if (urls.Count >= maxUrls)
                        break;

                    string? href = node.GetAttributeValue("href", null);
                    if (string.IsNullOrEmpty(href)) continue;

                    // Clean up the URL
                    string cleanUrl = CleanUrl(href);

                    // Add any valid HelloFresh URL (both recipes and categories)
                    if (IsValidHelloFreshRecipeUrl(cleanUrl) || IsValidHelloFreshCategoryUrl(cleanUrl))
                    {
                        urls.Add(cleanUrl);
                        _logger.LogDebug("Added URL: {Url} (Recipe: {IsRecipe})",
                            cleanUrl, IsValidHelloFreshRecipeUrl(cleanUrl));
                    }
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
                    if (urls.Count >= maxUrls)
                        break;

                    MatchCollection matches = Regex.Matches(htmlContent, pattern, RegexOptions.IgnoreCase);
                    _logger.LogDebug("Regex pattern '{Pattern}' found {Count} matches", pattern, matches.Count);

                    foreach (Match match in matches)
                    {
                        if (urls.Count >= maxUrls)
                            break;

                        string url = match.Groups[1].Value;
                        string cleanUrl = CleanUrl(url);

                        if (IsValidHelloFreshRecipeUrl(cleanUrl) || IsValidHelloFreshCategoryUrl(cleanUrl))
                        {
                            urls.Add(cleanUrl);
                            _logger.LogDebug("Added URL from regex: {Url}", cleanUrl);
                        }
                    }
                }
            }

            _logger.LogDebug("Extracted {Count} URLs from HTML (Recipes: {RecipeCount}, Categories: {CategoryCount})",
                urls.Count,
                urls.Count(u => IsValidHelloFreshRecipeUrl(u)),
                urls.Count(u => IsValidHelloFreshCategoryUrl(u)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting URLs from HTML: {Message}", ex.Message);
        }

        return urls.ToList();
    }

    /// <summary>
    ///     Extracts URLs from __NEXT_DATA__ JSON embedded in Next.js pages
    /// </summary>
    private void ExtractUrlsFromNextData(string htmlContent, HashSet<string> urls)
    {
        try
        {
            // Look for __NEXT_DATA__ script tag
            const string nextDataPattern = @"<script\s+id=""__NEXT_DATA__""[^>]*>(.*?)</script>";
            Match nextDataMatch = Regex.Match(htmlContent, nextDataPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (!nextDataMatch.Success)
            {
                _logger.LogDebug("No __NEXT_DATA__ found in page");
                return;
            }

            string jsonContent = nextDataMatch.Groups[1].Value;
            if (string.IsNullOrWhiteSpace(jsonContent))
            {
                _logger.LogDebug("Empty __NEXT_DATA__ content");
                return;
            }

            // Look for recipe URLs in the JSON data
            var urlPatterns = new[]
            {
                @"""href"":\s*""([^""]*recipes/[^""]+)""",  // href properties
                @"""url"":\s*""([^""]*recipes/[^""]+)""",   // url properties  
                @"""slug"":\s*""([^""]*recipes/[^""]+)""",  // slug properties
                @"""path"":\s*""([^""]*recipes/[^""]+)""",  // path properties
                @"""link"":\s*""([^""]*recipes/[^""]+)""",  // link properties
                @"""to"":\s*""([^""]*recipes/[^""]+)""",    // navigation to properties
                @"""pathname"":\s*""([^""]*recipes/[^""]+)""" // pathname properties
            };

            foreach (string pattern in urlPatterns)
            {
                MatchCollection matches = Regex.Matches(jsonContent, pattern, RegexOptions.IgnoreCase);

                foreach (Match match in matches)
                {
                    string url = match.Groups[1].Value;
                    string cleanUrl = CleanUrl(url);

                    if (IsValidHelloFreshRecipeUrl(cleanUrl) || IsValidHelloFreshCategoryUrl(cleanUrl))
                    {
                        urls.Add(cleanUrl);
                        _logger.LogDebug("Found URL in __NEXT_DATA__: {Url}", cleanUrl);
                    }
                }
            }

            _logger.LogDebug("Extracted {Count} URLs from __NEXT_DATA__", urls.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error extracting URLs from __NEXT_DATA__: {Message}", ex.Message);
        }
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

                    if (!IsValidHelloFreshRecipeUrl(cleanUrl)) continue;

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