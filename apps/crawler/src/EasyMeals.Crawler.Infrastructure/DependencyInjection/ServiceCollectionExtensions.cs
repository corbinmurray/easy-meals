using EasyMeals.Shared.Data.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EasyMeals.Crawler.Infrastructure.DependencyInjection;

/// <summary>
///     Dependency injection extensions for the Crawler Infrastructure layer
///     Follows DDD principles and Clean Architecture patterns
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Adds crawler infrastructure services including MongoDB data layer
    ///     Uses shared MongoDB options and database with crawler-specific collections
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The application configuration</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddCrawlerInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Add MongoDB data services using shared options pattern with shared database
        services.AddEasyMealsDataWithOptions(options =>
        {
            // Crawler-specific configuration overrides (using shared database)
            options.ApplicationName = "EasyMeals.Crawler";
            options.ReadPreference = "SecondaryPreferred"; // Crawler can use secondary reads
            options.MaxConnectionPoolSize = 50; // Optimized for crawler workload
            options.SocketTimeoutSeconds = 120; // Longer timeout for batch operations
            options.HealthCheckTags = ["crawler", "database", "mongodb"];
            options.EnableDetailedLogging = true; // Enable for development/troubleshooting
        });
        
        // TODO: Add repositories

        return services;
    }
}