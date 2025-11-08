using EasyMeals.RecipeEngine.Application.EventHandlers;
using EasyMeals.RecipeEngine.Application.EventHandlers.DiscoveryEvents;
using EasyMeals.RecipeEngine.Application.Interfaces;
using EasyMeals.RecipeEngine.Application.Sagas;
using EasyMeals.RecipeEngine.Domain.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EasyMeals.RecipeEngine.Application.DependencyInjection;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddRecipeEngine(this IServiceCollection services)
	{
		services.AddScoped<IRecipeProcessingSaga, RecipeProcessingSaga>();
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
		// Register all event handlers
		services.AddTransient<IEventHandler<RecipeUrlsDiscoveredEvent>, RecipeUrlsDiscoveredHandler>();
		services.AddTransient<IEventHandler<IngredientMappingMissingEvent>, IngredientMappingMissingEventHandler>();

		// Register event bus and subscribe to events
		services.AddSingleton<IEventBus>(sp =>
		{
			var eventBus = new EasyMealsEventBus(sp.GetRequiredService<ILogger<EasyMealsEventBus>>());

			// Generic registration method
			RegisterHandler<RecipeUrlsDiscoveredEvent, RecipeUrlsDiscoveredHandler>(eventBus, sp);
			RegisterHandler<IngredientMappingMissingEvent, IngredientMappingMissingEventHandler>(eventBus, sp);

			return eventBus;
		});

		return services;
	}

	private static void RegisterHandler<TEvent, THandler>(EasyMealsEventBus eventBus, IServiceProvider sp)
		where TEvent : IDomainEvent
		where THandler : IEventHandler<TEvent>
	{
		eventBus.Subscribe<TEvent>(async @event =>
		{
			try
			{
				var handler = sp.GetRequiredService<THandler>();
				await handler.HandleAsync(@event);
			}
			catch (Exception ex)
			{
				var logger = sp.GetRequiredService<ILogger<THandler>>();
				logger.LogError(ex, "Error handling {EventType}", typeof(TEvent).Name);
			}
		});
	}
}