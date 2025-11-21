using EasyMeals.Shared.Data.DependencyInjection;
using EasyMeals.Shared.Data.Repositories.Recipe;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EasyMeals.RecipeEngine.Infrastructure.DependencyInjection;

/// <summary>
///     Dependency injection extensions for the RecipeEngine Infrastructure layer
///     Follows DDD principles and Clean Architecture patterns
/// </summary>
public static class ServiceCollectionExtensions
{
	/// <summary>
	///     Adds recipe-engine infrastructure services including MongoDB data layer
	///     Uses shared MongoDB options and database with recipe-engine-specific collections
	/// </summary>
	/// <param name="services">The service collection</param>
	/// <param name="configuration">The application configuration</param>
	/// <returns>Service collection for chaining</returns>
	public static IServiceCollection AddRecipeEngineInfrastructure(this IServiceCollection services, IConfiguration configuration)
	{
		
		return services;
	}
}