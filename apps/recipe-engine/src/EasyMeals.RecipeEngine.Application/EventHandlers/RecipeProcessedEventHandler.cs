using EasyMeals.RecipeEngine.Application.Interfaces;
using EasyMeals.RecipeEngine.Domain.Events;
using Microsoft.Extensions.Logging;

namespace EasyMeals.RecipeEngine.Application.EventHandlers;

/// <summary>
///     Event handler for RecipeProcessedEvent.
///     Logs recipe processing with structured logging for observability and metrics.
/// </summary>
public class RecipeProcessedEventHandler(ILogger<RecipeProcessedEventHandler> logger) : IEventHandler<RecipeProcessedEvent>
{
	public Task HandleAsync(RecipeProcessedEvent @event, CancellationToken cancellationToken = default)
	{
		using (logger.BeginScope(new Dictionary<string, object>
		       {
			       ["RecipeUrl"] = @event.Url,
			       ["ProviderId"] = @event.ProviderId,
			       ["RecipeId"] = @event.RecipeId,
			       ["EventType"] = nameof(RecipeProcessedEvent)
		       }))
		{
			logger.LogInformation(
				"Recipe processed - URL: {RecipeUrl}, ProviderId: {ProviderId}, RecipeId: {RecipeId}, ProcessedAt: {ProcessedAt}",
				@event.Url,
				@event.ProviderId,
				@event.RecipeId,
				@event.ProcessedAt);
		}

		return Task.CompletedTask;
	}
}