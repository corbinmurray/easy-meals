using EasyMeals.RecipeEngine.Application.Interfaces;
using EasyMeals.RecipeEngine.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace EasyMeals.RecipeEngine.Application.Services;

/// <summary>
/// Application service that coordinates the recipe processing workflow.
/// Orchestrates saga startup, batch creation, and domain event handling.
/// </summary>
public class RecipeProcessingApplicationService(
	ILogger<RecipeProcessingApplicationService> logger,
	IProviderConfigurationLoader configurationLoader,
	IRecipeBatchRepository batchRepository,
	IRecipeProcessingSaga recipeProcessingSaga)
{
	/// <summary>
	/// Starts a new recipe processing batch for the specified provider.
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
			var config = await configurationLoader.GetByProviderIdAsync(providerId, cancellationToken);
			if (config == null)
			{
				throw new InvalidOperationException($"Provider configuration not found for {providerId}");
			}

			// Create a new batch
			var batch = await batchRepository.CreateAsync(providerId, config, cancellationToken);
			logger.LogInformation(
				"Created recipe batch {BatchId} for provider {ProviderId} with size {BatchSize}",
				batch.Id, providerId, batch.BatchSize);

			// Start the saga
			var correlationId = await recipeProcessingSaga.StartProcessingAsync(
				providerId,
				(int)batch.BatchSize,
				batch.TimeWindow,
				cancellationToken);

			logger.LogInformation(
				"Started recipe processing saga with correlation ID {CorrelationId} for batch {BatchId}",
				correlationId, batch.Id);

			return correlationId;
		}
	}

	/// <summary>
	/// Resumes a previously started batch after application restart.
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
	/// Gets the current status of a batch.
	/// </summary>
	/// <param name="batchId">Batch correlation ID</param>
	/// <param name="cancellationToken">Cancellation token</param>
	public async Task<Domain.Entities.RecipeBatch?> GetBatchStatusAsync(
		Guid batchId,
		CancellationToken cancellationToken = default)
	{
		return await recipeProcessingSaga.GetBatchStatusAsync(batchId, cancellationToken);
	}
}
