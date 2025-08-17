using EasyMeals.Shared.Data.DbContexts;
using EasyMeals.Shared.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace EasyMeals.Shared.Data.Extensions;

/// <summary>
/// Base class for application-specific DbContext extensions
/// Follows the Template Method pattern to allow applications to extend the shared context
/// Supports the Open/Closed Principle - applications can extend without modifying shared code
/// </summary>
/// <typeparam name="TContext">The derived DbContext type</typeparam>
public abstract class EasyMealsAppDbContext<TContext>(DbContextOptions<TContext> options) : EasyMealsDbContext(CreateSharedOptions(options))
    where TContext : EasyMealsAppDbContext<TContext>
{
    /// <summary>
    /// Converts app-specific options to shared context options
    /// Ensures compatibility between shared and application-specific contexts
    /// </summary>
    /// <param name="options">Application-specific options</param>
    /// <returns>Shared context options</returns>
    private static DbContextOptions<EasyMealsDbContext> CreateSharedOptions(DbContextOptions<TContext> options)
    {
        var builder = new DbContextOptionsBuilder<EasyMealsDbContext>();

        // Copy all configurations from the app-specific options
        foreach (IDbContextOptionsExtension extension in options.Extensions)
        {
            ((IDbContextOptionsBuilderInfrastructure)builder).AddOrUpdateExtension(extension);
        }

        return builder.Options;
    }

    /// <summary>
    /// Override to configure application-specific entities
    /// Called by the shared OnModelCreating method via the partial method pattern
    /// </summary>
    /// <param name="modelBuilder">The model builder instance</param>
    protected abstract void ConfigureApplicationEntities(ModelBuilder modelBuilder);

    /// <summary>
    /// Implementation of the partial method from the shared context
    /// Delegates to the abstract method that applications must implement
    /// </summary>
    /// <param name="modelBuilder">The model builder instance</param>
    protected override void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        ConfigureApplicationEntities(modelBuilder);
    }
}

/// <summary>
/// Extension methods for application-specific data service registration
/// Provides patterns for applications to extend the shared data infrastructure
/// </summary>
public static class ApplicationDataExtensions
{
    /// <summary>
    /// Adds application-specific data services extending the shared infrastructure
    /// Template method for applications to follow consistent configuration patterns
    /// </summary>
    /// <typeparam name="TContext">The application-specific DbContext type</typeparam>
    /// <param name="services">Service collection</param>
    /// <param name="configureDbContext">DbContext configuration action</param>
    /// <param name="configureRepositories">Optional repository configuration action</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddApplicationData<TContext>(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureDbContext,
        Action<IServiceCollection>? configureRepositories = null)
        where TContext : EasyMealsAppDbContext<TContext>
    {
        // Register the application-specific DbContext
        services.AddDbContext<TContext>(configureDbContext);

        // Register the application context as the shared context interface
        services.AddScoped<EasyMealsDbContext>(provider =>
            provider.GetRequiredService<TContext>());

        // Register shared repositories and unit of work
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IRecipeRepository, RecipeRepository>();
        services.AddScoped<ICrawlStateRepository, CrawlStateRepository>();

        // Allow applications to register additional repositories
        configureRepositories?.Invoke(services);

        return services;
    }
}

/// <summary>
/// Example of how applications should extend the shared data context
/// Demonstrates the pattern applications should follow for adding their own entities
/// </summary>
/// <example>
/// <code>
/// // In your application project:
/// public class MyAppDbContext : EasyMealsAppDbContext<MyAppDbContext>
/// {
///     public MyAppDbContext(DbContextOptions<MyAppDbContext> options) : base(options) { }
///     
///     // Add your application-specific DbSets
///     public DbSet<MyEntity> MyEntities { get; set; } = null!;
///     
///     protected override void ConfigureApplicationEntities(ModelBuilder modelBuilder)
///     {
///         // Configure your application-specific entities
///         modelBuilder.Entity<MyEntity>().ToTable("MyEntities");
///         // Add configurations, indexes, relationships, etc.
///     }
/// }
/// 
/// // In your Program.cs or Startup.cs:
/// services.AddApplicationData<MyAppDbContext>(options =>
/// {
///     options.UseSqlServer(connectionString);
/// }, services =>
/// {
///     // Register your application-specific repositories
///     services.AddScoped<IMyEntityRepository, MyEntityRepository>();
/// });
/// </code>
/// </example>
public static class ApplicationDataPatternExample
{
    // This class exists only for documentation and examples
    // Applications should follow this pattern in their own projects
}
