using EasyMeals.Shared.Data.Documents;
using MongoDB.Bson;
using MongoDB.Driver;

namespace EasyMeals.Shared.Data.Configuration;

/// <summary>
///     MongoDB index configuration for optimal query performance
///     Implements comprehensive indexing strategy for recipes and crawl states
///     Follows MongoDB best practices for query optimization
/// </summary>
public static class MongoIndexConfiguration
{
    /// <summary>
    ///     Creates all necessary indexes for optimal performance
    ///     Should be called during application startup or deployment
    /// </summary>
    /// <param name="database">MongoDB database instance</param>
    /// <returns>Task representing the async operation</returns>
    public static async Task CreateAllIndexesAsync(IMongoDatabase database)
    {
        var tasks = new List<Task>
        {
            CreateRecipeIndexesAsync(database),
            CreateCrawlStateIndexesAsync(database)
        };

        await Task.WhenAll(tasks);
    }

    /// <summary>
    ///     Creates optimized indexes for the recipes collection
    ///     Supports efficient queries for search, filtering, and sorting
    /// </summary>
    public static async Task CreateRecipeIndexesAsync(IMongoDatabase database)
    {
        IMongoCollection<RecipeDocument>? collection = database.GetCollection<RecipeDocument>("recipes");

        var indexes = new[]
        {
            // 1. Compound index for source provider queries with active/deleted filtering
            new CreateIndexModel<RecipeDocument>(
                Builders<RecipeDocument>.IndexKeys
                    .Ascending(r => r.SourceProvider)
                    .Ascending(r => r.IsActive)
                    .Ascending(r => r.IsDeleted)
                    .Descending(r => r.CreatedAt),
                new CreateIndexOptions
                {
                    Name = "idx_source_provider_active_created",
                    Background = true
                }
            ),

            // 2. Unique index for source URL (duplicate prevention)
            new CreateIndexModel<RecipeDocument>(
                Builders<RecipeDocument>.IndexKeys.Ascending(r => r.SourceUrl),
                new CreateIndexOptions
                {
                    Name = "idx_source_url_unique",
                    Unique = true,
                    Background = true
                }
            ),

            // 3. Text index for comprehensive search across title and description
            new CreateIndexModel<RecipeDocument>(
                Builders<RecipeDocument>.IndexKeys
                    .Text(r => r.Title)
                    .Text(r => r.Description),
                new CreateIndexOptions
                {
                    Name = "idx_text_search",
                    Background = true
                }
            ),

            // 4. Multikey index for tags (array field optimization)
            new CreateIndexModel<RecipeDocument>(
                Builders<RecipeDocument>.IndexKeys.Ascending(r => r.Tags),
                new CreateIndexOptions
                {
                    Name = "idx_tags",
                    Background = true
                }
            ),

            // 5. Case-insensitive index for cuisine filtering
            new CreateIndexModel<RecipeDocument>(
                Builders<RecipeDocument>.IndexKeys.Ascending(r => r.Cuisine),
                new CreateIndexOptions
                {
                    Name = "idx_cuisine",
                    Background = true
                }
            ),

            // 6. Compound index for time-based filtering (prep + cook time)
            new CreateIndexModel<RecipeDocument>(
                Builders<RecipeDocument>.IndexKeys
                    .Ascending(r => r.PrepTimeMinutes)
                    .Ascending(r => r.CookTimeMinutes)
                    .Ascending(r => r.IsActive),
                new CreateIndexOptions
                {
                    Name = "idx_time_constraints",
                    Background = true
                }
            ),

            // 7. Compound index for nutritional filtering
            new CreateIndexModel<RecipeDocument>(
                Builders<RecipeDocument>.IndexKeys
                    .Ascending("nutritionInfo.calories")
                    .Ascending("nutritionInfo.proteinGrams")
                    .Ascending(r => r.IsActive),
                new CreateIndexOptions
                {
                    Name = "idx_nutrition_criteria",
                    Background = true
                }
            ),

            // 8. Index for date range queries (created/updated)
            new CreateIndexModel<RecipeDocument>(
                Builders<RecipeDocument>.IndexKeys
                    .Descending(r => r.UpdatedAt)
                    .Descending(r => r.CreatedAt),
                new CreateIndexOptions
                {
                    Name = "idx_date_range",
                    Background = true
                }
            ),

            // 9. Compound index for rating and popularity sorting
            new CreateIndexModel<RecipeDocument>(
                Builders<RecipeDocument>.IndexKeys
                    .Descending(r => r.Rating)
                    .Descending(r => r.ReviewCount)
                    .Ascending(r => r.IsActive),
                new CreateIndexOptions
                {
                    Name = "idx_rating_popularity",
                    Background = true
                }
            ),

            // 10. Sparse index for difficulty level
            new CreateIndexModel<RecipeDocument>(
                Builders<RecipeDocument>.IndexKeys.Ascending(r => r.Difficulty),
                new CreateIndexOptions
                {
                    Name = "idx_difficulty",
                    Background = true,
                    Sparse = true
                }
            )
        };

        await collection.Indexes.CreateManyAsync(indexes);
    }

    /// <summary>
    ///     Creates optimized indexes for the crawl states collection
    ///     Supports efficient distributed crawling operations
    /// </summary>
    public static async Task CreateCrawlStateIndexesAsync(IMongoDatabase database)
    {
        IMongoCollection<CrawlStateDocument>? collection = database.GetCollection<CrawlStateDocument>("crawlstates");

        var indexes = new[]
        {
            // 1. Unique index for source provider (one state per provider)
            new CreateIndexModel<CrawlStateDocument>(
                Builders<CrawlStateDocument>.IndexKeys.Ascending(c => c.SourceProvider),
                new CreateIndexOptions
                {
                    Name = "idx_source_provider_unique",
                    Unique = true,
                    Background = true
                }
            ),

            // 2. Compound index for active crawling with priority ordering
            new CreateIndexModel<CrawlStateDocument>(
                Builders<CrawlStateDocument>.IndexKeys
                    .Ascending(c => c.IsActive)
                    .Descending(c => c.Priority)
                    .Ascending(c => c.LastCrawlTime),
                new CreateIndexOptions
                {
                    Name = "idx_active_priority_crawl",
                    Background = true
                }
            ),

            // 3. Index for scheduled crawling
            new CreateIndexModel<CrawlStateDocument>(
                Builders<CrawlStateDocument>.IndexKeys
                    .Ascending(c => c.NextScheduledCrawl)
                    .Ascending(c => c.IsActive),
                new CreateIndexOptions
                {
                    Name = "idx_scheduled_crawl",
                    Background = true
                }
            ),

            // 4. Index for stale state detection
            new CreateIndexModel<CrawlStateDocument>(
                Builders<CrawlStateDocument>.IndexKeys
                    .Ascending(c => c.UpdatedAt)
                    .Ascending(c => c.IsActive),
                new CreateIndexOptions
                {
                    Name = "idx_stale_detection",
                    Background = true
                }
            ),

            // 5. Index for priority-based processing
            new CreateIndexModel<CrawlStateDocument>(
                Builders<CrawlStateDocument>.IndexKeys
                    .Descending(c => c.Priority)
                    .Ascending(c => c.LastCrawlTime),
                new CreateIndexOptions
                {
                    Name = "idx_priority_processing",
                    Background = true
                }
            ),

            // 6. Index for session-based claiming
            new CreateIndexModel<CrawlStateDocument>(
                Builders<CrawlStateDocument>.IndexKeys
                    .Ascending(c => c.CurrentSessionId)
                    .Ascending(c => c.IsActive),
                new CreateIndexOptions
                {
                    Name = "idx_session_claiming",
                    Background = true,
                    Sparse = true
                }
            ),

            // 7. Compound index for pending work detection
            new CreateIndexModel<CrawlStateDocument>(
                Builders<CrawlStateDocument>.IndexKeys
                    .Ascending(c => c.IsActive)
                    .Descending(c => c.Priority),
                new CreateIndexOptions
                {
                    Name = "idx_pending_work",
                    Background = true
                }
            )
        };

        await collection.Indexes.CreateManyAsync(indexes);
    }

    /// <summary>
    ///     Drops all custom indexes (useful for migrations or cleanup)
    ///     Preserves MongoDB system indexes
    /// </summary>
    public static async Task DropAllCustomIndexesAsync(IMongoDatabase database)
    {
        await DropRecipeIndexesAsync(database);
        await DropCrawlStateIndexesAsync(database);
    }

    /// <summary>
    ///     Drops custom indexes for recipes collection
    /// </summary>
    public static async Task DropRecipeIndexesAsync(IMongoDatabase database)
    {
        IMongoCollection<RecipeDocument>? collection = database.GetCollection<RecipeDocument>("recipes");

        var indexNames = new[]
        {
            "idx_source_provider_active_created",
            "idx_source_url_unique",
            "idx_text_search",
            "idx_tags",
            "idx_cuisine",
            "idx_time_constraints",
            "idx_nutrition_criteria",
            "idx_date_range",
            "idx_rating_popularity",
            "idx_difficulty"
        };

        foreach (string indexName in indexNames)
        {
            try
            {
                await collection.Indexes.DropOneAsync(indexName);
            }
            catch (MongoCommandException)
            {
                // Index doesn't exist, continue
            }
        }
    }

    /// <summary>
    ///     Drops custom indexes for crawl states collection
    /// </summary>
    public static async Task DropCrawlStateIndexesAsync(IMongoDatabase database)
    {
        IMongoCollection<CrawlStateDocument>? collection = database.GetCollection<CrawlStateDocument>("crawlstates");

        var indexNames = new[]
        {
            "idx_source_provider_unique",
            "idx_active_priority_crawl",
            "idx_scheduled_crawl",
            "idx_stale_detection",
            "idx_priority_processing",
            "idx_session_claiming",
            "idx_pending_work"
        };

        foreach (string indexName in indexNames)
        {
            try
            {
                await collection.Indexes.DropOneAsync(indexName);
            }
            catch (MongoCommandException)
            {
                // Index doesn't exist, continue
            }
        }
    }

    /// <summary>
    ///     Gets index statistics for performance monitoring
    /// </summary>
    public static async Task<Dictionary<string, BsonDocument>> GetIndexStatsAsync(IMongoDatabase database)
    {
        var stats = new Dictionary<string, BsonDocument>();

        // Get recipe collection index stats
        IMongoCollection<RecipeDocument>? recipesCollection = database.GetCollection<RecipeDocument>("recipes");
        var recipeStats = await recipesCollection.Database.RunCommandAsync<BsonDocument>(
            new BsonDocument("collStats", "recipes"));
        stats["recipes"] = recipeStats;

        // Get crawl states collection index stats
        IMongoCollection<CrawlStateDocument>? crawlStatesCollection = database.GetCollection<CrawlStateDocument>("crawlstates");
        var crawlStats = await crawlStatesCollection.Database.RunCommandAsync<BsonDocument>(
            new BsonDocument("collStats", "crawlstates"));
        stats["crawlstates"] = crawlStats;

        return stats;
    }
}