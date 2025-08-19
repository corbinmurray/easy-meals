using MongoDB.Driver;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using EasyMeals.Shared.Data.Repositories;
using EasyMeals.Shared.Data.Documents;

namespace EasyMeals.Shared.Data.Extensions;

/// <summary>
/// Extension methods for configuring EasyMeals MongoDB data services
/// Provides fluent configuration following the Dependency Inversion Principle
/// Supports MongoDB client configuration and flexible connection options
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds EasyMeals data services with MongoDB using connection string
    /// Standard configuration for production and development environments
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="connectionString">MongoDB connection string</param>
    /// <param name="databaseName">Database name (optional, defaults to "easymealsprod")</param>
    /// <param name="configureClient">Optional MongoDB client configuration</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddEasyMealsDataMongoDB(
        this IServiceCollection services,
        string connectionString,
        string? databaseName = null,
        Action<MongoClientSettings>? configureClient = null)
    {
        var dbName = databaseName ?? "easymealsprod";

        return services.AddEasyMealsDataCore(clientSettings =>
        {
            // Parse connection string and apply custom settings
            var settings = MongoClientSettings.FromConnectionString(connectionString);

            // Apply custom configuration if provided
            configureClient?.Invoke(settings);

            return settings;
        }, dbName);
    }

    /// <summary>
    /// Adds EasyMeals data services with MongoDB using custom client settings
    /// Advanced configuration for specialized deployment scenarios
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="clientSettings">MongoDB client settings</param>
    /// <param name="databaseName">Database name</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddEasyMealsDataMongoDB(
        this IServiceCollection services,
        MongoClientSettings clientSettings,
        string databaseName)
    {
        return services.AddEasyMealsDataCore(_ => clientSettings, databaseName);
    }

    /// <summary>
    /// Adds EasyMeals data services with in-memory MongoDB for testing
    /// Uses Testcontainers or local MongoDB instance for development
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="databaseName">Database name (optional, defaults to test database)</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddEasyMealsDataInMemory(
        this IServiceCollection services,
        string? databaseName = null)
    {
        var dbName = databaseName ?? $"easymealstests_{Guid.NewGuid():N}";

        return services.AddEasyMealsDataCore(clientSettings =>
        {
            // Use local MongoDB instance for testing
            var settings = new MongoClientSettings
            {
                Server = new MongoServerAddress("localhost", 27017),
                ConnectTimeout = TimeSpan.FromSeconds(5),
                ServerSelectionTimeout = TimeSpan.FromSeconds(5)
            };

            return settings;
        }, dbName);
    }

    /// <summary>
    /// Core configuration method for MongoDB data services
    /// Registers all necessary services and repository implementations
    /// </summary>
    private static IServiceCollection AddEasyMealsDataCore(
        this IServiceCollection services,
        Func<MongoClientSettings, MongoClientSettings> configureClientSettings,
        string databaseName)
    {
        // Register MongoDB client as singleton
        services.AddSingleton<IMongoClient>(serviceProvider =>
        {
            var settings = configureClientSettings(new MongoClientSettings());
            return new MongoClient(settings);
        });

        // Register MongoDB database as scoped
        services.AddScoped<IMongoDatabase>(serviceProvider =>
        {
            var client = serviceProvider.GetRequiredService<IMongoClient>();
            return client.GetDatabase(databaseName);
        });

        // Register Unit of Work pattern for MongoDB
        services.AddScoped<IUnitOfWork, MongoUnitOfWork>();
        services.AddScoped<IMongoUnitOfWork, MongoUnitOfWork>();

        // Register generic repository pattern
        services.AddScoped(typeof(IRepository<>), typeof(MongoRepository<>));
        services.AddScoped(typeof(IMongoRepository<>), typeof(MongoRepository<>));

        // Register specific repositories
        services.AddScoped<IRecipeRepository, RecipeRepository>();
        services.AddScoped<ICrawlStateRepository, CrawlStateRepository>();

        return services;
    }

    /// <summary>
    /// Ensures the MongoDB database exists and creates indexes
    /// Essential for deployment scenarios and development setup
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <returns>Service collection for chaining</returns>
    public static async Task<IServiceCollection> EnsureEasyMealsDatabaseAsync(this IServiceCollection services)
    {
        using var scope = services.BuildServiceProvider(validateScopes: false).CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<IMongoDatabase>();

        // The database will be created automatically when first accessed
        // Create essential indexes for optimal performance
        await CreateIndexesAsync(database);

        return services;
    }

    /// <summary>
    /// Adds health checks for EasyMeals MongoDB connectivity
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
            .AddCheck<MongoDbHealthCheck>(
                name: name ?? "easymealsmongodb",
                tags: tags ?? ["database", "mongodb", "ready"]);

        return services;
    }

    /// <summary>
    /// Creates essential MongoDB indexes for optimal query performance
    /// Implements indexing strategy from the migration plan
    /// </summary>
    private static async Task CreateIndexesAsync(IMongoDatabase database)
    {
        // Create indexes for recipes collection
        var recipesCollection = database.GetCollection<RecipeDocument>("recipes");
        var recipeIndexes = new[]
        {
            // Text search index for title, description, and ingredients
            new CreateIndexModel<RecipeDocument>(
                Builders<RecipeDocument>.IndexKeys.Text(r => r.Title).Text(r => r.Description),
                new CreateIndexOptions { Name = "text_search_index" }
            ),
            
            // Compound index for source provider and active status
            new CreateIndexModel<RecipeDocument>(
                Builders<RecipeDocument>.IndexKeys
                    .Ascending(r => r.SourceProvider)
                    .Ascending(r => r.IsActive)
                    .Ascending(r => r.IsDeleted),
                new CreateIndexOptions { Name = "source_provider_active_index" }
            ),
            
            // Index for source URL (unique constraint for duplicate prevention)
            new CreateIndexModel<RecipeDocument>(
                Builders<RecipeDocument>.IndexKeys.Ascending(r => r.SourceUrl),
                new CreateIndexOptions { Name = "source_url_index", Unique = true }
            ),
            
            // Index for tags (multikey index for array queries)
            new CreateIndexModel<RecipeDocument>(
                Builders<RecipeDocument>.IndexKeys.Ascending(r => r.Tags),
                new CreateIndexOptions { Name = "tags_index" }
            ),
            
            // Index for cuisine type
            new CreateIndexModel<RecipeDocument>(
                Builders<RecipeDocument>.IndexKeys.Ascending(r => r.Cuisine),
                new CreateIndexOptions { Name = "cuisine_index" }
            ),
            
            // Compound index for time constraints
            new CreateIndexModel<RecipeDocument>(
                Builders<RecipeDocument>.IndexKeys
                    .Ascending(r => r.PrepTimeMinutes)
                    .Ascending(r => r.CookTimeMinutes),
                new CreateIndexOptions { Name = "time_constraints_index" }
            )
        };

        await recipesCollection.Indexes.CreateManyAsync(recipeIndexes);

        // Create indexes for crawl states collection
        var crawlStatesCollection = database.GetCollection<CrawlStateDocument>("crawlstates");
        var crawlStateIndexes = new[]
        {
            // Unique index for source provider
            new CreateIndexModel<CrawlStateDocument>(
                Builders<CrawlStateDocument>.IndexKeys.Ascending(c => c.SourceProvider),
                new CreateIndexOptions { Name = "source_provider_unique_index", Unique = true }
            ),
            
            // Index for active status and priority
            new CreateIndexModel<CrawlStateDocument>(
                Builders<CrawlStateDocument>.IndexKeys
                    .Ascending(c => c.IsActive)
                    .Descending(c => c.Priority),
                new CreateIndexOptions { Name = "active_priority_index" }
            ),
            
            // Index for scheduled crawls
            new CreateIndexModel<CrawlStateDocument>(
                Builders<CrawlStateDocument>.IndexKeys.Ascending(c => c.NextScheduledCrawl),
                new CreateIndexOptions { Name = "scheduled_crawl_index" }
            )
        };

        await crawlStatesCollection.Indexes.CreateManyAsync(crawlStateIndexes);
    }
}

/// <summary>
/// MongoDB health check implementation
/// Verifies database connectivity and basic operations
/// </summary>
public class MongoDbHealthCheck : IHealthCheck
{
    private readonly IMongoDatabase _database;

    public MongoDbHealthCheck(IMongoDatabase database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Perform a simple ping operation
            await _database.RunCommandAsync<MongoDB.Bson.BsonDocument>(
                new MongoDB.Bson.BsonDocument("ping", 1),
                cancellationToken: cancellationToken);

            return HealthCheckResult.Healthy("MongoDB connection is healthy");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("MongoDB connection failed", ex);
        }
    }
}
