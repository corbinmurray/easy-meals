# EasyMeals.Shared.Data - MongoDB Integration

A comprehensive MongoDB data access library for the EasyMeals application, providing type-safe document models, repository patterns, and optimized
indexing strategies.

## Overview

This library implements the migration from Entity Framework Core to MongoDB, maintaining backward compatibility while leveraging MongoDB's
document-oriented features for improved performance and scalability.

### Key Features

- **Document-based Models**: Native MongoDB document structures with embedded relationships
- **Repository Pattern**: Generic and specific repositories following Domain-Driven Design principles
- **Unit of Work**: Transaction support for data consistency across operations
- **Optimized Indexing**: Comprehensive indexing strategy for high-performance queries
- **Type Safety**: Strongly-typed queries and operations using C# expressions
- **Health Monitoring**: Built-in health checks for production monitoring

## Quick Start

### 1. Installation

Add the package reference to your project:

```xml
<PackageReference Include="EasyMeals.Shared.Data" Version="1.0.0" />
```

### 2. Basic Configuration

#### In your `Program.cs` or `Startup.cs`:

```csharp
using EasyMeals.Shared.Data.Extensions;

// Basic configuration with connection string
services.AddEasyMealsDataMongoDB(
    connectionString: "mongodb://localhost:27017",
    databaseName: "easymealsprod"
);

// Add health checks (optional but recommended)
services.AddEasyMealsDataHealthChecks();

// Ensure database and indexes are created (run once during deployment)
await services.EnsureEasyMealsDatabaseAsync();
```

#### Advanced Configuration:

```csharp
// Custom MongoDB client settings
var clientSettings = new MongoClientSettings
{
    Server = new MongoServerAddress("mongodb.example.com", 27017),
    ConnectTimeout = TimeSpan.FromSeconds(30),
    ServerSelectionTimeout = TimeSpan.FromSeconds(30),
    Credential = MongoCredential.CreateCredential("mydb", "username", "password")
};

services.AddEasyMealsDataMongoDB(clientSettings, "easymealsprod");
```

#### For Testing:

```csharp
// In-memory database for unit tests
services.AddEasyMealsDataInMemory("test_database");
```

### 3. Using Repositories

#### Recipe Repository:

```csharp
public class RecipeService
{
    private readonly IRecipeRepository _recipeRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RecipeService(IRecipeRepository recipeRepository, IUnitOfWork unitOfWork)
    {
        _recipeRepository = recipeRepository;
        _unitOfWork = unitOfWork;
    }

    // Create a new recipe
    public async Task<string> CreateRecipeAsync(RecipeDocument recipe)
    {
        var recipeId = await _recipeRepository.AddAsync(recipe);
        await _unitOfWork.CommitAsync();
        return recipeId;
    }

    // Find recipes by title
    public async Task<IEnumerable<RecipeDocument>> SearchRecipesAsync(string searchTerm)
    {
        return await _recipeRepository.SearchByTextAsync(searchTerm);
    }

    // Get active recipes with pagination
    public async Task<PagedResult<RecipeDocument>> GetActiveRecipesAsync(int pageNumber, int pageSize)
    {
        return await _recipeRepository.GetPagedAsync(
            filter: r => r.IsActive && !r.IsDeleted,
            pageNumber: pageNumber,
            pageSize: pageSize,
            orderBy: r => r.CreatedAt,
            ascending: false
        );
    }

    // Update recipe with optimistic concurrency
    public async Task UpdateRecipeAsync(RecipeDocument recipe)
    {
        await _recipeRepository.UpdateAsync(recipe);
        await _unitOfWork.CommitAsync();
    }
}
```

#### Crawl State Repository:

```csharp
public class CrawlerService
{
    private readonly ICrawlStateRepository _crawlStateRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CrawlerService(ICrawlStateRepository crawlStateRepository, IUnitOfWork unitOfWork)
    {
        _crawlStateRepository = crawlStateRepository;
        _unitOfWork = unitOfWork;
    }

    // Claim next crawl job
    public async Task<CrawlStateDocument?> ClaimNextCrawlJobAsync(string sessionId)
    {
        var crawlState = await _crawlStateRepository.ClaimNextAvailableAsync(sessionId);
        if (crawlState != null)
        {
            await _unitOfWork.CommitAsync();
        }
        return crawlState;
    }

    // Update crawl progress
    public async Task UpdateCrawlProgressAsync(string sourceProvider, List<string> newUrls)
    {
        await _crawlStateRepository.AddPendingUrlsAsync(sourceProvider, newUrls);
        await _unitOfWork.CommitAsync();
    }
}
```

## Document Models

### RecipeDocument

The main recipe aggregate containing all recipe information:

```csharp
[BsonCollection("recipes")]
public class RecipeDocument : BaseDocument
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public string SourceProvider { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public int? PrepTimeMinutes { get; set; }
    public int? CookTimeMinutes { get; set; }
    public int? Servings { get; set; }
    public string? Difficulty { get; set; }
    public string? Cuisine { get; set; }
    public double? Rating { get; set; }
    public int? ReviewCount { get; set; }
    public List<string> Tags { get; set; } = new();

    // Embedded documents
    public List<IngredientDocument> Ingredients { get; set; } = new();
    public List<InstructionDocument> Instructions { get; set; } = new();
    public NutritionalInfoDocument? NutritionInfo { get; set; }

    // Computed properties
    public int? TotalTimeMinutes => (PrepTimeMinutes ?? 0) + (CookTimeMinutes ?? 0);
    public bool HasNutritionInfo => NutritionInfo != null;
}
```

### CrawlStateDocument

Manages distributed crawling state:

```csharp
[BsonCollection("crawlstates")]
public class CrawlStateDocument : BaseDocument
{
    public string SourceProvider { get; set; } = string.Empty;
    public List<string> PendingUrls { get; set; } = new();
    public List<string> ProcessedUrls { get; set; } = new();
    public DateTime? LastCrawlTime { get; set; }
    public DateTime? NextScheduledCrawl { get; set; }
    public int Priority { get; set; } = 1;
    public string? CurrentSessionId { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}
```

## Querying and Filtering

### Text Search

MongoDB's text search capabilities are leveraged for recipe discovery:

```csharp
// Search across title and description
var results = await _recipeRepository.SearchByTextAsync("chicken pasta");

// Complex filtering
var italianRecipes = await _recipeRepository.GetManyAsync(
    filter: r => r.Cuisine == "Italian" &&
                 r.Tags.Contains("quick") &&
                 r.PrepTimeMinutes <= 30
);
```

### Advanced Queries

```csharp
// Aggregation pipeline example
var popularRecipesByCuisine = await _recipeRepository.Collection
    .Aggregate()
    .Match(r => r.IsActive && r.Rating >= 4.0)
    .Group(r => r.Cuisine, g => new
    {
        Cuisine = g.Key,
        Count = g.Count(),
        AvgRating = g.Average(r => r.Rating)
    })
    .SortByDescending(g => g.Count)
    .ToListAsync();
```

## Index Strategy

The library includes a comprehensive indexing strategy optimized for common query patterns:

### Recipe Indexes

- **Text Search**: Title and description for full-text search
- **Source Provider**: Compound index with active status for crawler queries
- **Unique URLs**: Prevents duplicate recipes from same source
- **Tags**: Multikey index for efficient tag-based filtering
- **Time Constraints**: Compound index for prep/cook time filtering
- **Nutritional Criteria**: Sparse index for nutrition-based queries

### Crawl State Indexes

- **Source Provider**: Unique constraint ensuring one state per provider
- **Priority Queue**: Optimized for job scheduling and claiming
- **Session Management**: Efficient session-based locking

### Manual Index Management

```csharp
// Create all indexes (usually done during deployment)
await MongoIndexConfiguration.CreateAllIndexesAsync(database);

// Create specific indexes
await MongoIndexConfiguration.CreateRecipeIndexesAsync(database);
await MongoIndexConfiguration.CreateCrawlStateIndexesAsync(database);

// Drop indexes (for migrations)
await MongoIndexConfiguration.DropAllCustomIndexesAsync(database);

// Monitor index performance
var stats = await MongoIndexConfiguration.GetIndexStatsAsync(database);
```

## Transaction Support

The Unit of Work pattern provides transaction support:

```csharp
public async Task ProcessRecipeBatchAsync(List<RecipeDocument> recipes)
{
    using var session = await _unitOfWork.StartSessionAsync();
    try
    {
        foreach (var recipe in recipes)
        {
            await _recipeRepository.AddAsync(recipe);
        }

        // Update crawl state
        await _crawlStateRepository.MarkUrlsProcessedAsync(
            "example-provider",
            recipes.Select(r => r.SourceUrl).ToList()
        );

        await _unitOfWork.CommitAsync();
    }
    catch (Exception)
    {
        await _unitOfWork.RollbackAsync();
        throw;
    }
}
```

## Health Monitoring

Production health checks are included:

```csharp
// In Program.cs
services.AddEasyMealsDataHealthChecks();

// The health check endpoint will verify:
// - MongoDB connectivity
// - Database availability
// - Basic operations
```

## Migration from Entity Framework

### Compatibility Layer

The repository interfaces remain unchanged, ensuring seamless migration:

```csharp
// These interfaces work with both EF Core and MongoDB implementations
IRecipeRepository recipeRepo;
ICrawlStateRepository crawlRepo;
IUnitOfWork unitOfWork;
```

### Data Migration

To migrate existing data from SQL Server to MongoDB:

1. **Export existing data** using Entity Framework
2. **Transform relationships** to embedded documents
3. **Import using MongoDB bulk operations**

Example migration script:

```csharp
public async Task MigrateRecipesAsync()
{
    // Read from SQL Server (old EF context)
    var sqlRecipes = await _oldContext.Recipes
        .Include(r => r.Ingredients)
        .Include(r => r.Instructions)
        .ToListAsync();

    // Transform to MongoDB documents
    var mongoRecipes = sqlRecipes.Select(r => new RecipeDocument
    {
        Title = r.Title,
        Description = r.Description,
        // ... map other properties
        Ingredients = r.Ingredients.Select(i => new IngredientDocument
        {
            Name = i.Name,
            Amount = i.Amount,
            Unit = i.Unit
        }).ToList()
    });

    // Bulk insert to MongoDB
    await _recipeRepository.AddManyAsync(mongoRecipes);
    await _unitOfWork.CommitAsync();
}
```

## Performance Considerations

### Indexing Best Practices

- **Compound Indexes**: Order fields by selectivity (most selective first)
- **Text Indexes**: Use weights to prioritize title matches over descriptions
- **Sparse Indexes**: For optional fields to save space
- **Background Creation**: Indexes are created in background to avoid blocking

### Query Optimization

```csharp
// Efficient: Uses compound index
var recipes = await _recipeRepository.GetManyAsync(
    r => r.SourceProvider == "example" && r.IsActive && !r.IsDeleted
);

// Less efficient: Missing index support
var recipes = await _recipeRepository.GetManyAsync(
    r => r.Description.Contains("complex search term")
);
```

### Bulk Operations

```csharp
// Use bulk operations for large datasets
await _recipeRepository.AddManyAsync(largeRecipeList);

// Better than individual operations
foreach (var recipe in largeRecipeList)
{
    await _recipeRepository.AddAsync(recipe); // Avoid this pattern
}
```

## Configuration Options

### Connection String Options

```bash
# Basic connection
mongodb://localhost:27017/easymealsprod

# With authentication
mongodb://username:password@cluster.mongodb.net/easymealsprod

# Replica set
mongodb://host1:27017,host2:27017,host3:27017/easymealsprod?replicaSet=rs0

# With options
mongodb://localhost:27017/easymealsprod?maxPoolSize=50&wtimeoutMS=2500
```

### Client Settings

```csharp
var settings = new MongoClientSettings
{
    // Connection pooling
    MaxConnectionPoolSize = 100,
    MinConnectionPoolSize = 5,
    MaxConnectionIdleTime = TimeSpan.FromMinutes(10),

    // Timeouts
    ConnectTimeout = TimeSpan.FromSeconds(30),
    ServerSelectionTimeout = TimeSpan.FromSeconds(30),
    SocketTimeout = TimeSpan.FromMinutes(5),

    // Write concern
    WriteConcern = WriteConcern.WMajority,

    // Read preference
    ReadPreference = ReadPreference.Secondary
};
```

## Troubleshooting

### Common Issues

1. **Connection Timeout**

   ```
   Solution: Check network connectivity and increase timeouts
   ```

2. **Duplicate Key Error**

   ```
   Cause: Violating unique index constraint
   Solution: Check for existing documents before insert
   ```

3. **Index Creation Failures**
   ```
   Cause: Conflicting indexes or data
   Solution: Drop existing indexes before recreation
   ```

### Logging

Enable detailed logging for debugging:

```csharp
// In appsettings.json
{
  "Logging": {
    "LogLevel": {
      "EasyMeals.Shared.Data": "Debug",
      "MongoDB.Driver": "Information"
    }
  }
}
```

## Examples Repository

See the `EXAMPLES.md` file for comprehensive code examples covering:

- Complex aggregation pipelines
- Custom repository implementations
- Advanced querying patterns
- Performance optimization techniques
- Testing strategies

## License

This project is licensed under the MIT License. See LICENSE file for details.
