using EasyMeals.Persistence.Abstractions.Repositories;
using EasyMeals.Persistence.Mongo;
using EasyMeals.Persistence.Mongo.Indexes;
using EasyMeals.Persistence.Mongo.Repositories;
using EasyMeals.RecipeEngine.Infrastructure.Caching;
using EasyMeals.RecipeEngine.Infrastructure.Metrics;
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
		// Configure caching options
		services.Configure<ProviderConfigurationCacheOptions>(
			configuration.GetSection(ProviderConfigurationCacheOptions.SectionName));

		// Add memory cache if not already registered
		services.AddMemoryCache();

		// Register the base repository
		services.AddScoped<ProviderConfigurationRepository>();
		services.AddScoped<IProviderConfigurationRepository>(sp =>
			sp.GetRequiredService<ProviderConfigurationRepository>());

		// Register the caching decorator
		services.AddScoped<ICacheableProviderConfigurationRepository, CachedProviderConfigurationRepository>(sp =>
			new CachedProviderConfigurationRepository(
				sp.GetRequiredService<ProviderConfigurationRepository>(),
				sp.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>(),
				sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ProviderConfigurationCacheOptions>>(),
				sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<CachedProviderConfigurationRepository>>()));

		// Register metrics
		services.AddSingleton<ProviderConfigurationMetrics>();

		return services;
	}

	/// <summary>
	/// Creates MongoDB indexes for provider configurations.
	/// Call this during application startup.
	/// </summary>
	/// <param name="serviceProvider">The service provider.</param>
	/// <param name="ct">Cancellation token.</param>
	public static async Task CreateProviderConfigurationIndexesAsync(
		this IServiceProvider serviceProvider,
		CancellationToken ct = default)
	{
		var context = serviceProvider.GetRequiredService<IMongoContext>();
		await ProviderConfigurationIndexes.CreateIndexesAsync(context, ct);
	}
}