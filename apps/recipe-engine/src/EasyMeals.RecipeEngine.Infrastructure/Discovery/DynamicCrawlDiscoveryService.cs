using EasyMeals.RecipeEngine.Domain.Interfaces;
using EasyMeals.RecipeEngine.Domain.ValueObjects.Discovery;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace EasyMeals.RecipeEngine.Infrastructure.Discovery;

/// <summary>
///     T110: Dynamic crawl discovery service using Playwright for JavaScript-rendered pages
///     Implements discovery strategy for sites that require browser rendering
/// </summary>
public class DynamicCrawlDiscoveryService : IDiscoveryService
{
    private readonly ILogger<DynamicCrawlDiscoveryService> _logger;
    private readonly IPlaywright _playwright;

    public DynamicCrawlDiscoveryService(
        ILogger<DynamicCrawlDiscoveryService> logger,
        IPlaywright playwright)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _playwright = playwright ?? throw new ArgumentNullException(nameof(playwright));
    }

    /// <summary>
    ///     Discovers recipe URLs from a provider's base URL using dynamic rendering
    /// </summary>
    public async Task<IEnumerable<DiscoveredUrl>> DiscoverRecipeUrlsAsync(
        string baseUrl,
        string provider,
        int maxDepth = 3,
        int maxUrls = 1000,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Starting dynamic crawl discovery for provider {Provider} from {BaseUrl} (maxDepth: {MaxDepth}, maxUrls: {MaxUrls})",
            provider, baseUrl, maxDepth, maxUrls);

        var discoveredUrls = new List<DiscoveredUrl>();
        var visitedUrls = new HashSet<string>();

        IBrowser? browser = null;
        try
        {
            // T114: Configure Playwright browser launch options
            browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true,
                Args = new[] { "--no-sandbox", "--disable-setuid-sandbox" }, // For Docker compatibility
                Timeout = 30000 // 30 seconds timeout
            });

            IBrowserContext context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
                ViewportSize = new ViewportSize { Width = 1920, Height = 1080 }
            });

            // Recursively discover URLs starting from base URL
            await DiscoverRecursiveAsync(
                context,
                baseUrl,
                provider,
                0,
                maxDepth,
                maxUrls,
                discoveredUrls,
                visitedUrls,
                cancellationToken);

            await context.CloseAsync();

            _logger.LogInformation(
                "Dynamic crawl discovery completed for provider {Provider}. Discovered {Count} URLs",
                provider, discoveredUrls.Count);

            return discoveredUrls;
        }
        catch (PlaywrightException ex)
        {
            _logger.LogError(ex,
                "Playwright error during dynamic crawl for provider {Provider} at {BaseUrl}",
                provider, baseUrl);
            throw new DiscoveryException(
                $"Dynamic crawl discovery failed for provider {provider}",
                provider,
                baseUrl,
                ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Dynamic crawl discovery failed for provider {Provider} at {BaseUrl}",
                provider, baseUrl);
            throw new DiscoveryException(
                $"Dynamic crawl discovery failed for provider {provider}",
                provider,
                baseUrl,
                ex);
        }
        finally
        {
            if (browser != null) await browser.CloseAsync();
        }
    }

    /// <summary>
    ///     Recursive discovery of recipe URLs from JavaScript-rendered pages
    /// </summary>
    private async Task DiscoverRecursiveAsync(
        IBrowserContext context,
        string currentUrl,
        string provider,
        int currentDepth,
        int maxDepth,
        int maxUrls,
        List<DiscoveredUrl> discoveredUrls,
        HashSet<string> visitedUrls,
        CancellationToken cancellationToken)
    {
        // Check limits
        if (currentDepth > maxDepth || discoveredUrls.Count >= maxUrls) return;

        // Avoid revisiting URLs
        if (!visitedUrls.Add(currentUrl)) return;

        IPage? page = null;
        try
        {
            page = await context.NewPageAsync();

            // Navigate to URL
            await page.GotoAsync(currentUrl, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = 30000
            });

            // Wait for content to render
            try
            {
                await page.WaitForSelectorAsync("a[href]", new PageWaitForSelectorOptions
                {
                    Timeout = 5000
                });
            }
            catch (TimeoutException)
            {
                _logger.LogDebug("No links found on page {Url} after 5 seconds", currentUrl);
                return;
            }

            // Extract all links
            IReadOnlyList<IElementHandle> linkElements = await page.Locator("a[href]").ElementHandlesAsync();

            // Track category pages to crawl recursively
            var categoryUrlsToCrawl = new List<string>();

            foreach (IElementHandle linkElement in linkElements)
            {
                if (discoveredUrls.Count >= maxUrls) break;

                string? href = await linkElement.GetAttributeAsync("href");
                if (string.IsNullOrWhiteSpace(href)) continue;

                // Convert relative URLs to absolute
                if (!Uri.TryCreate(new Uri(currentUrl), href, out Uri? absoluteUri)) continue;

                var absoluteUrl = absoluteUri.ToString();

                // Check if this is a recipe URL
                if (IsRecipeUrl(absoluteUrl, provider))
                {
                    decimal confidence = CalculateConfidence(absoluteUrl);

                    var discoveredUrl = DiscoveredUrl.CreateDiscovered(
                        absoluteUrl,
                        provider,
                        currentUrl,
                        currentDepth,
                        confidence);

                    // Avoid duplicates
                    if (!discoveredUrls.Any(u => u.Url == absoluteUrl))
                    {
                        discoveredUrls.Add(discoveredUrl);
                        _logger.LogDebug(
                            "Discovered recipe URL: {Url} (depth: {Depth}, confidence: {Confidence:P0})",
                            absoluteUrl, currentDepth, confidence);
                    }
                }
                // Check if this is a category/listing page we should crawl
                else if (IsCategoryUrl(absoluteUrl) && currentDepth < maxDepth)
                {
                    categoryUrlsToCrawl.Add(absoluteUrl);
                }
            }

            // Recursively crawl category pages to find more recipes
            foreach (string categoryUrl in categoryUrlsToCrawl)
            {
                if (discoveredUrls.Count >= maxUrls) break;

                await DiscoverRecursiveAsync(
                    context,
                    categoryUrl,
                    provider,
                    currentDepth + 1,
                    maxDepth,
                    maxUrls,
                    discoveredUrls,
                    visitedUrls,
                    cancellationToken);
            }
        }
        catch (PlaywrightException ex)
        {
            _logger.LogWarning(ex,
                "Playwright error fetching URL {Url} at depth {Depth}",
                currentUrl, currentDepth);

            // Re-throw for the base URL (depth 0) to fail fast
            if (currentDepth == 0) throw;
        }
        catch (TimeoutException ex)
        {
            _logger.LogWarning(ex,
                "Timeout fetching URL {Url} at depth {Depth}",
                currentUrl, currentDepth);

            // Re-throw for the base URL (depth 0) to fail fast
            if (currentDepth == 0) throw;
        }
        finally
        {
            if (page != null) await page.CloseAsync();
        }
    }

    /// <summary>
    ///     Discovers recipe URLs from multiple seed URLs
    /// </summary>
    public async Task<IEnumerable<DiscoveredUrl>> DiscoverFromSeedUrlsAsync(
        IEnumerable<string> seedUrls,
        string provider,
        DiscoveryOptions discoveryOptions,
        CancellationToken cancellationToken = default)
    {
        var allDiscoveredUrls = new List<DiscoveredUrl>();

        foreach (string seedUrl in seedUrls)
        {
            if (cancellationToken.IsCancellationRequested) break;

            IEnumerable<DiscoveredUrl> urls = await DiscoverRecipeUrlsAsync(
                seedUrl,
                provider,
                discoveryOptions.MaxDepth,
                discoveryOptions.MaxUrls - allDiscoveredUrls.Count,
                cancellationToken);

            allDiscoveredUrls.AddRange(urls);

            if (allDiscoveredUrls.Count >= discoveryOptions.MaxUrls) break;

            // Add delay between requests to be respectful
            if (discoveryOptions.DelayBetweenRequests > TimeSpan.Zero) await Task.Delay(discoveryOptions.DelayBetweenRequests, cancellationToken);
        }

        return allDiscoveredUrls;
    }

    /// <summary>
    ///     Checks if a URL is likely to be a recipe page based on provider patterns
    /// </summary>
    public bool IsRecipeUrl(string url, string provider)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;

        // Common recipe URL patterns
        var recipePatterns = new[]
        {
            "/recipe/",
            "/recipes/",
            "/food/recipe",
            "/cooking/recipe",
            "/r/",
            "/dish/"
        };

        // Exclude common non-recipe patterns (pages we don't want as results)
        var excludePatterns = new[]
        {
            "/about",
            "/contact",
            "/privacy",
            "/terms",
            "/login",
            "/signup",
            "/cart",
            "/checkout",
            "/account",
            "/search"
        };

        string lowerUrl = url.ToLowerInvariant();

        // Check if URL matches exclude patterns
        if (excludePatterns.Any(pattern => lowerUrl.Contains(pattern))) return false;

        // Check if URL matches recipe patterns
        return recipePatterns.Any(pattern => lowerUrl.Contains(pattern));
    }

    /// <summary>
    ///     Checks if a URL is a category/listing page that should be crawled for recipes
    /// </summary>
    private bool IsCategoryUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;

        // Category/listing page patterns (pages we should crawl but not return as recipes)
        var categoryPatterns = new[]
        {
            "/category",
            "/categories",
            "/tag",
            "/tags",
            "/collection",
            "/cuisine",
            "/meal-type",
            "/recipes" // Main listings page
		};

        // Exclude patterns that shouldn't be crawled
        var excludePatterns = new[]
        {
            "/about",
            "/contact",
            "/privacy",
            "/terms",
            "/login",
            "/signup",
            "/cart",
            "/checkout",
            "/account",
            "/search"
        };

        string lowerUrl = url.ToLowerInvariant();

        // Don't crawl excluded pages
        if (excludePatterns.Any(pattern => lowerUrl.Contains(pattern))) return false;

        // Check if URL matches category patterns
        return categoryPatterns.Any(pattern => lowerUrl.Contains(pattern));
    }

    /// <summary>
    ///     Gets discovery statistics for monitoring and optimization
    /// </summary>
    public Task<DiscoveryStatistics> GetDiscoveryStatisticsAsync(
        string provider,
        TimeRange timeRange) =>
        // For dynamic discovery, we don't track detailed statistics
        // This would typically be implemented with a separate metrics service
        Task.FromResult(new DiscoveryStatistics(
            0,
            0,
            0,
            0m,
            TimeSpan.Zero,
            new Dictionary<int, int>(),
            DateTime.UtcNow));

    /// <summary>
    ///     Calculates confidence score based on URL patterns
    /// </summary>
    private decimal CalculateConfidence(string url)
    {
        string lowerUrl = url.ToLowerInvariant();

        // High confidence patterns (0.9)
        if (lowerUrl.Contains("/recipe/") || lowerUrl.Contains("/recipes/")) return 0.9m;

        // Medium confidence patterns (0.7)
        if (lowerUrl.Contains("/food/") || lowerUrl.Contains("/cooking/")) return 0.7m;

        // Low confidence patterns (0.5)
        return 0.5m;
    }
}