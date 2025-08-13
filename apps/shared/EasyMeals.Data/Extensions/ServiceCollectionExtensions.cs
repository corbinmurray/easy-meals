using EasyMeals.Data.DbContexts;
using EasyMeals.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EasyMeals.Data.Extensions;

/// <summary>
/// Extension methods for configuring EasyMeals data services
/// Makes it easy to switch between different database providers
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds EasyMeals data services with In-Memory database (for development/testing)
    /// </summary>
    public static IServiceCollection AddEasyMealsDataInMemory(
        this IServiceCollection services, 
        string databaseName = "EasyMealsDb")
    {
        services.AddDbContext<EasyMealsDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));

        AddRepositories(services);
        
        return services;
    }

    /// <summary>
    /// Adds EasyMeals data services with PostgreSQL database
    /// </summary>
    public static IServiceCollection AddEasyMealsDataPostgreSQL(
        this IServiceCollection services, 
        string connectionString)
    {
        // Note: This will require adding Npgsql.EntityFrameworkCore.PostgreSQL package
        // services.AddDbContext<EasyMealsDbContext>(options =>
        //     options.UseNpgsql(connectionString));

        // For now, fallback to in-memory until PostgreSQL provider is added
        services.AddDbContext<EasyMealsDbContext>(options =>
            options.UseInMemoryDatabase("EasyMealsDb"));

        AddRepositories(services);
        
        return services;
    }

    /// <summary>
    /// Adds EasyMeals data services with MongoDB via EF Core provider
    /// </summary>
    public static IServiceCollection AddEasyMealsDataMongoDB(
        this IServiceCollection services, 
        string connectionString)
    {
        // Note: This will require adding MongoDB.EntityFrameworkCore package
        // services.AddDbContext<EasyMealsDbContext>(options =>
        //     options.UseMongoDB(connectionString, "EasyMealsDb"));

        // For now, fallback to in-memory until MongoDB provider is added
        services.AddDbContext<EasyMealsDbContext>(options =>
            options.UseInMemoryDatabase("EasyMealsDb"));

        AddRepositories(services);
        
        return services;
    }

    /// <summary>
    /// Adds EasyMeals data services with custom DbContext configuration
    /// </summary>
    public static IServiceCollection AddEasyMealsDataCustom(
        this IServiceCollection services, 
        Action<DbContextOptionsBuilder> configureOptions)
    {
        services.AddDbContext<EasyMealsDbContext>(configureOptions);
        AddRepositories(services);
        
        return services;
    }

    /// <summary>
    /// Registers the repository implementations
    /// </summary>
    private static void AddRepositories(IServiceCollection services)
    {
        services.AddScoped<IRecipeDataRepository, EfCoreRecipeRepository>();
        services.AddScoped<ICrawlStateDataRepository, EfCoreCrawlStateRepository>();
    }

    /// <summary>
    /// Ensures the database is created (for development)
    /// </summary>
    public static async Task EnsureDatabaseCreatedAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EasyMealsDbContext>();
        await context.Database.EnsureCreatedAsync();
    }
}
