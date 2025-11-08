using EasyMeals.RecipeEngine.Application.Interfaces;
using EasyMeals.RecipeEngine.Domain.Interfaces;
using EasyMeals.RecipeEngine.Domain.Repositories;
using EasyMeals.RecipeEngine.Domain.Services;
using EasyMeals.RecipeEngine.Infrastructure.Documents;
using EasyMeals.RecipeEngine.Infrastructure.Documents.Fingerprint;
using EasyMeals.RecipeEngine.Infrastructure.Documents.Recipe;
using EasyMeals.RecipeEngine.Infrastructure.Documents.SagaState;
using EasyMeals.RecipeEngine.Infrastructure.Normalization;
using EasyMeals.RecipeEngine.Infrastructure.Repositories;
using EasyMeals.RecipeEngine.Infrastructure.Services;
using EasyMeals.Shared.Data.DependencyInjection;
using EasyMeals.Shared.Data.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using IRecipeRepository = EasyMeals.RecipeEngine.Domain.Interfaces.IRecipeRepository;

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
			.AddEasyMealsMongoDb(configuration)
			.ConfigureEasyMealsDatabase(builder =>
			{
				builder.IncludeSharedRepositories = true;

				builder
					.AddRepository<ISagaStateRepository, SagaStateRepository, SagaStateDocument>()
					.WithSoftDeletableIndexes<SagaStateDocument>();

				builder
					.AddRepository<IFingerprintRepository, FingerprintRepository, FingerprintDocument>()
					.WithDefaultIndexes();
			})
			.EnsureDatabaseAsync().GetAwaiter().GetResult();

		// Register MongoDB document repositories for Recipe Engine
		services.AddScoped<IMongoRepository<ProviderConfigurationDocument>, MongoRepository<ProviderConfigurationDocument>>();
		services.AddScoped<IMongoRepository<RecipeBatchDocument>, MongoRepository<RecipeBatchDocument>>();
		services.AddScoped<IMongoRepository<IngredientMappingDocument>, MongoRepository<IngredientMappingDocument>>();
		services.AddScoped<IMongoRepository<RecipeFingerprintDocument>, MongoRepository<RecipeFingerprintDocument>>();
		services.AddScoped<IMongoRepository<RecipeDocument>, MongoRepository<RecipeDocument>>();

		// Register domain repositories
		services.AddScoped<IRecipeBatchRepository, RecipeBatchRepository>();
		services.AddScoped<IIngredientMappingRepository, IngredientMappingRepository>();
		services.AddScoped<IRecipeFingerprintRepository, RecipeFingerprintRepository>();
		services.AddScoped<IRecipeRepository, RecipeRepository>();

		// Register domain services
		services.AddScoped<IRecipeDuplicationChecker, RecipeDuplicationChecker>();
		services.AddScoped<IBatchCompletionPolicy, BatchCompletionPolicy>();

		// Register application services
		services.AddScoped<IProviderConfigurationLoader, ProviderConfigurationLoader>();
		services.AddScoped<IIngredientNormalizer, IngredientNormalizationService>();

		// Register hosted services
		services.AddHostedService<ConfigurationHostedService>();

		return services;
	}
}