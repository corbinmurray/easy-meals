using EasyMeals.Crawler.Domain.Entities;
using EasyMeals.Crawler.Domain.Interfaces;
using EasyMeals.Crawler.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace EasyMeals.Crawler.Application.Services;

/// <summary>
///     Application service that orchestrates the recipe crawling process
///     Coordinates between domain services and maintains crawl state
/// </summary>
public class CrawlOrchestrationService(
    ICrawlStateRepository crawlStateRepository,
    IRecipeRepository recipeRepository,
    IRecipeExtractor recipeExtractor,
    IHelloFreshHttpService httpService,
    ILogger<CrawlOrchestrationService> logger)
{
    /// <summary>
    ///     Starts or resumes a crawl session
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task StartCrawlSessionAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting crawl session...");

        // Load existing state or create new one
        CrawlState crawlState = await crawlStateRepository.LoadStateAsync(cancellationToken);

        if (!crawlState.PendingUrls.Any())
        {
            logger.LogInformation("No pending URLs found. Discovering recipe URLs...");
            crawlState = await DiscoverRecipeUrlsAsync(crawlState, cancellationToken);
        }

        logger.LogInformation("Found {PendingCount} pending URLs to process", crawlState.PendingUrls.Count());

        // Process pending URLs
        await ProcessPendingUrlsAsync(crawlState, cancellationToken);
    }

    /// <summary>
    ///     Discovers recipe URLs from HelloFresh website
    /// </summary>
    private async Task<CrawlState> DiscoverRecipeUrlsAsync(CrawlState currentState, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Discovering recipe URLs from HelloFresh website...");

            // Use the HTTP service to discover URLs
            List<string> discoveredUrls = await httpService.DiscoverRecipeUrlsAsync(50, cancellationToken);

            if (!discoveredUrls.Any())
            {
                logger.LogWarning("No recipe URLs discovered. Using fallback URLs for testing.");

                return currentState with { PendingUrls = [] };
            }

            logger.LogInformation("Discovered {Count} recipe URLs", discoveredUrls.Count);

            return currentState with { PendingUrls = discoveredUrls };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error discovering recipe URLs: {Message}", ex.Message);

            // Return current state with empty URLs if discovery fails
            return currentState with { PendingUrls = [] };
        }
    }

    /// <summary>
    ///     Processes all pending URLs in the crawl state
    /// </summary>
    private async Task ProcessPendingUrlsAsync(CrawlState crawlState, CancellationToken cancellationToken)
    {
        CrawlState currentState = crawlState;

        foreach (string url in crawlState.PendingUrls)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                logger.LogInformation("Crawl session cancelled. Saving current state...");
                break;
            }

            try
            {
                Recipe? recipe = await ProcessSingleUrlAsync(url, cancellationToken);
                if (recipe is not null)
                {
                    bool saved = await recipeRepository.SaveRecipeAsync(recipe, cancellationToken);
                    if (saved)
                    {
                        currentState = currentState.MarkAsCompleted(recipe.Id, url);
                        logger.LogInformation("Successfully processed recipe: {Title} ({Id})", recipe.Title, recipe.Id);
                    }
                    else
                    {
                        currentState = currentState.MarkAsFailed(url);
                        logger.LogWarning("Failed to save recipe from URL: {Url}", url);
                    }
                }
                else
                {
                    currentState = currentState.MarkAsFailed(url);
                    logger.LogWarning("Failed to extract recipe from URL: {Url}", url);
                }
            }
            catch (Exception ex)
            {
                currentState = currentState.MarkAsFailed(url);
                logger.LogError(ex, "Error processing URL: {Url}", url);
            }

            // Save state periodically
            await crawlStateRepository.SaveStateAsync(currentState, cancellationToken);

            // Note: The HTTP service handles rate limiting internally
            // No need for additional delays here
        }

        // Final state save
        await crawlStateRepository.SaveStateAsync(currentState, cancellationToken);

        logger.LogInformation("Crawl session completed. Processed: {Total}, Successful: {Success}, Failed: {Failed}",
            currentState.TotalProcessed, currentState.TotalSuccessful, currentState.TotalFailed);
    }

    /// <summary>
    ///     Processes a single recipe URL
    /// </summary>
    private async Task<Recipe?> ProcessSingleUrlAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogDebug("Fetching content from URL: {Url}", url);

            // Use the HTTP service to fetch HTML content
            string? htmlContent = await httpService.FetchRecipeHtmlAsync(url, cancellationToken);

            if (string.IsNullOrEmpty(htmlContent))
            {
                logger.LogWarning("No HTML content received for URL: {Url}", url);
                return null;
            }

            // Extract recipe from HTML content
            Recipe? recipe = await recipeExtractor.ExtractRecipeAsync(htmlContent, url, cancellationToken);
            return recipe;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process URL: {Url}", url);
            return null;
        }
    }
}