using EasyMeals.RecipeEngine.Domain.Interfaces;
using EasyMeals.RecipeEngine.Infrastructure.Documents.Fingerprint;
using EasyMeals.RecipeEngine.Infrastructure.Documents.SagaState;
using EasyMeals.RecipeEngine.Infrastructure.Repositories;
using EasyMeals.Shared.Data.DependencyInjection;
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
		// Add MongoDB data services using shared options pattern with shared database
		services
			.AddEasyMealsDataWithOptions(options =>
			{
				options.ApplicationName = "EasyMeals.RecipeEngine";
				options.HealthCheckTags = ["recipe-engine", "database", "mongodb"];
				options.EnableDetailedLogging = true;
			})
			.ConfigureEasyMealsRepositories()
			.AddRepository<ISagaStateRepository, SagaStateRepository, SagaStateDocument>().WithSoftDeletableIndexes<SagaStateDocument>()
			.AddRepository<IFingerprintRepository, FingerprintRepository, FingerprintDocument>().WithDefaultIndexes()
			.EnsureDatabaseAsync().GetAwaiter().GetResult();

		return services;
	}
}