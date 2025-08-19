# MongoDB Usage Examples for EasyMeals.Shared.Data

## Example 1: Basic MongoDB Setup in an API Project

```csharp
// Program.cs
using EasyMeals.Shared.Data.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Basic MongoDB configuration
builder.Services.AddEasyMealsDataMongoDB(
    connectionString: builder.Configuration.GetConnectionString("MongoConnection")!,
    databaseName: "easymealsprod"
);

// Advanced MongoDB configuration with custom settings
/*
var mongoSettings = new MongoClientSettings
{
    Server = new MongoServerAddress("mongodb.example.com", 27017),
    ConnectTimeout = TimeSpan.FromSeconds(30),
    ServerSelectionTimeout = TimeSpan.FromSeconds(30),
    MaxConnectionPoolSize = 100,
    MinConnectionPoolSize = 5,
    WriteConcern = WriteConcern.WMajority,
    ReadPreference = ReadPreference.SecondaryPreferred
};
builder.Services.AddEasyMealsDataMongoDB(mongoSettings, "easymealsprod");
*/

// For testing environments
// builder.Services.AddEasyMealsDataInMemory("test_database");

// Add health checks for MongoDB
builder.Services.AddEasyMealsDataHealthChecks();

var app = builder.Build();

// Ensure MongoDB database and indexes are created (run once during deployment)
await app.Services.EnsureEasyMealsDatabaseAsync();

// Health check endpoint
app.MapHealthChecks("/health");

app.Run();
```

## Example 2: Using MongoDB Repositories in a Service

```csharp
using EasyMeals.Shared.Data.Documents;
using EasyMeals.Shared.Data.Interfaces;

public class RecipeService
{
    private readonly IRecipeRepository _recipeRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RecipeService(IRecipeRepository recipeRepository, IUnitOfWork unitOfWork)
    {
        _recipeRepository = recipeRepository;
        _unitOfWork = unitOfWork;
    }

    // Create a new recipe with embedded ingredients
    public async Task<string> CreateRecipeAsync(RecipeDocument recipe)
    {
        // Add ingredients as embedded documents
        recipe.Ingredients = new List<IngredientDocument>
        {
            new() { Name = "Chicken breast", Amount = "2", Unit = "pieces" },
            new() { Name = "Olive oil", Amount = "2", Unit = "tbsp" },
            new() { Name = "Salt", Amount = "1", Unit = "tsp" }
        };

        // Add instructions as embedded documents
        recipe.Instructions = new List<InstructionDocument>
        {
            new() { StepNumber = 1, Description = "Season chicken with salt" },
            new() { StepNumber = 2, Description = "Heat oil in pan" },
            new() { StepNumber = 3, Description = "Cook chicken until done" }
        };

        var recipeId = await _recipeRepository.AddAsync(recipe);
        await _unitOfWork.CommitAsync();
        return recipeId;
    }

    // Text search across title and description
    public async Task<IEnumerable<RecipeDocument>> SearchRecipesAsync(string searchTerm)
    {
        return await _recipeRepository.SearchByTextAsync(searchTerm);
    }

    // Complex filtering with MongoDB queries
    public async Task<IEnumerable<RecipeDocument>> GetQuickItalianRecipesAsync()
    {
        return await _recipeRepository.GetManyAsync(r =>
            r.Cuisine == "Italian" &&
            r.Tags.Contains("quick") &&
            r.PrepTimeMinutes <= 30 &&
            r.IsActive &&
            !r.IsDeleted
        );
    }

    // Paginated results with sorting
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

    // Update with optimistic concurrency
    public async Task UpdateRecipeAsync(RecipeDocument recipe)
    {
        await _recipeRepository.UpdateAsync(recipe);
        await _unitOfWork.CommitAsync();
    }

    // Soft delete (sets IsDeleted = true)
    public async Task DeleteRecipeAsync(string id)
    {
        var recipe = await _recipeRepository.GetByIdAsync(id);
        if (recipe != null)
        {
            await _recipeRepository.SoftDeleteAsync(id);
            await _unitOfWork.CommitAsync();
        }
    }

    // Bulk operations for efficiency
    public async Task ImportRecipesAsync(IEnumerable<RecipeDocument> recipes)
    {
        await _recipeRepository.AddManyAsync(recipes);
        await _unitOfWork.CommitAsync();
    }
}
```

## Example 3: Advanced MongoDB Aggregation Pipelines

```csharp
using MongoDB.Driver;
using EasyMeals.Shared.Data.Documents;
using EasyMeals.Shared.Data.Interfaces;

public class RecipeAnalyticsService
{
    private readonly IRecipeRepository _recipeRepository;

    public RecipeAnalyticsService(IRecipeRepository recipeRepository)
    {
        _recipeRepository = recipeRepository;
    }

    // Aggregation: Popular recipes by cuisine
    public async Task<IEnumerable<CuisineStats>> GetPopularRecipesByCuisineAsync()
    {
        var pipeline = _recipeRepository.Collection
            .Aggregate()
            .Match(r => r.IsActive && r.Rating >= 4.0)
            .Group(r => r.Cuisine, g => new CuisineStats
            {
                Cuisine = g.Key,
                RecipeCount = g.Count(),
                AverageRating = g.Average(r => r.Rating),
                TotalReviews = g.Sum(r => r.ReviewCount ?? 0)
            })
            .SortByDescending(g => g.RecipeCount);

        return await pipeline.ToListAsync();
    }

    // Aggregation: Recipe complexity analysis
    public async Task<IEnumerable<ComplexityStats>> GetRecipeComplexityStatsAsync()
    {
        var pipeline = _recipeRepository.Collection
            .Aggregate()
            .Match(r => r.IsActive && r.Ingredients.Count > 0)
            .Group(r => new
            {
                IngredientCount = r.Ingredients.Count <= 5 ? "Simple" :
                                 r.Ingredients.Count <= 10 ? "Medium" : "Complex",
                TimeRange = r.TotalTimeMinutes <= 30 ? "Quick" :
                           r.TotalTimeMinutes <= 60 ? "Medium" : "Long"
            }, g => new ComplexityStats
            {
                Category = g.Key.IngredientCount + " & " + g.Key.TimeRange,
                Count = g.Count(),
                AverageRating = g.Average(r => r.Rating ?? 0),
                AveragePrepTime = g.Average(r => r.PrepTimeMinutes ?? 0)
            })
            .SortByDescending(g => g.Count);

        return await pipeline.ToListAsync();
    }

    // Aggregation: Top ingredients usage
    public async Task<IEnumerable<IngredientUsage>> GetTopIngredientsAsync(int limit = 20)
    {
        var pipeline = _recipeRepository.Collection
            .Aggregate()
            .Match(r => r.IsActive && r.Ingredients.Count > 0)
            .Unwind<RecipeDocument, UnwindedIngredient>(r => r.Ingredients)
            .Group(r => r.Ingredients.Name, g => new IngredientUsage
            {
                IngredientName = g.Key,
                UsageCount = g.Count(),
                UniqueRecipes = g.Count() // In this simplified case
            })
            .SortByDescending(g => g.UsageCount)
            .Limit(limit);

        return await pipeline.ToListAsync();
    }

    // Text search with scoring
    public async Task<IEnumerable<RecipeSearchResult>> SearchWithRelevanceAsync(string searchTerm)
    {
        var filter = Builders<RecipeDocument>.Filter.And(
            Builders<RecipeDocument>.Filter.Text(searchTerm),
            Builders<RecipeDocument>.Filter.Eq(r => r.IsActive, true),
            Builders<RecipeDocument>.Filter.Eq(r => r.IsDeleted, false)
        );

        var projection = Builders<RecipeDocument>.Projection
            .Include(r => r.Title)
            .Include(r => r.Description)
            .Include(r => r.Cuisine)
            .Include(r => r.Rating)
            .MetaTextScore("score");

        var results = await _recipeRepository.Collection
            .Find(filter)
            .Project<RecipeSearchResult>(projection)
            .Sort(Builders<RecipeDocument>.Sort.MetaTextScore("score"))
            .Limit(50)
            .ToListAsync();

        return results;
    }
}

// Supporting classes for aggregation results
public class CuisineStats
{
    public string Cuisine { get; set; } = string.Empty;
    public int RecipeCount { get; set; }
    public double AverageRating { get; set; }
    public int TotalReviews { get; set; }
}

public class ComplexityStats
{
    public string Category { get; set; } = string.Empty;
    public int Count { get; set; }
    public double AverageRating { get; set; }
    public double AveragePrepTime { get; set; }
}

public class IngredientUsage
{
    public string IngredientName { get; set; } = string.Empty;
    public int UsageCount { get; set; }
    public int UniqueRecipes { get; set; }
}

public class RecipeSearchResult
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Cuisine { get; set; } = string.Empty;
    public double? Rating { get; set; }
    public double Score { get; set; } // Text search relevance score
}

public class UnwindedIngredient
{
    public IngredientDocument Ingredients { get; set; } = new();
}
```

## Example 4: MongoDB Transactions and Crawl State Management

```csharp
using EasyMeals.Shared.Data.Documents;
using EasyMeals.Shared.Data.Interfaces;

public class CrawlerService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRecipeRepository _recipeRepository;
    private readonly ICrawlStateRepository _crawlStateRepository;

    public CrawlerService(
        IUnitOfWork unitOfWork,
        IRecipeRepository recipeRepository,
        ICrawlStateRepository crawlStateRepository)
    {
        _unitOfWork = unitOfWork;
        _recipeRepository = recipeRepository;
        _crawlStateRepository = crawlStateRepository;
    }

    // Process crawl results with MongoDB transactions
    public async Task ProcessCrawlResultsAsync(
        string sourceProvider,
        IEnumerable<RecipeDocument> newRecipes)
    {
        using var session = await _unitOfWork.StartSessionAsync();

        try
        {
            // Get or create crawl state
            var crawlState = await _crawlStateRepository.GetBySourceProviderAsync(sourceProvider)
                ?? new CrawlStateDocument
                {
                    SourceProvider = sourceProvider,
                    Priority = 1
                };

            // Add all new recipes in bulk
            var recipesList = newRecipes.ToList();
            if (recipesList.Any())
            {
                await _recipeRepository.AddManyAsync(recipesList);
            }

            // Update crawl state with processed URLs
            var processedUrls = recipesList.Select(r => r.SourceUrl).ToList();
            crawlState.ProcessedUrls.AddRange(processedUrls);
            crawlState.LastCrawlTime = DateTime.UtcNow;
            crawlState.NextScheduledCrawl = DateTime.UtcNow.AddHours(24);

            await _crawlStateRepository.UpdateAsync(crawlState);

            // Commit all changes atomically
            await _unitOfWork.CommitAsync();
        }
        catch (Exception)
        {
            // Rollback on any error
            await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    // Claim next crawl job with atomic updates
    public async Task<CrawlStateDocument?> ClaimNextCrawlJobAsync(string sessionId)
    {
        // Find highest priority available job
        var availableJobs = await _crawlStateRepository.GetManyAsync(cs =>
            cs.IsActive &&
            cs.PendingUrls.Count > 0 &&
            (cs.CurrentSessionId == null || cs.CurrentSessionId == string.Empty)
        );

        var nextJob = availableJobs
            .OrderByDescending(cs => cs.Priority)
            .ThenBy(cs => cs.LastCrawlTime)
            .FirstOrDefault();

        if (nextJob == null)
            return null;

        // Atomically claim the job
        try
        {
            nextJob.CurrentSessionId = sessionId;
            await _crawlStateRepository.UpdateAsync(nextJob);
            await _unitOfWork.CommitAsync();
            return nextJob;
        }
        catch (Exception)
        {
            // Someone else claimed it, return null
            return null;
        }
    }

    // Update crawl progress
    public async Task UpdateCrawlProgressAsync(
        string sourceProvider,
        List<string> newPendingUrls,
        List<string> completedUrls)
    {
        var crawlState = await _crawlStateRepository.GetBySourceProviderAsync(sourceProvider);
        if (crawlState == null)
            return;

        // Add new URLs to pending
        await _crawlStateRepository.AddPendingUrlsAsync(sourceProvider, newPendingUrls);

        // Mark URLs as processed
        await _crawlStateRepository.MarkUrlsProcessedAsync(sourceProvider, completedUrls);

        await _unitOfWork.CommitAsync();
    }

    // Release session lock when crawling completes
    public async Task ReleaseCrawlJobAsync(string sourceProvider, string sessionId)
    {
        var crawlState = await _crawlStateRepository.GetBySourceProviderAsync(sourceProvider);
        if (crawlState?.CurrentSessionId == sessionId)
        {
            crawlState.CurrentSessionId = null;
            crawlState.LastCrawlTime = DateTime.UtcNow;

            await _crawlStateRepository.UpdateAsync(crawlState);
            await _unitOfWork.CommitAsync();
        }
    }
}

// Example: Distributed crawling coordinator
public class CrawlCoordinator
{
    private readonly ICrawlStateRepository _crawlStateRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CrawlCoordinator(ICrawlStateRepository crawlStateRepository, IUnitOfWork unitOfWork)
    {
        _crawlStateRepository = crawlStateRepository;
        _unitOfWork = unitOfWork;
    }

    // Initialize crawl states for multiple providers
    public async Task InitializeCrawlProvidersAsync(Dictionary<string, int> providerPriorities)
    {
        var crawlStates = new List<CrawlStateDocument>();

        foreach (var (provider, priority) in providerPriorities)
        {
            var existing = await _crawlStateRepository.GetBySourceProviderAsync(provider);
            if (existing == null)
            {
                crawlStates.Add(new CrawlStateDocument
                {
                    SourceProvider = provider,
                    Priority = priority,
                    IsActive = true,
                    PendingUrls = new List<string> { $"https://{provider}.com/recipes" },
                    Metadata = new Dictionary<string, object>
                    {
                        ["initialized"] = DateTime.UtcNow,
                        ["crawlType"] = "initial"
                    }
                });
            }
        }

        if (crawlStates.Any())
        {
            await _crawlStateRepository.AddManyAsync(crawlStates);
            await _unitOfWork.CommitAsync();
        }
    }
}
```

## Example 5: Testing with In-Memory MongoDB

```csharp
using Microsoft.Extensions.DependencyInjection;
using EasyMeals.Shared.Data.Documents;
using EasyMeals.Shared.Data.Interfaces;
using EasyMeals.Shared.Data.Extensions;

public class RecipeServiceTests
{
    private ServiceProvider GetServiceProvider()
    {
        var services = new ServiceCollection();

        // Use in-memory MongoDB for testing
        services.AddEasyMealsDataInMemory($"test_db_{Guid.NewGuid():N}");

        return services.BuildServiceProvider();
    }

    [Test]
    public async Task CreateRecipe_ValidRecipe_ReturnsCreatedRecipe()
    {
        // Arrange
        using var serviceProvider = GetServiceProvider();
        var recipeRepository = serviceProvider.GetRequiredService<IRecipeRepository>();
        var unitOfWork = serviceProvider.GetRequiredService<IUnitOfWork>();

        var recipe = new RecipeDocument
        {
            Title = "Test Pasta Recipe",
            Description = "A delicious test pasta",
            SourceUrl = "https://example.com/pasta-recipe",
            SourceProvider = "TestProvider",
            Cuisine = "Italian",
            PrepTimeMinutes = 15,
            CookTimeMinutes = 20,
            Tags = new List<string> { "pasta", "quick", "easy" },
            Ingredients = new List<IngredientDocument>
            {
                new() { Name = "Pasta", Amount = "500", Unit = "g" },
                new() { Name = "Tomato sauce", Amount = "200", Unit = "ml" }
            },
            Instructions = new List<InstructionDocument>
            {
                new() { StepNumber = 1, Description = "Boil water for pasta" },
                new() { StepNumber = 2, Description = "Cook pasta according to package" }
            }
        };

        // Act
        var recipeId = await recipeRepository.AddAsync(recipe);
        await unitOfWork.CommitAsync();

        // Assert
        var savedRecipe = await recipeRepository.GetByIdAsync(recipeId);
        Assert.IsNotNull(savedRecipe);
        Assert.AreEqual("Test Pasta Recipe", savedRecipe.Title);
        Assert.AreEqual(2, savedRecipe.Ingredients.Count);
        Assert.AreEqual(2, savedRecipe.Instructions.Count);
        Assert.Contains("pasta", savedRecipe.Tags);
    }

    [Test]
    public async Task SearchRecipes_WithTextSearch_ReturnsMatchingRecipes()
    {
        // Arrange
        using var serviceProvider = GetServiceProvider();
        var recipeRepository = serviceProvider.GetRequiredService<IRecipeRepository>();
        var unitOfWork = serviceProvider.GetRequiredService<IUnitOfWork>();

        var recipes = new[]
        {
            new RecipeDocument
            {
                Title = "Chicken Pasta Carbonara",
                Description = "Creamy pasta with chicken",
                SourceUrl = "https://example.com/carbonara",
                SourceProvider = "TestProvider",
                Tags = new List<string> { "pasta", "chicken", "creamy" }
            },
            new RecipeDocument
            {
                Title = "Beef Stir Fry",
                Description = "Quick beef with vegetables",
                SourceUrl = "https://example.com/stirfry",
                SourceProvider = "TestProvider",
                Tags = new List<string> { "beef", "quick", "healthy" }
            }
        };

        await recipeRepository.AddManyAsync(recipes);
        await unitOfWork.CommitAsync();

        // Act
        var searchResults = await recipeRepository.SearchByTextAsync("pasta chicken");

        // Assert
        Assert.IsNotEmpty(searchResults);
        Assert.IsTrue(searchResults.Any(r => r.Title.Contains("Carbonara")));
    }

    [Test]
    public async Task GetPagedRecipes_WithFiltering_ReturnsCorrectPage()
    {
        // Arrange
        using var serviceProvider = GetServiceProvider();
        var recipeRepository = serviceProvider.GetRequiredService<IRecipeRepository>();
        var unitOfWork = serviceProvider.GetRequiredService<IUnitOfWork>();

        // Create 15 test recipes
        var recipes = Enumerable.Range(1, 15).Select(i => new RecipeDocument
        {
            Title = $"Recipe {i}",
            Description = $"Description for recipe {i}",
            SourceUrl = $"https://example.com/recipe-{i}",
            SourceProvider = "TestProvider",
            IsActive = i % 2 == 1, // Only odd recipes are active
            Cuisine = i <= 7 ? "Italian" : "Mexican"
        });

        await recipeRepository.AddManyAsync(recipes);
        await unitOfWork.CommitAsync();

        // Act
        var pagedResult = await recipeRepository.GetPagedAsync(
            filter: r => r.IsActive && r.Cuisine == "Italian",
            pageNumber: 1,
            pageSize: 3,
            orderBy: r => r.Title,
            ascending: true
        );

        // Assert
        Assert.AreEqual(4, pagedResult.TotalCount); // Recipes 1, 3, 5, 7
        Assert.AreEqual(3, pagedResult.Items.Count); // Page size
        Assert.AreEqual(1, pagedResult.CurrentPage);
        Assert.AreEqual(2, pagedResult.TotalPages); // 4 items / 3 per page = 2 pages
        Assert.IsTrue(pagedResult.HasNextPage);
        Assert.IsFalse(pagedResult.HasPreviousPage);
    }

    [Test]
    public async Task UpdateRecipe_WithConcurrency_UpdatesSuccessfully()
    {
        // Arrange
        using var serviceProvider = GetServiceProvider();
        var recipeRepository = serviceProvider.GetRequiredService<IRecipeRepository>();
        var unitOfWork = serviceProvider.GetRequiredService<IUnitOfWork>();

        var recipe = new RecipeDocument
        {
            Title = "Original Title",
            Description = "Original Description",
            SourceUrl = "https://example.com/original",
            SourceProvider = "TestProvider"
        };

        var recipeId = await recipeRepository.AddAsync(recipe);
        await unitOfWork.CommitAsync();

        // Act
        var savedRecipe = await recipeRepository.GetByIdAsync(recipeId);
        savedRecipe!.Title = "Updated Title";
        savedRecipe.Description = "Updated Description";

        await recipeRepository.UpdateAsync(savedRecipe);
        await unitOfWork.CommitAsync();

        // Assert
        var updatedRecipe = await recipeRepository.GetByIdAsync(recipeId);
        Assert.AreEqual("Updated Title", updatedRecipe!.Title);
        Assert.AreEqual("Updated Description", updatedRecipe.Description);
    }

    [Test]
    public async Task SoftDelete_Recipe_MarksAsDeleted()
    {
        // Arrange
        using var serviceProvider = GetServiceProvider();
        var recipeRepository = serviceProvider.GetRequiredService<IRecipeRepository>();
        var unitOfWork = serviceProvider.GetRequiredService<IUnitOfWork>();

        var recipe = new RecipeDocument
        {
            Title = "Recipe to Delete",
            SourceUrl = "https://example.com/delete",
            SourceProvider = "TestProvider"
        };

        var recipeId = await recipeRepository.AddAsync(recipe);
        await unitOfWork.CommitAsync();

        // Act
        await recipeRepository.SoftDeleteAsync(recipeId);
        await unitOfWork.CommitAsync();

        // Assert - Should not be found in normal queries
        var activeRecipes = await recipeRepository.GetManyAsync(r => r.IsActive && !r.IsDeleted);
        Assert.IsFalse(activeRecipes.Any(r => r.Id == recipeId));

        // But should still exist when querying all (including deleted)
        var allRecipes = await recipeRepository.GetManyAsync(r => true);
        var deletedRecipe = allRecipes.FirstOrDefault(r => r.Id == recipeId);
        Assert.IsNotNull(deletedRecipe);
        Assert.IsTrue(deletedRecipe.IsDeleted);
    }
}
```
