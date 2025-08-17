using EasyMeals.Shared.Data.DbContexts;
using EasyMeals.Shared.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EasyMeals.Shared.Data.Extensions;

/// <summary>
/// Extension methods for configuring EasyMeals shared data services
/// Provides fluent configuration following the Dependency Inversion Principle
/// Supports multiple database providers and flexible configuration options
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds EasyMeals data services with PostgreSQL provider
    /// Optimized for high-performance scenarios and JSON operations
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="connectionString">PostgreSQL connection string</param>
    /// <param name="configureOptions">Optional EF Core configuration</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddEasyMealsDataPostgreSQL(
        this IServiceCollection services,
        string connectionString,
        Action<DbContextOptionsBuilder>? configureOptions = null)
    {
        return services.AddEasyMealsDataCore(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorCodesToAdd: null);
            });

            configureOptions?.Invoke(options);
        });
    }

    /// <summary>
    /// Adds EasyMeals data services with In-Memory provider
    /// Ideal for testing scenarios and development without database setup
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="databaseName">In-memory database name (optional)</param>
    /// <param name="configureOptions">Optional EF Core configuration</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddEasyMealsDataInMemory(
        this IServiceCollection services,
        string? databaseName = null,
        Action<DbContextOptionsBuilder>? configureOptions = null)
    {
        var dbName = databaseName ?? Guid.NewGuid().ToString();

        return services.AddEasyMealsDataCore(options =>
        {
            options.UseInMemoryDatabase(dbName);
            configureOptions?.Invoke(options);
        });
    }

    /// <summary>
    /// Adds EasyMeals data services with custom DbContext configuration
    /// Supports any EF Core provider and custom configuration scenarios
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureDbContext">DbContext configuration action</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddEasyMealsDataCore(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureDbContext)
    {
        // Register DbContext with the specified configuration
        services.AddDbContext<EasyMealsDbContext>(configureDbContext);

        // Register Unit of Work pattern
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Register generic repository pattern
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

        // Register specific repositories
        services.AddScoped<IRecipeRepository, RecipeRepository>();
        services.AddScoped<ICrawlStateRepository, CrawlStateRepository>();

        return services;
    }

    /// <summary>
    /// Ensures the database is created and migrated to the latest version
    /// Essential for deployment scenarios and development setup
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection EnsureEasyMealsDatabase(this IServiceCollection services)
    {
        using var scope = services.BuildServiceProvider().CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EasyMealsDbContext>();

        context.Database.EnsureCreated();

        return services;
    }

    /// <summary>
    /// Adds health checks for EasyMeals database connectivity
    /// Essential for production monitoring and service health validation
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="name">Health check name (optional)</param>
    /// <param name="tags">Health check tags (optional)</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddEasyMealsDataHealthChecks(
        this IServiceCollection services,
        string? name = null,
        string[]? tags = null)
    {
        services.AddHealthChecks()
            .AddDbContextCheck<EasyMealsDbContext>(
                name: name ?? "easymealsdatabase",
                tags: tags ?? ["database", "ready"]);

        return services;
    }
}
