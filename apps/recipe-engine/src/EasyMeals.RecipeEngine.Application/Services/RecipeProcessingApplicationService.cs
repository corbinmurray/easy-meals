using EasyMeals.RecipeEngine.Application.Interfaces;
using EasyMeals.RecipeEngine.Domain.Entities;
using EasyMeals.RecipeEngine.Domain.Repositories;
using EasyMeals.RecipeEngine.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace EasyMeals.RecipeEngine.Application.Services;

/// <summary>
///     Application service that coordinates the recipe processing workflow.
///     Orchestrates saga startup, batch creation, and domain event handling.
/// </summary>
public class RecipeProcessingApplicationService(
    ILogger<RecipeProcessingApplicationService> logger,
    IProviderConfigurationLoader configurationLoader,
    IRecipeBatchRepository batchRepository,
    IRecipeProcessingSaga recipeProcessingSaga)
{
    /// <summary>
    ///     Starts a new recipe processing batch for the specified provider.
    /// </summary>
    /// <param name="providerId">Provider identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Batch correlation ID</returns>
    public async Task<Guid> StartBatchProcessingAsync(
        string providerId,
        CancellationToken cancellationToken = default)
    {
        using (logger.BeginScope(new Dictionary<string, object>
        {
            ["ProviderId"] = providerId,
            ["Operation"] = "StartBatchProcessing"
        }))
        {
            logger.LogInformation("Starting recipe batch processing for provider {ProviderId}", providerId);

            // Load provider configuration
            ProviderConfiguration? config = await configurationLoader.GetByProviderIdAsync(providerId, cancellationToken);
            if (config == null) throw new InvalidOperationException($"Provider configuration not found for {providerId}");

            // Create a new batch
            RecipeBatch batch = await batchRepository.CreateAsync(providerId, config, cancellationToken);
            logger.LogInformation(
                "Created recipe batch {BatchId} for provider {ProviderId} with size {BatchSize}",
                batch.Id, providerId, batch.BatchSize);

            // Start the saga
            Guid correlationId = await recipeProcessingSaga.StartProcessingAsync(
                providerId,
                batch.BatchSize,
                batch.TimeWindow,
                cancellationToken);

            logger.LogInformation(
                "Started recipe processing saga with correlation ID {CorrelationId} for batch {BatchId}",
                correlationId, batch.Id);

            return correlationId;
        }
    }

    /// <summary>
    ///     Resumes a previously started batch after application restart.
    /// </summary>
    /// <param name="batchId">Batch correlation ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task ResumeBatchProcessingAsync(
        Guid batchId,
        CancellationToken cancellationToken = default)
    {
        using (logger.BeginScope(new Dictionary<string, object>
        {
            ["BatchId"] = batchId,
            ["Operation"] = "ResumeBatchProcessing"
        }))
        {
            logger.LogInformation("Resuming recipe batch processing for batch {BatchId}", batchId);

            await recipeProcessingSaga.ResumeProcessingAsync(batchId, cancellationToken);

            logger.LogInformation("Resumed recipe processing saga for batch {BatchId}", batchId);
        }
    }

    /// <summary>
    ///     Gets the current status of a batch.
    /// </summary>
    /// <param name="batchId">Batch correlation ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task<RecipeBatch?> GetBatchStatusAsync(
        Guid batchId,
        CancellationToken cancellationToken = default) =>
        await recipeProcessingSaga.GetBatchStatusAsync(batchId, cancellationToken);

    /// <summary>
    ///     Processes recipes for all enabled providers sequentially.
    ///     Creates a batch for each provider and respects batch time windows to avoid overlapping batches.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary of provider IDs to their batch correlation IDs</returns>
    public async Task<Dictionary<string, Guid>> ProcessAllProvidersAsync(
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting batch processing for all enabled providers");

        var results = new Dictionary<string, Guid>();

        // Load all enabled provider configurations
        IEnumerable<ProviderConfiguration> configurations = await configurationLoader.GetAllEnabledAsync(cancellationToken);
        List<ProviderConfiguration> configList = configurations.ToList();

        logger.LogInformation("Found {Count} enabled provider(s) to process", configList.Count);

        // Process each provider sequentially to respect batch time windows
        foreach (ProviderConfiguration config in configList)
        {
            // Skip disabled providers (defensive check, should already be filtered)
            if (!config.Enabled)
            {
                logger.LogWarning(
                    "Skipping disabled provider {ProviderId} (filtered by GetAllEnabledAsync but Enabled=false)",
                    config.ProviderId);
                continue;
            }

            try
            {
                logger.LogInformation(
                    "Processing provider {ProviderId} with batch size {BatchSize} and time window {TimeWindow}",
                    config.ProviderId,
                    config.BatchSize,
                    config.TimeWindow);

                // Start batch processing for this provider
                Guid correlationId = await StartBatchProcessingAsync(config.ProviderId, cancellationToken);
                results[config.ProviderId] = correlationId;

                logger.LogInformation(
                    "Started batch processing for provider {ProviderId} with correlation ID {CorrelationId}",
                    config.ProviderId,
                    correlationId);

                // Respect batch time windows - wait before starting next provider
                // This ensures batches don't overlap and respects rate limits
                if (configList.IndexOf(config) < configList.Count - 1)
                {
                    logger.LogInformation(
                        "Waiting for provider {ProviderId} batch time window ({TimeWindow}) before processing next provider",
                        config.ProviderId,
                        config.TimeWindow);

                    await Task.Delay(config.TimeWindow, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Failed to start batch processing for provider {ProviderId}. Continuing with remaining providers.",
                    config.ProviderId);

                // Continue processing remaining providers even if one fails
                // This ensures partial failures don't block other providers
            }
        }

        logger.LogInformation(
            "Completed batch processing initialization for {SuccessCount}/{TotalCount} providers",
            results.Count,
            configList.Count);

        return results;
    }
}