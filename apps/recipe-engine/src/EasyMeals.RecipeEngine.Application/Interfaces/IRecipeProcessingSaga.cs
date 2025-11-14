using EasyMeals.RecipeEngine.Domain.Entities;

namespace EasyMeals.RecipeEngine.Application.Interfaces;

/// <summary>
///     Service interface for orchestrating the multi-step recipe processing workflow.
///     Implements the Saga pattern for state persistence and crash recovery.
///     Workflow stages:
///     1. Discovering: Fetch recipe URLs from provider
///     2. Fingerprinting: Generate fingerprints and filter duplicates
///     3. Processing: Scrape and parse recipe data
///     4. Persisting: Save recipes to MongoDB
///     5. Completed: Emit domain events
/// </summary>
public interface IRecipeProcessingSaga
{
    /// <summary>
    ///     Starts processing a new recipe batch for the specified provider.
    ///     Creates a new saga state and persists to MongoDB for crash recovery.
    /// </summary>
    /// <param name="providerId">Provider identifier (e.g., "provider_001")</param>
    /// <param name="batchSize">Maximum recipes to process</param>
    /// <param name="timeWindow">Maximum duration for batch processing</param>
    /// <param name="cancellationToken">Cancellation token for graceful shutdown</param>
    /// <returns>Batch ID (correlation ID for saga)</returns>
    Task<Guid> StartProcessingAsync(
        string providerId,
        int batchSize,
        TimeSpan timeWindow,
        CancellationToken cancellationToken = default);
}