using EasyMeals.Shared.Data.Attributes;
using EasyMeals.Shared.Data.Documents;
using EasyMeals.Shared.Data.Documents.Recipe;
using MongoDB.Bson;
using MongoDB.Driver;

namespace EasyMeals.Shared.Data.Configuration;

/// <summary>
///     MongoDB index configuration for optimal query performance
///     Implements modular indexing strategy with separation between base and specific indexes
///     Follows MongoDB best practices for query optimization and maintainability
/// </summary>
public static class MongoIndexConfiguration
{
	/// <summary>
	///     Creates default indexes for BaseDocument fields across all collections
	///     This method is called by WithDefaultIndexes() in the fluent API
	/// </summary>
	/// <param name="database">MongoDB database instance</param>
	/// <returns>Task representing the async operation</returns>
	public static async Task CreateBaseDocumentIndexesAsync(IMongoDatabase database)
	{
		// Get all collections in the database
		IAsyncCursor<string>? collections = await database.ListCollectionNamesAsync();
		List<string>? collectionNames = await collections.ToListAsync();

		var tasks = new List<Task>();

		foreach (string collectionName in collectionNames)
		{
			// Skip system collections
			if (collectionName.StartsWith("system."))
				continue;

			tasks.Add(CreateBaseIndexesForCollectionAsync(database, collectionName));
		}

		await Task.WhenAll(tasks);
	}

	/// <summary>
	///     Creates base document indexes for a specific collection
	/// </summary>
	/// <typeparam name="TDocument">Document type extending BaseDocument</typeparam>
	/// <param name="database">MongoDB database instance</param>
	/// <returns>Task representing the async operation</returns>
	public static async Task CreateBaseDocumentIndexesAsync<TDocument>(IMongoDatabase database)
		where TDocument : BaseDocument
	{
		string collectionName = GetCollectionName<TDocument>();
		await CreateBaseIndexesForCollectionAsync(database, collectionName);
	}

	/// <summary>
	///     Creates base indexes for a specific collection by name
	/// </summary>
	private static async Task CreateBaseIndexesForCollectionAsync(IMongoDatabase database, string collectionName)
	{
		IMongoCollection<BaseDocument>? collection = database.GetCollection<BaseDocument>(collectionName);

		var baseIndexes = new[]
		{
			// 1. Primary query index for date-based operations
			new CreateIndexModel<BaseDocument>(
				Builders<BaseDocument>.IndexKeys
					.Descending(d => d.CreatedAt)
					.Descending(d => d.UpdatedAt),
				new CreateIndexOptions
				{
					Name = "idx_base_dates",
					Background = true
				}
			),

			// 2. Efficient range queries on creation date
			new CreateIndexModel<BaseDocument>(
				Builders<BaseDocument>.IndexKeys.Descending(d => d.CreatedAt),
				new CreateIndexOptions
				{
					Name = "idx_base_created_at",
					Background = true
				}
			),

			// 3. Efficient range queries on update date
			new CreateIndexModel<BaseDocument>(
				Builders<BaseDocument>.IndexKeys.Descending(d => d.UpdatedAt),
				new CreateIndexOptions
				{
					Name = "idx_base_updated_at",
					Background = true
				}
			)
		};

		await collection.Indexes.CreateManyAsync(baseIndexes);
	}

	/// <summary>
	///     Creates indexes for BaseSoftDeletableDocument fields
	/// </summary>
	/// <typeparam name="TDocument">Document type extending BaseSoftDeletableDocument</typeparam>
	/// <param name="database">MongoDB database instance</param>
	/// <returns>Task representing the async operation</returns>
	public static async Task CreateSoftDeletableIndexesAsync<TDocument>(IMongoDatabase database)
		where TDocument : BaseSoftDeletableDocument
	{
		IMongoCollection<TDocument>? collection = database.GetCollection<TDocument>(GetCollectionName<TDocument>());

		var softDeleteIndexes = new[]
		{
			// 1. Critical index for filtering non-deleted documents
			new CreateIndexModel<TDocument>(
				Builders<TDocument>.IndexKeys
					.Ascending(d => d.IsDeleted)
					.Descending(d => d.CreatedAt),
				new CreateIndexOptions
				{
					Name = "idx_soft_delete_filter",
					Background = true
				}
			),

			// 2. Sparse index for deleted documents recovery queries
			new CreateIndexModel<TDocument>(
				Builders<TDocument>.IndexKeys.Ascending(d => d.DeletedAt),
				new CreateIndexOptions
				{
					Name = "idx_deleted_at",
					Background = true,
					Sparse = true // Only index documents where DeletedAt is not null
				}
			),

			// 3. Compound index for active document queries with sorting
			new CreateIndexModel<TDocument>(
				Builders<TDocument>.IndexKeys
					.Ascending(d => d.IsDeleted)
					.Descending(d => d.UpdatedAt),
				new CreateIndexOptions
				{
					Name = "idx_active_documents",
					Background = true
				}
			)
		};

		await collection.Indexes.CreateManyAsync(softDeleteIndexes);
	}

	/// <summary>
	///     Creates all necessary indexes for optimal performance (legacy method)
	///     Now delegates to more specific methods for better organization
	/// </summary>
	/// <param name="database">MongoDB database instance</param>
	/// <returns>Task representing the async operation</returns>
	[Obsolete("Use CreateBaseDocumentIndexesAsync() for default indexes and specific methods for collection indexes")]
	public static async Task CreateAllIndexesAsync(IMongoDatabase database)
	{
		var tasks = new List<Task>
		{
			CreateBaseDocumentIndexesAsync(database),
			CreateRecipeSpecificIndexesAsync(database)
		};

		await Task.WhenAll(tasks);
	}

	#region Helper Methods

	/// <summary>
	///     Gets the collection name for a document type using BsonCollection attribute or type name
	/// </summary>
	/// <typeparam name="TDocument">Document type</typeparam>
	/// <returns>Collection name</returns>
	private static string GetCollectionName<TDocument>() where TDocument : BaseDocument
	{
		var attribute = typeof(TDocument).GetCustomAttributes(typeof(BsonCollectionAttribute), true)
			.FirstOrDefault() as BsonCollectionAttribute;

		return attribute?.CollectionName ?? typeof(TDocument).Name.ToLowerInvariant();
	}

	#endregion

	#region Collection-Specific Index Methods

	/// <summary>
	///     Creates Recipe collection specific indexes for search and filtering
	///     Call this separately from WithDefaultIndexes() for recipe-specific optimizations
	/// </summary>
	/// <param name="database">MongoDB database instance</param>
	/// <returns>Task representing the async operation</returns>
	public static async Task CreateRecipeSpecificIndexesAsync(IMongoDatabase database)
	{
		IMongoCollection<RecipeDocument>? collection = database.GetCollection<RecipeDocument>(GetCollectionName<RecipeDocument>());

		var recipeIndexes = new[]
		{
			// 1. Unique index for source URL (duplicate prevention)
			new CreateIndexModel<RecipeDocument>(
				Builders<RecipeDocument>.IndexKeys.Ascending(r => r.SourceUrl),
				new CreateIndexOptions
				{
					Name = "idx_recipe_source_url_unique",
					Unique = true,
					Background = true
				}
			),

			// 2. Text index for comprehensive search across title and description
			new CreateIndexModel<RecipeDocument>(
				Builders<RecipeDocument>.IndexKeys
					.Text(r => r.Title)
					.Text(r => r.Description),
				new CreateIndexOptions
				{
					Name = "idx_recipe_text_search",
					Background = true
				}
			),

			// 3. Multikey index for tags (array field optimization)
			new CreateIndexModel<RecipeDocument>(
				Builders<RecipeDocument>.IndexKeys.Ascending(r => r.Tags),
				new CreateIndexOptions
				{
					Name = "idx_recipe_tags",
					Background = true
				}
			),

			// 4. Index for cuisine filtering
			new CreateIndexModel<RecipeDocument>(
				Builders<RecipeDocument>.IndexKeys.Ascending(r => r.Cuisine),
				new CreateIndexOptions
				{
					Name = "idx_recipe_cuisine",
					Background = true
				}
			),

			// 5. Compound index for time-based filtering (prep + cook time)
			new CreateIndexModel<RecipeDocument>(
				Builders<RecipeDocument>.IndexKeys
					.Ascending(r => r.PrepTimeMinutes)
					.Ascending(r => r.CookTimeMinutes),
				new CreateIndexOptions
				{
					Name = "idx_recipe_time_constraints",
					Background = true
				}
			),

			// 6. Compound index for nutritional filtering
			new CreateIndexModel<RecipeDocument>(
				Builders<RecipeDocument>.IndexKeys
					.Ascending("nutritionInfo.calories")
					.Ascending("nutritionInfo.proteinGrams"),
				new CreateIndexOptions
				{
					Name = "idx_recipe_nutrition_criteria",
					Background = true
				}
			),

			// 7. Compound index for rating and popularity sorting
			new CreateIndexModel<RecipeDocument>(
				Builders<RecipeDocument>.IndexKeys
					.Descending(r => r.Rating)
					.Descending(r => r.ReviewCount),
				new CreateIndexOptions
				{
					Name = "idx_recipe_rating_popularity",
					Background = true
				}
			),

			// 8. Index for source provider queries
			new CreateIndexModel<RecipeDocument>(
				Builders<RecipeDocument>.IndexKeys.Ascending(r => r.ProviderName),
				new CreateIndexOptions
				{
					Name = "idx_recipe_source_provider",
					Background = true
				}
			),

			// 9. Sparse index for difficulty level
			new CreateIndexModel<RecipeDocument>(
				Builders<RecipeDocument>.IndexKeys.Ascending(r => r.Difficulty),
				new CreateIndexOptions
				{
					Name = "idx_recipe_difficulty",
					Background = true,
					Sparse = true
				}
			)
		};

		await collection.Indexes.CreateManyAsync(recipeIndexes);
	}

	/// <summary>
	///     Creates comprehensive recipe indexes including base document and recipe-specific indexes
	///     Use this for complete recipe collection optimization
	/// </summary>
	/// <param name="database">MongoDB database instance</param>
	/// <returns>Task representing the async operation</returns>
	public static async Task CreateCompleteRecipeIndexesAsync(IMongoDatabase database)
	{
		await Task.WhenAll(
			CreateBaseDocumentIndexesAsync<RecipeDocument>(database),
			CreateSoftDeletableIndexesAsync<RecipeDocument>(database),
			CreateRecipeSpecificIndexesAsync(database)
		);
	}

	#endregion

	#region Index Management

	/// <summary>
	///     Drops base document indexes from all collections
	/// </summary>
	/// <param name="database">MongoDB database instance</param>
	/// <returns>Task representing the async operation</returns>
	public static async Task DropBaseDocumentIndexesAsync(IMongoDatabase database)
	{
		IAsyncCursor<string>? collections = await database.ListCollectionNamesAsync();
		List<string>? collectionNames = await collections.ToListAsync();

		var tasks = new List<Task>();

		foreach (string collectionName in collectionNames)
		{
			if (collectionName.StartsWith("system."))
				continue;

			tasks.Add(DropBaseIndexesForCollectionAsync(database, collectionName));
		}

		await Task.WhenAll(tasks);
	}

	/// <summary>
	///     Drops base indexes for a specific collection
	/// </summary>
	private static async Task DropBaseIndexesForCollectionAsync(IMongoDatabase database, string collectionName)
	{
		IMongoCollection<BaseDocument>? collection = database.GetCollection<BaseDocument>(collectionName);

		var baseIndexNames = new[]
		{
			"idx_base_dates",
			"idx_base_created_at",
			"idx_base_updated_at"
		};

		foreach (string indexName in baseIndexNames)
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
	///     Drops recipe-specific indexes
	/// </summary>
	/// <param name="database">MongoDB database instance</param>
	/// <returns>Task representing the async operation</returns>
	public static async Task DropRecipeSpecificIndexesAsync(IMongoDatabase database)
	{
		IMongoCollection<RecipeDocument>? collection = database.GetCollection<RecipeDocument>(GetCollectionName<RecipeDocument>());

		var recipeIndexNames = new[]
		{
			"idx_recipe_source_url_unique",
			"idx_recipe_text_search",
			"idx_recipe_tags",
			"idx_recipe_cuisine",
			"idx_recipe_time_constraints",
			"idx_recipe_nutrition_criteria",
			"idx_recipe_rating_popularity",
			"idx_recipe_source_provider",
			"idx_recipe_difficulty"
		};

		foreach (string indexName in recipeIndexNames)
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
	///     Drops all custom indexes (useful for migrations or cleanup)
	///     Preserves MongoDB system indexes
	/// </summary>
	[Obsolete("Use specific drop methods like DropBaseDocumentIndexesAsync() and DropRecipeSpecificIndexesAsync()")]
	public static async Task DropAllCustomIndexesAsync(IMongoDatabase database)
	{
		await Task.WhenAll(
			DropBaseDocumentIndexesAsync(database),
			DropRecipeSpecificIndexesAsync(database)
		);
	}

	/// <summary>
	///     Drops custom indexes for recipes collection (legacy method)
	/// </summary>
	[Obsolete("Use DropRecipeSpecificIndexesAsync() instead")]
	public static async Task DropRecipeIndexesAsync(IMongoDatabase database)
	{
		await DropRecipeSpecificIndexesAsync(database);
	}

	#endregion

	#region Index Statistics and Monitoring

	/// <summary>
	///     Gets index statistics for performance monitoring
	/// </summary>
	/// <param name="database">MongoDB database instance</param>
	/// <returns>Dictionary containing collection statistics</returns>
	public static async Task<Dictionary<string, BsonDocument>> GetIndexStatsAsync(IMongoDatabase database)
	{
		var stats = new Dictionary<string, BsonDocument>();

		IAsyncCursor<string>? collections = await database.ListCollectionNamesAsync();
		List<string>? collectionNames = await collections.ToListAsync();

		foreach (string collectionName in collectionNames)
		{
			if (collectionName.StartsWith("system."))
				continue;

			try
			{
				var collStats = await database.RunCommandAsync<BsonDocument>(
					new BsonDocument("collStats", collectionName));
				stats[collectionName] = collStats;
			}
			catch (MongoCommandException)
			{
				// Collection might not exist or be accessible
			}
		}

		return stats;
	}

	/// <summary>
	///     Gets detailed index information for a specific collection
	/// </summary>
	/// <typeparam name="TDocument">Document type</typeparam>
	/// <param name="database">MongoDB database instance</param>
	/// <returns>List of index information</returns>
	public static async Task<List<BsonDocument>> GetCollectionIndexesAsync<TDocument>(IMongoDatabase database)
		where TDocument : BaseDocument
	{
		IMongoCollection<TDocument>? collection = database.GetCollection<TDocument>(GetCollectionName<TDocument>());
		IAsyncCursor<BsonDocument>? cursor = await collection.Indexes.ListAsync();
		return await cursor.ToListAsync();
	}

	#endregion
}