using EasyMeals.Shared.Data.Common;
using EasyMeals.Shared.Data.Documents.Recipe;
using MongoDB.Bson;
using MongoDB.Driver;

namespace EasyMeals.Shared.Data.Repositories.Recipe;

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

		return new PagedResult<RecipeDocument>(
			recipes,
			(int)totalCount,
			pageNumber,
			pageSize
		);
	}
}