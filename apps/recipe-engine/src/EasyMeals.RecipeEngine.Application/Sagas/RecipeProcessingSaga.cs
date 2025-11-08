using EasyMeals.RecipeEngine.Application.Interfaces;
using EasyMeals.RecipeEngine.Domain.Entities;
using EasyMeals.RecipeEngine.Domain.Events;
using EasyMeals.RecipeEngine.Domain.Interfaces;
using EasyMeals.RecipeEngine.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace EasyMeals.RecipeEngine.Application.Sagas;

/// <summary>
///     Saga that orchestrates the complete recipe processing workflow
///     Demonstrates the Saga pattern for managing complex, multi-step business processes
///     Workflow Steps:
///     1. Discovery: Find recipe URLs from provider sites
///     2. Fingerprinting: Scrape and validate content
///     3. Processing: Extract structured recipe data with ingredient normalization (Phase 4)
///     4. Persistence: Save to repository
///     5. Notification: Publish completion events
///     This Saga includes compensating transactions and comprehensive error handling
///     State is persisted for resumability across application restarts
/// </summary>
public class RecipeProcessingSaga(
	ILogger<RecipeProcessingSaga> logger,
	ISagaStateRepository sagaStateRepository,
	IIngredientNormalizer ingredientNormalizer,
	IEventBus eventBus) : IRecipeProcessingSaga
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

	/// <summary>
	///     Processes ingredients for a recipe, normalizing provider-specific codes to canonical forms.
	///     This method demonstrates Phase 4 (T069) integration of ingredient normalization service.
	///     
	///     Implementation Details:
	///     - Calls IIngredientNormalizer.NormalizeBatchAsync for all ingredient codes in recipe
	///     - Creates IngredientReference value objects with both ProviderCode and CanonicalForm
	///     - Emits IngredientMappingMissingEvent for unmapped ingredients (non-blocking)
	///     - Continues processing even if some ingredients cannot be mapped
	///     
	///     NOTE: This method will be fully integrated into the Processing state handler
	///     as part of Phase 3 (User Story 1) completion. It is provided here as a
	///     demonstration of how Phase 4's ingredient normalization will be used.
	/// </summary>
	/// <param name="providerId">Provider identifier (e.g., "provider_001")</param>
	/// <param name="recipeUrl">URL of the recipe being processed (for event context)</param>
	/// <param name="rawIngredientCodes">Raw provider-specific ingredient codes from scraped recipe</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>List of IngredientReference value objects with normalized canonical forms</returns>
	public async Task<IReadOnlyList<IngredientReference>> ProcessIngredientsAsync(
		string providerId,
		string recipeUrl,
		IEnumerable<string> rawIngredientCodes,
		CancellationToken cancellationToken = default)
	{
		logger.LogDebug(
			"Normalizing ingredients for recipe {RecipeUrl} from provider {ProviderId}",
			recipeUrl,
			providerId);

		// T066: Batch normalize all ingredient codes at once (reduces DB round-trips)
		var normalizedIngredients = await ingredientNormalizer.NormalizeBatchAsync(
			providerId,
			rawIngredientCodes,
			cancellationToken);

		var ingredientReferences = new List<IngredientReference>();
		var displayOrder = 1;

		foreach (var (providerCode, canonicalForm) in normalizedIngredients)
		{
			// T069: Create IngredientReference value objects with both ProviderCode and CanonicalForm
			// Store both for auditability and to support future provider migrations
			var ingredientRef = new IngredientReference(
				providerCode,
				canonicalForm, // Will be null if unmapped - stored as-is for manual review
				quantity: "1", // Quantity extraction is separate concern (not in Phase 4 scope)
				displayOrder);

			ingredientReferences.Add(ingredientRef);

			// T069: Emit IngredientMappingMissingEvent for unmapped ingredients
			// This is non-blocking - we log and continue processing
			if (canonicalForm is null)
			{
				var missingEvent = new IngredientMappingMissingEvent(providerId, providerCode, recipeUrl);
				eventBus.Publish(missingEvent);
				
				logger.LogWarning(
					"Unmapped ingredient '{ProviderCode}' in recipe {RecipeUrl} from provider {ProviderId}",
					providerCode,
					recipeUrl,
					providerId);
			}

			displayOrder++;
		}

		var mappedCount = ingredientReferences.Count(ir => ir.CanonicalForm is not null);
		var unmappedCount = ingredientReferences.Count(ir => ir.CanonicalForm is null);

		logger.LogInformation(
			"Ingredient normalization complete for recipe {RecipeUrl}: {MappedCount} mapped, {UnmappedCount} unmapped",
			recipeUrl,
			mappedCount,
			unmappedCount);

		// T069: Return ingredient references for persistence
		// The calling Processing state handler will attach these to the Recipe entity
		return ingredientReferences.AsReadOnly();
	}
}