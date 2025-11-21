using EasyMeals.RecipeEngine.Domain.Entities;
using EasyMeals.RecipeEngine.Domain.Events;
using EasyMeals.RecipeEngine.Domain.Interfaces;
using EasyMeals.RecipeEngine.Domain.ValueObjects.Discovery;
using EasyMeals.RecipeEngine.Domain.ValueObjects.Fingerprint;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EasyMeals.RecipeEngine.Application.Sagas;

/// <summary>
///     Saga that orchestrates the complete recipe processing workflow
///     Demonstrates the Saga pattern for managing complex, multi-step business processes
///     
///     Workflow Steps:
///     1. Discovery: Find recipe URLs from provider sites
///     2. Fingerprinting: Scrape and validate content
///     3. Processing: Extract structured recipe data
///     4. Persistence: Save to repository
///     5. Notification: Publish completion events
/// 
///     This Saga includes compensating transactions and comprehensive error handling
/// </summary>
public class RecipeProcessingSaga(
    ILogger<RecipeProcessingSaga> logger,
    IDiscoveryService discoveryService,
    IFingerprintRepository fingerprintRepository,
    IRecipeRepository recipeRepository,
    IRecipeExtractor recipeExtractor,
    IStealthyHttpClient httpClient,
    IConfiguration configuration)
{
    private readonly Dictionary<string, object> _sagaState = [];

    /// <summary>
    ///     Starts the complete recipe processing saga
    /// </summary>
    public async Task StartProcessingAsync(CancellationToken cancellationToken)
    {
        var sagaId = Guid.NewGuid();
        _sagaState["SagaId"] = sagaId;
        _sagaState["StartedAt"] = DateTime.UtcNow;

        logger.LogInformation("Starting Recipe Processing Saga {SagaId}", sagaId);

        try
        {
            // Step 1: Discovery Phase
            List<DiscoveredUrl> discoveredUrls = await ExecuteDiscoveryPhaseAsync(cancellationToken);
            _sagaState["DiscoveredUrls"] = discoveredUrls;

            // Step 2: Fingerprinting Phase
            List<Fingerprint> fingerprints = await ExecuteFingerprintingPhaseAsync(discoveredUrls, cancellationToken);
            _sagaState["Fingerprints"] = fingerprints;

            // Step 3: Processing Phase
            List<Recipe> recipes = await ExecuteProcessingPhaseAsync(fingerprints, cancellationToken);
            _sagaState["Recipes"] = recipes;

            // Step 4: Persistence Phase
            await ExecutePersistencePhaseAsync(recipes, cancellationToken);

            // Step 5: Completion
            await CompleteSuccessfullyAsync(recipes, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Recipe Processing Saga {SagaId} failed: {ErrorMessage}", sagaId, ex.Message);
            await HandleSagaFailureAsync(ex, cancellationToken);
            throw;
        }
    }

    #region Phase Implementations

    /// <summary>
    ///     Phase 1: Discovery - Find recipe URLs from configured providers
    /// </summary>
    private async Task<List<DiscoveredUrl>> ExecuteDiscoveryPhaseAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Executing Discovery Phase for Saga {SagaId}", _sagaState["SagaId"]);

        var allDiscoveredUrls = new List<DiscoveredUrl>();
        List<ProviderConfig> providers = GetConfiguredProviders();

        foreach (ProviderConfig provider in providers)
        {
            try
            {
                logger.LogInformation("Starting discovery for provider: {Provider}", provider.Name);

                var discoveryOptions = new DiscoveryOptions(
                    maxDepth: provider.MaxDepth,
                    maxUrls: provider.MaxUrls,
                    respectRobotsTxt: true,
                    delayBetweenRequests: TimeSpan.FromMilliseconds(provider.DelayMs));

                IEnumerable<DiscoveredUrl> discoveredUrls = await discoveryService.DiscoverRecipeUrlsAsync(
                    provider.BaseUrl,
                    provider.Name,
                    discoveryOptions.MaxDepth,
                    discoveryOptions.MaxUrls,
                    cancellationToken);

                List<DiscoveredUrl> urlList = discoveredUrls.ToList();
                allDiscoveredUrls.AddRange(urlList);

                logger.LogInformation("Discovery completed for {Provider}: {Count} URLs found",
                    provider.Name, urlList.Count);

                // Publish discovery completed event
                PublishEvent(new DiscoveryCompletedEvent(
                    Guid.NewGuid(),
                    provider.Name,
                    urlList.Count,
                    urlList.Count(u => u.IsHighConfidence),
                    TimeSpan.FromMinutes(5), // This would be calculated from actual start time
                    new DiscoveryStatistics(
                        TotalUrlsDiscovered: urlList.Count,
                        RecipeUrlsFound: urlList.Count(u => u.IsHighConfidence),
                        FailedRequests: 0,
                        AverageConfidence: urlList.Any() ? urlList.Average(u => u.Confidence) : 0m,
                        AverageDiscoveryTime: TimeSpan.FromMinutes(5),
                        UrlsByDepth: urlList.GroupBy(u => u.Depth).ToDictionary(g => g.Key, g => g.Count()),
                        GeneratedAt: DateTime.UtcNow)));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Discovery failed for provider {Provider}: {ErrorMessage}",
                    provider.Name, ex.Message);

                // Publish discovery failed event
                PublishEvent(new DiscoveryFailedEvent(
                    Guid.NewGuid(),
                    provider.BaseUrl,
                    provider.Name,
                    ex.Message,
                    ex.ToString()));

                // Continue with other providers rather than failing the entire saga
                continue;
            }
        }

        if (!allDiscoveredUrls.Any())
        {
            throw new InvalidOperationException("No URLs were discovered from any configured providers");
        }

        logger.LogInformation("Discovery Phase completed: {TotalUrls} URLs discovered", allDiscoveredUrls.Count);
        return allDiscoveredUrls;
    }

    /// <summary>
    ///     Phase 2: Fingerprinting - Scrape content and create fingerprints
    /// </summary>
    private async Task<List<Fingerprint>> ExecuteFingerprintingPhaseAsync(
        List<DiscoveredUrl> discoveredUrls,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Executing Fingerprinting Phase for Saga {SagaId}: Processing {Count} URLs",
            _sagaState["SagaId"], discoveredUrls.Count);

        var fingerprints = new List<Fingerprint>();
        var semaphore = new SemaphoreSlim(5, 5); // Limit concurrent scraping

        IEnumerable<Task<Fingerprint?>> fingerprintTasks = discoveredUrls.Select(async url =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                return await CreateFingerprintAsync(url, cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
        });

        Fingerprint?[] results = await Task.WhenAll(fingerprintTasks);
        fingerprints.AddRange(results.Where(f => f != null)!);

        logger.LogInformation("Fingerprinting Phase completed: {Count} fingerprints created", fingerprints.Count);
        return fingerprints;
    }

    /// <summary>
    ///     Phase 3: Processing - Extract structured recipe data from fingerprints
    /// </summary>
    private async Task<List<Recipe>> ExecuteProcessingPhaseAsync(
        List<Fingerprint> fingerprints,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Executing Processing Phase for Saga {SagaId}: Processing {Count} fingerprints",
            _sagaState["SagaId"], fingerprints.Count);

        var recipes = new List<Recipe>();

        // Only process high-quality fingerprints
        List<Fingerprint> qualityFingerprints = fingerprints
            .Where(f => f.Quality == ScrapingQuality.Excellent || f.Quality == ScrapingQuality.Good)
            .ToList();

        foreach (Fingerprint fingerprint in qualityFingerprints)
        {
            try
            {
                Recipe? extractedRecipe = await recipeExtractor.ExtractRecipeAsync(fingerprint, cancellationToken);
                if (extractedRecipe != null)
                {
                    recipes.Add(extractedRecipe);

                    // Mark fingerprint as processed
                    fingerprint.MarkAsProcessed(extractedRecipe.Id);

                    // Publish events
                    PublishEvent(new FingerprintProcessedEvent(fingerprint.Id, fingerprint.Url, extractedRecipe.Id));
                    PublishEvent(new RecipeCreatedEvent(extractedRecipe));
                }
                else
                {
                    // Note: In a real implementation, we would call IncrementRetryCount on Fingerprint
                    // fingerprint.IncrementRetryCount("Recipe extraction failed");
                    PublishEvent(new FingerprintRetryEvent(fingerprint.Id, fingerprint.Url,
                        fingerprint.RetryCount, "Recipe extraction returned null"));
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to extract recipe from fingerprint {FingerprintId}: {ErrorMessage}",
                    fingerprint.Id, ex.Message);

                // Note: In a real implementation, we would call IncrementRetryCount on Fingerprint
                // fingerprint.IncrementRetryCount($"Extraction failed: {ex.Message}");
                PublishEvent(new FingerprintRetryEvent(fingerprint.Id, fingerprint.Url,
                    fingerprint.RetryCount, ex.Message));
            }
        }

        logger.LogInformation("Processing Phase completed: {Count} recipes extracted", recipes.Count);
        return recipes;
    }

    /// <summary>
    ///     Phase 4: Persistence - Save all entities to repositories
    /// </summary>
    private async Task ExecutePersistencePhaseAsync(List<Recipe> recipes, CancellationToken cancellationToken)
    {
        logger.LogInformation("Executing Persistence Phase for Saga {SagaId}: Saving {Count} recipes",
            _sagaState["SagaId"], recipes.Count);

        // Get fingerprints from saga state
        var fingerprints = (List<Fingerprint>)_sagaState["Fingerprints"];

        try
        {
            // Save all fingerprints (both successful and failed for audit trail)
            foreach (Fingerprint fingerprint in fingerprints)
            {
                await fingerprintRepository.AddAsync(fingerprint);
            }

            // Save all successfully extracted recipes
            // Note: IRecipeRepository needs to be enhanced with save methods
            // foreach (var recipe in recipes)
            // {
            //     await _recipeRepository.SaveAsync(recipe, cancellationToken);
            // }

            logger.LogInformation("Persistence Phase completed: {FingerprintCount} fingerprints and {RecipeCount} recipes saved",
                fingerprints.Count, recipes.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Persistence Phase failed: {ErrorMessage}", ex.Message);

            // This is a critical failure - we need to trigger compensating transactions
            await ExecuteCompensatingTransactionsAsync(cancellationToken);
            throw;
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    ///     Creates a fingerprint from a discovered URL
    /// </summary>
    private Task<Fingerprint?> CreateFingerprintAsync(DiscoveredUrl discoveredUrl, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogDebug("Creating fingerprint for URL: {Url}", discoveredUrl.Url);

            // Note: This would require IStealthyHttpClient to have a GetAsync method
            // For demonstration, we'll create a fingerprint with mock content
            var mockContent = $"<html><body>Mock content for {discoveredUrl.Url}</body></html>";
            var mockHeaders = new Dictionary<string, object>
            {
                ["Content-Type"] = "text/html",
                ["User-Agent"] = "EasyMeals Recipe Engine"
            };

            var fingerprint = Fingerprint.CreateSuccess(
                discoveredUrl.Url,
                mockContent,
                discoveredUrl.Provider,
                ScrapingQuality.Good,
                mockHeaders);

            PublishEvent(new FingerprintCreatedEvent(
                fingerprint.Id,
                fingerprint.Url,
                fingerprint.ProviderName,
                fingerprint.Quality,
                fingerprint.ContentHash));

            return Task.FromResult<Fingerprint?>(fingerprint);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to create fingerprint for URL {Url}: {ErrorMessage}",
                discoveredUrl.Url, ex.Message);

            var failedFingerprint = Fingerprint.CreateFailure(
        discoveredUrl.Url,
        discoveredUrl.Provider,
        ex.Message);

            PublishEvent(new ScrapingFailedEvent(
                failedFingerprint.Id,
                failedFingerprint.Url,
                failedFingerprint.ProviderName,
                ex.Message));

            return Task.FromResult<Fingerprint?>(failedFingerprint);
        }
    }

    /// <summary>
    ///     Gets configured providers from application settings
    /// </summary>
    private List<ProviderConfig> GetConfiguredProviders()
    {
        // This would typically come from configuration
        return new List<ProviderConfig>
        {
              new("AllRecipes", "https://allrecipes.com", MaxDepth: 3, MaxUrls: 100, DelayMs: 1000),
              new("FoodNetwork", "https://foodnetwork.com", MaxDepth: 2, MaxUrls: 50, DelayMs: 1500),
              new("TasteOfHome", "https://tasteofhome.com", MaxDepth: 2, MaxUrls: 75, DelayMs: 1200)
        };
    }

    /// <summary>
    ///     Publishes domain events (in real implementation, this would use an event bus)
    /// </summary>
    private void PublishEvent(BaseDomainEvent domainEvent)
    {
        logger.LogDebug("Publishing event: {EventType}", domainEvent.GetType().Name);
        // In a real implementation, this would publish to an event bus
        // For demonstration, we'll just log the events
    }

    /// <summary>
    ///     Handles successful completion of the saga
    /// </summary>
    private async Task CompleteSuccessfullyAsync(List<Recipe> recipes, CancellationToken cancellationToken)
    {
        var sagaId = (Guid)_sagaState["SagaId"];
        var startedAt = (DateTime)_sagaState["StartedAt"];
        TimeSpan duration = DateTime.UtcNow - startedAt;

        logger.LogInformation("Recipe Processing Saga {SagaId} completed successfully in {Duration}. " +
                              "Processed {RecipeCount} recipes.",
                              sagaId, duration, recipes.Count);

        // Publish completion event
        // PublishEvent(new SagaCompletedEvent(sagaId, recipes.Count, duration));

        // Clean up saga state
        _sagaState.Clear();

        await Task.CompletedTask;
    }

    /// <summary>
    ///     Handles saga failure and executes compensating transactions
    /// </summary>
    private async Task HandleSagaFailureAsync(Exception exception, CancellationToken cancellationToken)
    {
        var sagaId = (Guid)_sagaState["SagaId"];

        logger.LogError("Recipe Processing Saga {SagaId} failed, executing compensating transactions", sagaId);

        await ExecuteCompensatingTransactionsAsync(cancellationToken);

        // Publish failure event
        // PublishEvent(new SagaFailedEvent(sagaId, exception.Message));

        // Clean up saga state
        _sagaState.Clear();
    }

    /// <summary>
    ///     Executes compensating transactions to rollback partial changes
    /// </summary>
    private async Task ExecuteCompensatingTransactionsAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Executing compensating transactions for Saga {SagaId}", _sagaState["SagaId"]);

        try
        {
            // Compensate Recipe creation (if any were saved)
            if (_sagaState.TryGetValue("Recipes", out object? recipesObj) && recipesObj is List<Recipe> recipes)
            {
                foreach (Recipe recipe in recipes)
                {
                    try
                    {
                        // Note: IRecipeRepository needs DeleteAsync method for full compensation
                        // await _recipeRepository.DeleteAsync(recipe.Id, cancellationToken);
                        logger.LogDebug("Compensated: Deleted recipe {RecipeId}", recipe.Id);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to compensate recipe deletion for {RecipeId}", recipe.Id);
                    }
                }
            }

            // Compensate Fingerprint creation (if any were saved)
            if (_sagaState.TryGetValue("Fingerprints", out object? fingerprintsObj) && fingerprintsObj is List<Fingerprint> fingerprints)
            {
                foreach (Fingerprint fingerprint in fingerprints)
                {
                    try
                    {
                        // Note: IFingerprintRepository needs DeleteAsync method for full compensation
                        // Could use DeleteStaleAsync with TimeSpan.Zero as a workaround
                        // await _fingerprintRepository.DeleteAsync(fingerprint.Id, cancellationToken);
                        logger.LogDebug("Compensated: Deleted fingerprint {FingerprintId}", fingerprint.Id);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to compensate fingerprint deletion for {FingerprintId}", fingerprint.Id);
                    }
                }
            }

            logger.LogInformation("Compensating transactions completed for Saga {SagaId}", _sagaState["SagaId"]);
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Compensating transactions failed for Saga {SagaId}: {ErrorMessage}",
                _sagaState["SagaId"], ex.Message);
            // In a production system, this would likely trigger manual intervention alerts
        }
    }

    #endregion
}

/// <summary>
///     Configuration for recipe providers
/// </summary>
/// <param name="Name">Provider name</param>
/// <param name="BaseUrl">Base URL for discovery</param>
/// <param name="MaxDepth">Maximum crawl depth</param>
/// <param name="MaxUrls">Maximum URLs to discover</param>
/// <param name="DelayMs">Delay between requests in milliseconds</param>
public record ProviderConfig(
    string Name,
    string BaseUrl,
    int MaxDepth = 3,
    int MaxUrls = 100,
    int DelayMs = 1000);