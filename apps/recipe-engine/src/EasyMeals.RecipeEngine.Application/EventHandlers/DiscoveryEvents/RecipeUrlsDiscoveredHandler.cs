using EasyMeals.RecipeEngine.Application.Interfaces;
using EasyMeals.RecipeEngine.Domain.Events;

namespace EasyMeals.RecipeEngine.Application.EventHandlers.DiscoveryEvents;

public sealed class RecipeUrlsDiscoveredHandler : IEventHandler<RecipeUrlsDiscoveredEvent>
{
	public async Task HandleAsync(RecipeUrlsDiscoveredEvent @event) => throw new NotImplementedException();
}