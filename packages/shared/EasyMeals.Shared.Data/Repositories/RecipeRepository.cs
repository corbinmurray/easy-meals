using EasyMeals.Shared.Data.Documents;
using MongoDB.Bson;
using MongoDB.Driver;

namespace EasyMeals.Shared.Data.Repositories;

/// <summary>
///     MongoDB-specific recipe repository implementation with optimized queries
///     Provides efficient MongoDB operations for recipe management
///     Follows DDD principles and maintains aggregate boundaries
/// </summary>
public class RecipeRepository(
	IMongoDatabase database,
	IClientSessionHandle? session = null) : MongoRepository<RecipeDocument>(database, session), IRecipeRepository
{
	/// <summary>
	///     Gets recipes by source provider with pagination
	///     Uses MongoDB compound index for optimal performance
	/// </summary>
	public async Task<PagedResult<RecipeDocument>> GetBySourceProviderAsync(
		string sourceProvider,
		int pageNumber,
		int pageSize,
		bool includeInactive = false,
		CancellationToken cancellationToken = default)
	{
		FilterDefinitionBuilder<RecipeDocument>? filterBuilder = Builders<RecipeDocument>.Filter;
		FilterDefinition<RecipeDocument>? filter = filterBuilder.Eq(r => r.SourceProvider, sourceProvider);

		if (!includeInactive) filter = filterBuilder.And(filter, filterBuilder.Eq(r => r.IsActive, true));

		// Add soft delete filter
		filter = filterBuilder.And(filter, filterBuilder.Ne(r => r.IsDeleted, true));

		long totalCount = await _collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);

		int skip = (pageNumber - 1) * pageSize;
		List<RecipeDocument>? recipes = await _collection
			.Find(filter)
			.Sort(Builders<RecipeDocument>.Sort.Descending(r => r.CreatedAt))
			.Skip(skip)
			.Limit(pageSize)
			.ToListAsync(cancellationToken);

		return new PagedResult<RecipeDocument>
		{
			Items = recipes,
			TotalCount = (int)totalCount,
			PageNumber = pageNumber,
			PageSize = pageSize
		};
	}

	/// <summary>
	///     Gets active recipes with specific cooking time constraints
	///     Uses MongoDB range queries for efficient time-based filtering
	/// </summary>
	public async Task<IEnumerable<RecipeDocument>> GetByTimeConstraintsAsync(
		int? maxPrepTime = null,
		int? maxCookTime = null,
		CancellationToken cancellationToken = default)
	{
		FilterDefinitionBuilder<RecipeDocument>? filterBuilder = Builders<RecipeDocument>.Filter;
		FilterDefinition<RecipeDocument>? filter = filterBuilder.And(
			filterBuilder.Eq(r => r.IsActive, true),
			filterBuilder.Ne(r => r.IsDeleted, true)
		);

		if (maxPrepTime.HasValue) filter = filterBuilder.And(filter, filterBuilder.Lte(r => r.PrepTimeMinutes, maxPrepTime.Value));

		if (maxCookTime.HasValue) filter = filterBuilder.And(filter, filterBuilder.Lte(r => r.CookTimeMinutes, maxCookTime.Value));

		return await _collection
			.Find(filter)
			.Sort(Builders<RecipeDocument>.Sort.Ascending(r => r.PrepTimeMinutes).Ascending(r => r.CookTimeMinutes))
			.ToListAsync(cancellationToken);
	}

	/// <summary>
	///     Searches recipes by title, description, or ingredients using MongoDB text search
	///     Leverages MongoDB full-text search capabilities for comprehensive results
	/// </summary>
	public async Task<PagedResult<RecipeDocument>> SearchAsync(
		string searchTerm,
		int pageNumber,
		int pageSize,
		CancellationToken cancellationToken = default)
	{
		FilterDefinitionBuilder<RecipeDocument>? filterBuilder = Builders<RecipeDocument>.Filter;

		// Use regex search for title and description
		FilterDefinition<RecipeDocument>? titleFilter = filterBuilder.Regex(r => r.Title, new BsonRegularExpression(searchTerm, "i"));
		FilterDefinition<RecipeDocument>? descriptionFilter = filterBuilder.Regex(r => r.Description, new BsonRegularExpression(searchTerm, "i"));

		FilterDefinition<RecipeDocument>? searchFilter = filterBuilder.Or(titleFilter, descriptionFilter);

		// Combine with active and non-deleted filters
		FilterDefinition<RecipeDocument>? filter = filterBuilder.And(
			searchFilter,
			filterBuilder.Eq(r => r.IsActive, true),
			filterBuilder.Ne(r => r.IsDeleted, true)
		);

		long totalCount = await _collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);

		int skip = (pageNumber - 1) * pageSize;
		List<RecipeDocument>? recipes = await _collection
			.Find(filter)
			.Sort(Builders<RecipeDocument>.Sort.Descending(r => r.CreatedAt))
			.Skip(skip)
			.Limit(pageSize)
			.ToListAsync(cancellationToken);

		return new PagedResult<RecipeDocument>
		{
			Items = recipes,
			TotalCount = (int)totalCount,
			PageNumber = pageNumber,
			PageSize = pageSize
		};
	}

	/// <summary>
	///     Gets recipes by tags with MongoDB array query optimization
	///     Uses $in operator for efficient tag matching
	/// </summary>
	public async Task<PagedResult<RecipeDocument>> GetByTagsAsync(
		IEnumerable<string> tags,
		int pageNumber,
		int pageSize,
		CancellationToken cancellationToken = default)
	{
		List<string> tagList = tags.ToList();
		if (!tagList.Any())
			return new PagedResult<RecipeDocument>
			{
				Items = [],
				TotalCount = 0,
				PageNumber = pageNumber,
				PageSize = pageSize
			};

		FilterDefinitionBuilder<RecipeDocument>? filterBuilder = Builders<RecipeDocument>.Filter;
		FilterDefinition<RecipeDocument>? filter = filterBuilder.And(
			filterBuilder.AnyIn(r => r.Tags, tagList),
			filterBuilder.Eq(r => r.IsActive, true),
			filterBuilder.Ne(r => r.IsDeleted, true)
		);

		long totalCount = await _collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);

		int skip = (pageNumber - 1) * pageSize;
		List<RecipeDocument>? recipes = await _collection
			.Find(filter)
			.Sort(Builders<RecipeDocument>.Sort.Descending(r => r.CreatedAt))
			.Skip(skip)
			.Limit(pageSize)
			.ToListAsync(cancellationToken);

		return new PagedResult<RecipeDocument>
		{
			Items = recipes,
			TotalCount = (int)totalCount,
			PageNumber = pageNumber,
			PageSize = pageSize
		};
	}

	/// <summary>
	///     Gets recipes by cuisine type with case-insensitive matching
	///     Uses MongoDB regex for flexible cuisine filtering
	/// </summary>
	public async Task<PagedResult<RecipeDocument>> GetByCuisineAsync(
		string cuisine,
		int pageNumber,
		int pageSize,
		CancellationToken cancellationToken = default)
	{
		FilterDefinitionBuilder<RecipeDocument>? filterBuilder = Builders<RecipeDocument>.Filter;
		FilterDefinition<RecipeDocument>? filter = filterBuilder.And(
			filterBuilder.Regex(r => r.Cuisine, new BsonRegularExpression($"^{cuisine}$", "i")),
			filterBuilder.Eq(r => r.IsActive, true),
			filterBuilder.Ne(r => r.IsDeleted, true)
		);

		long totalCount = await _collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);

		int skip = (pageNumber - 1) * pageSize;
		List<RecipeDocument>? recipes = await _collection
			.Find(filter)
			.Sort(Builders<RecipeDocument>.Sort.Descending(r => r.Rating).Descending(r => r.CreatedAt))
			.Skip(skip)
			.Limit(pageSize)
			.ToListAsync(cancellationToken);

		return new PagedResult<RecipeDocument>
		{
			Items = recipes,
			TotalCount = (int)totalCount,
			PageNumber = pageNumber,
			PageSize = pageSize
		};
	}

	/// <summary>
	///     Gets recipes created or updated within a specific time range
	///     Uses MongoDB date range queries for efficient temporal filtering
	/// </summary>
	public async Task<IEnumerable<RecipeDocument>> GetByDateRangeAsync(
		DateTime fromDate,
		DateTime toDate,
		CancellationToken cancellationToken = default)
	{
		FilterDefinitionBuilder<RecipeDocument>? filterBuilder = Builders<RecipeDocument>.Filter;
		FilterDefinition<RecipeDocument>? filter = filterBuilder.And(
			filterBuilder.Or(
				filterBuilder.And(
					filterBuilder.Gte(r => r.CreatedAt, fromDate),
					filterBuilder.Lte(r => r.CreatedAt, toDate)
				),
				filterBuilder.And(
					filterBuilder.Gte(r => r.UpdatedAt, fromDate),
					filterBuilder.Lte(r => r.UpdatedAt, toDate)
				)
			),
			filterBuilder.Ne(r => r.IsDeleted, true)
		);

		return await _collection
			.Find(filter)
			.Sort(Builders<RecipeDocument>.Sort.Descending(r => r.UpdatedAt))
			.ToListAsync(cancellationToken);
	}

	/// <summary>
	///     Checks if a recipe exists by source URL to prevent duplicates
	///     Uses MongoDB indexed lookup for optimal performance
	/// </summary>
	public async Task<bool> ExistsBySourceUrlAsync(string sourceUrl, CancellationToken cancellationToken = default)
	{
		FilterDefinition<RecipeDocument>? filter = Builders<RecipeDocument>.Filter.And(
			Builders<RecipeDocument>.Filter.Eq(r => r.SourceUrl, sourceUrl),
			Builders<RecipeDocument>.Filter.Ne(r => r.IsDeleted, true)
		);

		long count = await _collection.CountDocumentsAsync(
			filter,
			new CountOptions { Limit = 1 },
			cancellationToken);

		return count > 0;
	}

	/// <summary>
	///     Gets recipes with nutritional information for health-conscious filtering
	///     Uses MongoDB nested document queries for nutritional criteria
	/// </summary>
	public async Task<PagedResult<RecipeDocument>> GetByNutritionalCriteriaAsync(
		int? maxCalories = null,
		decimal? minProtein = null,
		int pageNumber = 1,
		int pageSize = 20,
		CancellationToken cancellationToken = default)
	{
		FilterDefinitionBuilder<RecipeDocument>? filterBuilder = Builders<RecipeDocument>.Filter;
		FilterDefinition<RecipeDocument>? filter = filterBuilder.And(
			filterBuilder.Eq(r => r.IsActive, true),
			filterBuilder.Ne(r => r.IsDeleted, true),
			filterBuilder.Ne(r => r.NutritionInfo, null)
		);

		if (maxCalories.HasValue)
			filter = filterBuilder.And(filter,
				filterBuilder.Lte("nutritionInfo.calories", maxCalories.Value));

		if (minProtein.HasValue)
			filter = filterBuilder.And(filter,
				filterBuilder.Gte("nutritionInfo.proteinGrams", minProtein.Value));

		long totalCount = await _collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);

		int skip = (pageNumber - 1) * pageSize;
		List<RecipeDocument>? recipes = await _collection
			.Find(filter)
			.Sort(Builders<RecipeDocument>.Sort.Ascending("nutritionInfo.calories"))
			.Skip(skip)
			.Limit(pageSize)
			.ToListAsync(cancellationToken);

		return new PagedResult<RecipeDocument>
		{
			Items = recipes,
			TotalCount = (int)totalCount,
			PageNumber = pageNumber,
			PageSize = pageSize
		};
	}
}