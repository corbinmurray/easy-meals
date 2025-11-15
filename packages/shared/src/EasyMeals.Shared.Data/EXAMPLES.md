# EasyMeals MongoDB Repository Examples

This document provides comprehensive examples of how to use the EasyMeals MongoDB repository system with the new robust index configuration.

## Table of Contents

1. [Basic Repository Setup](#basic-repository-setup)
2. [Index Configuration Patterns](#index-configuration-patterns)
3. [Advanced Repository Configuration](#advanced-repository-configuration)
4. [Performance Optimization Examples](#performance-optimization-examples)
5. [Production Setup Examples](#production-setup-examples)

## Basic Repository Setup

### Simple Setup with Default Indexes

```csharp
// Program.cs - Basic setup for development
var services = new ServiceCollection();

// Method 1: Using convenience method (recommended for most cases)
await services.AddEasyMealsRepositoriesWithDefaultsAsync(
    connectionString: "mongodb://localhost:27017",
    databaseName: "easymealdb");

// Method 2: Using fluent API for more control
services.AddEasyMealsDataMongoDB("mongodb://localhost:27017", "easymealdb");
await services.AddEasyMealsRepository()
    .WithDefaultIndexes()
    .EnsureDatabaseAsync();
```

### Setup with Recipe-Specific Optimization

```csharp
// Program.cs - Setup optimized for recipe collections
await services.AddEasyMealsRepositoriesWithDefaultsAsync(
    connectionString: "mongodb://localhost:27017",
    databaseName: "easymealdb",
    includeRecipeIndexes: true);

// Or using fluent API
services.AddEasyMealsDataMongoDB("mongodb://localhost:27017", "easymealdb");
await services.AddEasyMealsRepository()
    .WithDefaultIndexes()                // BaseDocument indexes (Id, CreatedAt, UpdatedAt)
    .WithCompleteRecipeIndexes()         // Base + SoftDelete + Recipe-specific indexes
    .EnsureDatabaseAsync();
```

## Index Configuration Patterns

### Understanding Index Types

The new index configuration separates concerns into logical groups:

#### 1. Base Document Indexes (`WithDefaultIndexes()`)

Applied to **all collections** and includes:

- `idx_base_dates`: Compound index on CreatedAt + UpdatedAt (descending)
- `idx_base_created_at`: Single index on CreatedAt (descending)
- `idx_base_updated_at`: Single index on UpdatedAt (descending)

#### 2. Soft-Deletable Indexes (`WithSoftDeletableIndexes<T>()`)

Applied to collections extending `BaseSoftDeletableDocument`:

- `idx_soft_delete_filter`: IsDeleted + CreatedAt compound index
- `idx_deleted_at`: Sparse index on DeletedAt (only indexed when not null)
- `idx_active_documents`: IsDeleted + UpdatedAt compound index

#### 3. Collection-Specific Indexes

Applied to specific document types for optimal query performance.

### Modular Index Configuration Examples

```csharp
// Example 1: Base indexes only (minimal setup)
await services.AddEasyMealsRepository()
    .WithDefaultIndexes()
    .EnsureDatabaseAsync();

// Example 2: Base + Recipe-specific indexes
await services.AddEasyMealsRepository()
    .WithDefaultIndexes()
    .WithRecipeIndexes()
    .EnsureDatabaseAsync();

// Example 3: Complete recipe optimization
await services.AddEasyMealsRepository()
    .WithCompleteRecipeIndexes()  // Includes base + soft-delete + recipe-specific
    .EnsureDatabaseAsync();

// Example 4: Custom soft-deletable document
await services.AddEasyMealsRepository()
    .AddRepository<CustomDocument>()
    .WithDefaultIndexes()
    .WithSoftDeletableIndexes<CustomDocument>()
    .EnsureDatabaseAsync();
```

## Advanced Repository Configuration

### Custom Repository with Specific Permissions

```csharp
// Read-only repository for analytics
await services.AddEasyMealsRepository()
    .AddRepository<RecipeDocument>(RepositoryPermissions.ReadOnly)
    .WithDefaultIndexes()
    .WithRecipeIndexes()
    .EnsureDatabaseAsync();

// Full permissions repository for admin operations
await services.AddEasyMealsRepository()
    .AddRepository<RecipeDocument>(RepositoryPermissions.ReadWrite)
    .AddSharedRepository<IRecipeRepository>()
    .WithCompleteRecipeIndexes()
    .EnsureDatabaseAsync();
```

### Multiple Document Types with Targeted Indexing

```csharp
public class UserDocument : BaseSoftDeletableDocument
{
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class OrderDocument : BaseDocument
{
    public string UserId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

// Configuration
await services.AddEasyMealsRepository()
    .AddRepository<UserDocument>()
    .AddRepository<OrderDocument>()
    .AddRepository<RecipeDocument>()
    .WithDefaultIndexes()                           // Applied to all collections
    .WithSoftDeletableIndexes<UserDocument>()       // Only for UserDocument
    .WithRecipeIndexes()                            // Only for RecipeDocument
    .WithCustomIndexes<OrderDocument>(async collection =>
    {
        // Custom indexes for OrderDocument
        var orderIndexes = new[]
        {
            new CreateIndexModel<OrderDocument>(
                Builders<OrderDocument>.IndexKeys.Ascending(o => o.UserId),
                new CreateIndexOptions { Name = "idx_order_user_id", Background = true }
            ),
            new CreateIndexModel<OrderDocument>(
                Builders<OrderDocument>.IndexKeys
                    .Descending(o => o.Amount)
                    .Descending(o => o.CreatedAt),
                new CreateIndexOptions { Name = "idx_order_amount_date", Background = true }
            )
        };
        await collection.Indexes.CreateManyAsync(orderIndexes);
    })
    .EnsureDatabaseAsync();
```

### Advanced Index Builder Usage

```csharp
// Using the fluent index builder for complex scenarios
await services.AddEasyMealsRepository()
    .AddRepository<RecipeDocument>()
    .WithDefaultIndexes()
    .ConfigureIndexes()
        .CreateCompoundIndex<RecipeDocument>(
            Builders<RecipeDocument>.IndexKeys
                .Ascending(r => r.Cuisine)
                .Descending(r => r.Rating)
                .Ascending(r => r.PrepTimeMinutes),
            new CreateIndexOptions
            {
                Name = "idx_cuisine_rating_preptime",
                Background = true
            })
        .CreateTextIndex<RecipeDocument>(
            Builders<RecipeDocument>.IndexKeys
                .Text(r => r.Title)
                .Text(r => r.Description)
                .Text(r => r.Tags),
            new CreateIndexOptions
            {
                Name = "idx_comprehensive_search",
                Background = true
            })
        .BuildIndexes()
    .EnsureDatabaseAsync();
```

## Performance Optimization Examples

### Recipe Search and Filtering Optimization

```csharp
// Setup optimized for recipe search application
await services.AddEasyMealsRepositoriesWithFullOptimizationAsync(
    connectionString: Environment.GetEnvironmentVariable("MONGODB_CONNECTION")!,
    databaseName: "easymeal_production");

// Usage in service
public class RecipeSearchService
{
    private readonly IReadOnlyMongoRepository<RecipeDocument> _recipeRepository;

    public RecipeSearchService(IReadOnlyMongoRepository<RecipeDocument> recipeRepository)
    {
        _recipeRepository = recipeRepository;
    }

    // Optimized by idx_recipe_text_search
    public async Task<IEnumerable<RecipeDocument>> SearchRecipesAsync(string searchTerm)
    {
        var filter = Builders<RecipeDocument>.Filter.Text(searchTerm);
        return await _recipeRepository.FindAsync(filter);
    }

    // Optimized by idx_recipe_cuisine and idx_recipe_time_constraints
    public async Task<IEnumerable<RecipeDocument>> FindQuickMealsByCuisineAsync(
        string cuisine, int maxMinutes = 30)
    {
        var filter = Builders<RecipeDocument>.Filter.And(
            Builders<RecipeDocument>.Filter.Eq(r => r.Cuisine, cuisine),
            Builders<RecipeDocument>.Filter.Lte(r => r.PrepTimeMinutes + r.CookTimeMinutes, maxMinutes)
        );

        return await _recipeRepository.FindAsync(filter);
    }

    // Optimized by idx_recipe_rating_popularity
    public async Task<IEnumerable<RecipeDocument>> GetTopRatedRecipesAsync(int limit = 10)
    {
        return await _recipeRepository.GetPagedAsync(
            pageNumber: 1,
            pageSize: limit,
            sortBy: r => r.Rating,
            ascending: false);
    }
}
```

### Audit and Analytics Optimization

```csharp
// Setup for analytics and audit queries
public class AnalyticsService
{
    private readonly IReadOnlyMongoRepository<RecipeDocument> _recipeRepository;

    // Optimized by idx_base_created_at and idx_base_dates
    public async Task<long> GetRecipesCreatedThisMonthAsync()
    {
        var startOfMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        var filter = Builders<RecipeDocument>.Filter.Gte(r => r.CreatedAt, startOfMonth);

        return await _recipeRepository.CountAsync(filter);
    }

    // Optimized by idx_base_updated_at
    public async Task<IEnumerable<RecipeDocument>> GetRecentlyUpdatedRecipesAsync(int days = 7)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-days);
        var filter = Builders<RecipeDocument>.Filter.Gte(r => r.UpdatedAt, cutoffDate);

        return await _recipeRepository.FindAsync(filter);
    }

    // Optimized by idx_recipe_source_provider
    public async Task<Dictionary<string, long>> GetRecipeCountByProviderAsync()
    {
        var pipeline = new[]
        {
            new BsonDocument("$group", new BsonDocument
            {
                {"_id", "$providerName"},
                {"count", new BsonDocument("$sum", 1)}
            })
        };

        // This would require additional aggregation support in the repository
        // For now, we can group in memory for smaller datasets
        var allRecipes = await _recipeRepository.GetAllAsync();
        return allRecipes
            .GroupBy(r => r.ProviderName)
            .ToDictionary(g => g.Key, g => (long)g.Count());
    }
}
```

## Production Setup Examples

### ASP.NET Core Integration

```csharp
// Program.cs for ASP.NET Core application
var builder = WebApplication.CreateBuilder(args);

// MongoDB configuration from appsettings
var connectionString = builder.Configuration.GetConnectionString("MongoDB")
    ?? throw new InvalidOperationException("MongoDB connection string not found");
var databaseName = builder.Configuration["MongoDB:DatabaseName"]
    ?? throw new InvalidOperationException("MongoDB database name not found");

// Production setup with full optimization
await builder.Services.AddEasyMealsRepositoriesWithFullOptimizationAsync(
    connectionString, databaseName);

// Add health checks for monitoring
builder.Services.AddHealthChecks();

var app = builder.Build();

// Health check endpoint
app.MapHealthChecks("/health");

app.Run();
```

### Background Service with Repository

```csharp
// Background service for recipe processing
public class RecipeProcessingService : BackgroundService
{
    private readonly IMongoRepository<RecipeDocument> _recipeRepository;
    private readonly ILogger<RecipeProcessingService> _logger;

    public RecipeProcessingService(
        IMongoRepository<RecipeDocument> recipeRepository,
        ILogger<RecipeProcessingService> logger)
    {
        _recipeRepository = recipeRepository;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Process recipes needing updates (optimized by idx_base_updated_at)
                var staleRecipes = await _recipeRepository.FindAsync(
                    Builders<RecipeDocument>.Filter.Lt(
                        r => r.UpdatedAt,
                        DateTime.UtcNow.AddDays(-30)));

                foreach (var recipe in staleRecipes)
                {
                    // Process recipe updates
                    recipe.UpdatedAt = DateTime.UtcNow;
                    await _recipeRepository.UpdateOneAsync(recipe);
                }

                _logger.LogInformation("Processed {Count} stale recipes", staleRecipes.Count());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing recipes");
            }

            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}

// Registration
await builder.Services.AddEasyMealsRepositoriesWithFullOptimizationAsync(
    connectionString, databaseName);
builder.Services.AddHostedService<RecipeProcessingService>();
```

### Testing Configuration

```csharp
// Test setup with in-memory or test database
public class RecipeRepositoryTests : IAsyncLifetime
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IMongoRepository<RecipeDocument> _repository;

    public RecipeRepositoryTests()
    {
        var services = new ServiceCollection();

        // Use test database
        var testConnectionString = "mongodb://localhost:27017";
        var testDatabaseName = $"test_easymeal_{Guid.NewGuid():N}";

        services.AddEasyMealsDataMongoDB(testConnectionString, testDatabaseName);
        services.AddEasyMealsRepository()
            .AddRepository<RecipeDocument>()
            .WithCompleteRecipeIndexes();

        _serviceProvider = services.BuildServiceProvider();
        _repository = _serviceProvider.GetRequiredService<IMongoRepository<RecipeDocument>>();
    }

    public async Task InitializeAsync()
    {
        // Ensure database and indexes are created
        var builder = _serviceProvider.GetRequiredService<EasyMealsRepositoryBuilder>();
        await builder.EnsureDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        // Clean up test database
        var database = _serviceProvider.GetRequiredService<IMongoDatabase>();
        await database.Client.DropDatabaseAsync(database.DatabaseNamespace.DatabaseName);
        _serviceProvider.Dispose();
    }

    [Fact]
    public async Task FindRecipesByText_WithOptimizedIndex_ReturnsResults()
    {
        // Test that leverages idx_recipe_text_search
        var recipe = new RecipeDocument
        {
            Title = "Delicious Pasta",
            Description = "A wonderful Italian dish",
            SourceUrl = "https://example.com/pasta"
        };

        await _repository.InsertOneAsync(recipe);

        var results = await _repository.FindAsync(
            Builders<RecipeDocument>.Filter.Text("pasta"));

        Assert.Single(results);
        Assert.Equal("Delicious Pasta", results.First().Title);
    }
}
```

## Index Management and Monitoring

### Index Statistics and Performance Monitoring

```csharp
// Service for monitoring index performance
public class IndexMonitoringService
{
    private readonly IMongoDatabase _database;

    public IndexMonitoringService(IMongoDatabase database)
    {
        _database = database;
    }

    public async Task<Dictionary<string, BsonDocument>> GetIndexStatsAsync()
    {
        return await MongoIndexConfiguration.GetIndexStatsAsync(_database);
    }

    public async Task<List<BsonDocument>> GetRecipeIndexesAsync()
    {
        return await MongoIndexConfiguration.GetCollectionIndexesAsync<RecipeDocument>(_database);
    }

    public async Task LogIndexUsageAsync(ILogger logger)
    {
        var stats = await GetIndexStatsAsync();

        foreach (var (collectionName, collectionStats) in stats)
        {
            logger.LogInformation("Collection {Collection}: {Stats}",
                collectionName, collectionStats.ToJson());
        }
    }
}
```

### Index Cleanup and Migration

```csharp
// Service for managing index lifecycle
public class IndexMigrationService
{
    private readonly IMongoDatabase _database;

    public IndexMigrationService(IMongoDatabase database)
    {
        _database = database;
    }

    public async Task MigrateToNewIndexStrategyAsync()
    {
        // Drop old indexes
        await MongoIndexConfiguration.DropBaseDocumentIndexesAsync(_database);
        await MongoIndexConfiguration.DropRecipeSpecificIndexesAsync(_database);

        // Create new optimized indexes
        await MongoIndexConfiguration.CreateBaseDocumentIndexesAsync(_database);
        await MongoIndexConfiguration.CreateCompleteRecipeIndexesAsync(_database);
    }

    public async Task CleanupUnusedIndexesAsync()
    {
        // Implementation would analyze index usage stats and drop unused indexes
        var stats = await MongoIndexConfiguration.GetIndexStatsAsync(_database);

        // Analyze and cleanup based on usage patterns
        // This is a placeholder for more sophisticated cleanup logic
    }
}
```

This comprehensive example system demonstrates the flexibility and power of the new robust index configuration system, allowing for optimal MongoDB
performance across different application scenarios.
