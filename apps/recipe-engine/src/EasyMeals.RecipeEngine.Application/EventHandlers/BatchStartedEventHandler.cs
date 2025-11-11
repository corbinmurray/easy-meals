using EasyMeals.RecipeEngine.Application.Interfaces;
using EasyMeals.RecipeEngine.Domain.Events;
using Microsoft.Extensions.Logging;

namespace EasyMeals.RecipeEngine.Application.EventHandlers;

/// <summary>
///     Event handler for BatchStartedEvent.
///     Logs batch start with structured logging for observability.
/// </summary>
public class BatchStartedEventHandler(ILogger<BatchStartedEventHandler> logger) : IEventHandler<BatchStartedEvent>
{
	public Task HandleAsync(BatchStartedEvent @event, CancellationToken cancellationToken = default)
	{
		using (logger.BeginScope(new Dictionary<string, object>
		       {
			       ["BatchId"] = @event.BatchId,
			       ["ProviderId"] = @event.ProviderId,
			       ["EventType"] = nameof(BatchStartedEvent)
		       }))
		{
			logger.LogInformation(
				"Recipe batch processing started - BatchId: {BatchId}, ProviderId: {ProviderId}, StartedAt: {StartedAt}",
				@event.BatchId,
				@event.ProviderId,
				@event.StartedAt);
		}

		return Task.CompletedTask;
	}
}