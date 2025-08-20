using System.Linq.Expressions;
using EasyMeals.Shared.Data.Documents;

namespace EasyMeals.Shared.Data.Repositories;

/// <summary>
///     MongoDB-specific repository interface extending the generic repository pattern
///     Provides MongoDB-optimized operations while maintaining DDD principles
/// </summary>
/// <typeparam name="TDocument">The document type managed by this repository</typeparam>
public interface IRepository<TDocument>
	where TDocument : BaseDocument
{
	/// <summary>
	///     Gets documents with MongoDB-specific projection support
	///     Allows field selection for performance optimization
	/// </summary>
	/// <typeparam name="TProjection">The projection type</typeparam>
	/// <param name="filter">Filter expression</param>
	/// <param name="projection">Projection expression</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>Projected results</returns>
	Task<IEnumerable<TProjection>> GetWithProjectionAsync<TProjection>(
		Expression<Func<TDocument, bool>>? filter = null,
		Expression<Func<TDocument, TProjection>>? projection = null,
		CancellationToken cancellationToken = default);

	/// <summary>
	///     Performs bulk update operations
	///     Optimized for large-scale data modifications
	/// </summary>
	/// <param name="filter">Filter for documents to update</param>
	/// <param name="update">Update definition</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>Number of documents modified</returns>
	Task<long> BulkUpdateAsync(
		Expression<Func<TDocument, bool>> filter,
		object update,
		CancellationToken cancellationToken = default);

	/// <summary>
	///     Performs MongoDB aggregation operations
	///     Supports complex data processing pipelines
	/// </summary>
	/// <typeparam name="TResult">Result type</typeparam>
	/// <param name="pipeline">Aggregation pipeline</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>Aggregation results</returns>
	Task<IEnumerable<TResult>> AggregateAsync<TResult>(
		object[] pipeline,
		CancellationToken cancellationToken = default);
}