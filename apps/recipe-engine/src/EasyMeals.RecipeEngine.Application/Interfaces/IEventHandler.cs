using EasyMeals.RecipeEngine.Domain.Events;

namespace EasyMeals.RecipeEngine.Application.Interfaces;

public interface IEventHandler<in TEvent>
	where TEvent : IDomainEvent
{
	Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default);
}