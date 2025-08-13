using EasyMeals.Crawler.Domain.Entities;
using EasyMeals.Crawler.Domain.Interfaces;
using EasyMeals.Crawler.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace EasyMeals.Crawler.Application.Services;

/// <summary>
/// Application service that orchestrates the recipe crawling process
/// Coordinates between domain services and maintains crawl state
/// </summary>
public class CrawlOrchestrationService
{
    private readonly ICrawlStateRepository _crawlStateRepository;
    private readonly IRecipeRepository _recipeRepository;
    private readonly IRecipeExtractor _recipeExtractor;
    private readonly IHelloFreshHttpService _httpService;
    private readonly ILogger<CrawlOrchestrationService> _logger;

    public CrawlOrchestrationService(
        ICrawlStateRepository crawlStateRepository,
        IRecipeRepository recipeRepository,
        IRecipeExtractor recipeExtractor,
        IHelloFreshHttpService httpService,
        ILogger<CrawlOrchestrationService> logger)
    {
        _crawlStateRepository = crawlStateRepository;
        _recipeRepository = recipeRepository;
        _recipeExtractor = recipeExtractor;
        _httpService = httpService;
        _logger = logger;
    }

    /// <summary>
    /// Starts or resumes a crawl session
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task StartCrawlSessionAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting crawl session...");

        // Load existing state or create new one
        var crawlState = await _crawlStateRepository.LoadStateAsync(cancellationToken);
        
        if (!crawlState.PendingUrls.Any())
        {
            _logger.LogInformation("No pending URLs found. Discovering recipe URLs...");
            crawlState = await DiscoverRecipeUrlsAsync(crawlState, cancellationToken);
        }

        _logger.LogInformation("Found {PendingCount} pending URLs to process", crawlState.PendingUrls.Count);

        // Process pending URLs
        await ProcessPendingUrlsAsync(crawlState, cancellationToken);
    }

    /// <summary>
    /// Discovers recipe URLs from HelloFresh website
    /// </summary>
    private async Task<CrawlState> DiscoverRecipeUrlsAsync(CrawlState currentState, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Discovering recipe URLs from HelloFresh website...");
            
            // Use the HTTP service to discover URLs
            var discoveredUrls = await _httpService.DiscoverRecipeUrlsAsync(50, cancellationToken);
            
            if (!discoveredUrls.Any())
            {
                _logger.LogWarning("No recipe URLs discovered. Using fallback URLs for testing.");
                // Fallback URLs for testing
                discoveredUrls = new List<string>
                {
                    "https://www.hellofresh.com/recipes/winner-winner-chicken-orzo-dinner-5aaabf7530006c52b54bd0c2",
                    "https://www.hellofresh.com/recipes/korean-beef-bibimbap-5ab3b883ae08b53bb4024952"
                };
            }

            _logger.LogInformation("Discovered {Count} recipe URLs", discoveredUrls.Count);
            
            return currentState with { PendingUrls = discoveredUrls };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering recipe URLs: {Message}", ex.Message);
            
            // Return current state with empty URLs if discovery fails
            return currentState with { PendingUrls = new List<string>() };
        }
    }

    /// <summary>
    /// Processes all pending URLs in the crawl state
    /// </summary>
    private async Task ProcessPendingUrlsAsync(CrawlState crawlState, CancellationToken cancellationToken)
    {
        var currentState = crawlState;

        foreach (var url in crawlState.PendingUrls.ToList())
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Crawl session cancelled. Saving current state...");
                break;
            }

            try
            {
                var recipe = await ProcessSingleUrlAsync(url, cancellationToken);
                if (recipe is not null)
                {
                    var saved = await _recipeRepository.SaveRecipeAsync(recipe, cancellationToken);
                    if (saved)
                    {
                        currentState = currentState.MarkAsCompleted(recipe.Id, url);
                        _logger.LogInformation("Successfully processed recipe: {Title} ({Id})", recipe.Title, recipe.Id);
                    }
                    else
                    {
                        currentState = currentState.MarkAsFailed(url);
                        _logger.LogWarning("Failed to save recipe from URL: {Url}", url);
                    }
                }
                else
                {
                    currentState = currentState.MarkAsFailed(url);
                    _logger.LogWarning("Failed to extract recipe from URL: {Url}", url);
                }
            }
            catch (Exception ex)
            {
                currentState = currentState.MarkAsFailed(url);
                _logger.LogError(ex, "Error processing URL: {Url}", url);
            }

            // Save state periodically
            await _crawlStateRepository.SaveStateAsync(currentState, cancellationToken);

            // Note: The HTTP service handles rate limiting internally
            // No need for additional delays here
        }

        // Final state save
        await _crawlStateRepository.SaveStateAsync(currentState, cancellationToken);
        
        _logger.LogInformation("Crawl session completed. Processed: {Total}, Successful: {Success}, Failed: {Failed}", 
            currentState.TotalProcessed, currentState.TotalSuccessful, currentState.TotalFailed);
    }

    /// <summary>
    /// Processes a single recipe URL
    /// </summary>
    private async Task<Recipe?> ProcessSingleUrlAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Fetching content from URL: {Url}", url);
            
            // Use the HTTP service to fetch HTML content
            var htmlContent = await _httpService.FetchRecipeHtmlAsync(url, cancellationToken);
            
            if (string.IsNullOrEmpty(htmlContent))
            {
                _logger.LogWarning("No HTML content received for URL: {Url}", url);
                return null;
            }
            
            // Extract recipe from HTML content
            var recipe = await _recipeExtractor.ExtractRecipeAsync(htmlContent, url, cancellationToken);
            return recipe;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process URL: {Url}", url);
            return null;
        }
    }
}
