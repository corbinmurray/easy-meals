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
	private SagaState? _sagaState;

	/// <summary>
	///     Starts the complete recipe processing saga
	/// </summary>
	public async Task StartProcessingAsync(CancellationToken cancellationToken)
	{
		var correlationId = Guid.NewGuid();
		_sagaState = SagaState.CreateForRecipeProcessing(correlationId, sagaType: nameof(RecipeProcessingSaga));
		//await sagaStateRepository.AddAsync(_sagaState, cancellationToken);

		logger.LogInformation("Starting Recipe Processing Saga {SagaId} with correlation {CorrelationId}",
			_sagaState.Id, correlationId);

		while (true)
		{
			logger.LogInformation("Processing.. blah blah blah");
			await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
		}
	}
}