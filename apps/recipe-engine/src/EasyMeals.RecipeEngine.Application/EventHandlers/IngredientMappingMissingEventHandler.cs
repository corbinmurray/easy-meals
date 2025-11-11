using EasyMeals.RecipeEngine.Application.Interfaces;
using EasyMeals.RecipeEngine.Domain.Events;
using Microsoft.Extensions.Logging;

namespace EasyMeals.RecipeEngine.Application.EventHandlers;

/// <summary>
///     Event handler for IngredientMappingMissingEvent.
///     Logs unmapped ingredients with structured context for manual review and mapping updates.
/// </summary>
public sealed class IngredientMappingMissingEventHandler : IEventHandler<IngredientMappingMissingEvent>
{
	private readonly ILogger<IngredientMappingMissingEventHandler> _logger;

	public IngredientMappingMissingEventHandler(ILogger<IngredientMappingMissingEventHandler> logger) =>
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

	public Task HandleAsync(IngredientMappingMissingEvent @event, CancellationToken cancellationToken)
	{
		_logger.LogWarning(
			"Unmapped ingredient detected - Provider: {ProviderId}, Code: {ProviderCode}, Recipe URL: {RecipeUrl}, EventId: {EventId}",
			@event.ProviderId,
			@event.ProviderCode,
			@event.RecipeUrl,
			@event.EventId);

		// Event is logged for manual review - no blocking action required
		// Future enhancement: could aggregate unmapped ingredients and create tasks for data team

		return Task.CompletedTask;
	}
}