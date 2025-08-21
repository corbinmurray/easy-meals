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
        services.AddEasyMealsDataWithOptions(options =>
        {
            // RecipeEngine-specific configuration overrides (using shared database)
            options.ApplicationName = "EasyMeals.RecipeEngine";
            options.ReadPreference = "SecondaryPreferred"; // RecipeEngine can use secondary reads
            options.MaxConnectionPoolSize = 50; // Optimized for recipe-engine workload
            options.SocketTimeoutSeconds = 120; // Longer timeout for batch operations
            options.HealthCheckTags = ["recipe-engine", "database", "mongodb"];
            options.EnableDetailedLogging = true; // Enable for development/troubleshooting
        });

        // TODO: Add repositories

        return services;
    }
}