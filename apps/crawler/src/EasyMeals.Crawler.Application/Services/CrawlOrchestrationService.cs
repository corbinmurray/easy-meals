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
    private readonly HttpClient _httpClient;
    private readonly ILogger<CrawlOrchestrationService> _logger;

    public CrawlOrchestrationService(
        ICrawlStateRepository crawlStateRepository,
        IRecipeRepository recipeRepository,
        IRecipeExtractor recipeExtractor,
        HttpClient httpClient,
        ILogger<CrawlOrchestrationService> logger)
    {
        _crawlStateRepository = crawlStateRepository;
        _recipeRepository = recipeRepository;
        _recipeExtractor = recipeExtractor;
        _httpClient = httpClient;
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
    /// Discovers recipe URLs from HelloFresh listing pages (placeholder implementation)
    /// </summary>
    private async Task<CrawlState> DiscoverRecipeUrlsAsync(CrawlState currentState, CancellationToken cancellationToken)
    {
        // TODO: Implement actual URL discovery logic for HelloFresh
        // This would involve crawling listing pages and extracting recipe URLs
        var discoveredUrls = new List<string>
        {
            "https://www.hellofresh.com/recipes/example-recipe-1",
            "https://www.hellofresh.com/recipes/example-recipe-2"
        };

        _logger.LogInformation("Discovered {Count} recipe URLs", discoveredUrls.Count);
        
        return currentState with { PendingUrls = discoveredUrls };
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

            // Add delay between requests to be respectful
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
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
            var htmlContent = await _httpClient.GetStringAsync(url, cancellationToken);
            
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
