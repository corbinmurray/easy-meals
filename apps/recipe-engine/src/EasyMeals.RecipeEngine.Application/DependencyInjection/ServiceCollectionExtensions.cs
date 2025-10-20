using EasyMeals.RecipeEngine.Application.EventHandlers.DiscoveryEvents;
using EasyMeals.RecipeEngine.Application.Interfaces;
using EasyMeals.RecipeEngine.Domain.Events;
using EasyMeals.RecipeEngine.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EasyMeals.RecipeEngine.Application.DependencyInjection;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddRecipeEngine(this IServiceCollection services)
	{
		services.AddTransient<IRecipeEngine, Services.RecipeEngine>();
		services.AddEventBus();

		return services;
	}

	/// <summary>
	///     Register event bus handlers for domain events
	/// </summary>
	/// <param name="services"></param>
	/// <returns></returns>
	private static IServiceCollection AddEventBus(this IServiceCollection services)
	{
		services.AddSingleton<IEventBus>(sp =>
		{
			var eventBus = new EasyMealsEventBus(sp.GetRequiredService<ILogger<EasyMealsEventBus>>());

			eventBus.Subscribe<RecipeUrlsDiscoveredEvent>(async @event =>
			{
				var handler = sp.GetRequiredService<RecipeUrlsDiscoveredHandler>();
				await handler.HandleAsync(@event);
			});

			return eventBus;
		});

		return services;
	}
}