using EasyMeals.RecipeEngine.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace EasyMeals.RecipeEngine.Application.DependencyInjection;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddRecipeEngine(this IServiceCollection services)
	{
		services.AddTransient<IRecipeEngine, Services.RecipeEngine>();

		return services;
	}
}