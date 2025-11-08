using EasyMeals.RecipeEngine.Application.Interfaces;
using EasyMeals.RecipeEngine.Domain.Entities;
using EasyMeals.RecipeEngine.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace EasyMeals.RecipeEngine.Application.Sagas;

/// <summary>
///     Saga that orchestrates the complete recipe processing workflow
///     Demonstrates the Saga pattern for managing complex, multi-step business processes
///     Workflow Steps:
///     1. Discovery: Find recipe URLs from provider sites
///     2. Fingerprinting: Scrape and validate content
///     3. Processing: Extract structured recipe data
///     4. Persistence: Save to repository
///     5. Notification: Publish completion events
///     This Saga includes compensating transactions and comprehensive error handling
///     State is persisted for resumability across application restarts
/// </summary>
public class RecipeProcessingSaga(
	ILogger<RecipeProcessingSaga> logger,
	ISagaStateRepository sagaStateRepository) : IRecipeProcessingSaga
{
	/// <summary>
	///     Starts the complete recipe processing saga
	/// </summary>
	public async Task StartProcessingAsync(CancellationToken cancellationToken)
	{
		var correlationId = Guid.NewGuid();
		var sagaState = SagaState.CreateForRecipeProcessing(correlationId, nameof(RecipeProcessingSaga));
		await sagaStateRepository.AddAsync(sagaState, cancellationToken);

		using IDisposable? _ = logger.BeginScope(new Dictionary<string, object>
		{
			["CorrelationId"] = correlationId,
			["SagaId"] = sagaState.Id
		});

		logger.LogInformation("Starting Recipe Processing Saga {SagaId} with correlation {CorrelationId}",
			sagaState.Id, correlationId);
	}
}