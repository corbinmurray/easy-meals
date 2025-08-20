using EasyMeals.Shared.Data.Configuration;
using EasyMeals.Shared.Data.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EasyMeals.Api.Infrastructure.DependencyInjection;

/// <summary>
///     Dependency injection extensions for the API Infrastructure layer
///     Follows DDD principles and Clean Architecture patterns
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Adds API infrastructure services including MongoDB data layer
    ///     Uses shared MongoDB options and database for consistent data access
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The application configuration</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddApiInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Add MongoDB data services using shared options pattern with shared database
        services.AddEasyMealsDataWithOptions(options =>
        {
            // API-specific configuration overrides (using shared database)
            options.ApplicationName = "EasyMeals.Api";
            options.ReadPreference = "Primary"; // API needs consistent reads
            options.MaxConnectionPoolSize = 100; // Higher for API workload
            options.SocketTimeoutSeconds = 30; // Shorter timeout for API responses
            options.HealthCheckTags = ["api", "database", "mongodb"];
            options.EnableDetailedLogging = false; // Disable for production API
            options.MinConnectionPoolSize = 5; // Maintain baseline connections
        })
        // Add shared repositories that the API needs (recipes, etc.)
        .AddSharedRepositories();
        // API-specific repositories can be added here as needed
        // .AddApplicationRepositories(services => { });

        return services;
    }
}
