using System.Linq.Expressions;
using EasyMeals.Shared.Data.Documents;

namespace EasyMeals.Shared.Data.Repositories;

/// <summary>
///     Write repository interface for modifying entities
///     Provides comprehensive write operations for MongoDB documents
/// </summary>
/// <typeparam name="TDocument">The document type extending BaseDocument</typeparam>
public interface IWriteRepository<TDocument>
	where TDocument : BaseDocument
{
	/// <summary>
	///     Inserts a single document
	/// </summary>
	/// <param name="document">The document to insert</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>The inserted document with generated ID</returns>
	Task<TDocument> InsertOneAsync(
		TDocument document,
		CancellationToken cancellationToken = default);

	/// <summary>
	///     Inserts multiple documents in a single operation
	/// </summary>
	/// <param name="documents">The documents to insert</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>The inserted documents with generated IDs</returns>
	Task<IEnumerable<TDocument>> InsertManyAsync(
		IEnumerable<TDocument> documents,
		CancellationToken cancellationToken = default);

	/// <summary>
	///     Updates a single document matching the filter
	/// </summary>
	/// <param name="filter">Filter expression to match document</param>
	/// <param name="update">Update definition</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>True if a document was updated</returns>
	Task<bool> UpdateOneAsync(
		Expression<Func<TDocument, bool>> filter,
		object update,
		CancellationToken cancellationToken = default);

	/// <summary>
	///     Updates a document by its identifier
	/// </summary>
	/// <param name="id">The document identifier</param>
	/// <param name="update">Update definition</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>True if the document was updated</returns>
	Task<bool> UpdateByIdAsync(
		string id,
		object update,
		CancellationToken cancellationToken = default);

	/// <summary>
	///     Updates multiple documents matching the filter
	/// </summary>
	/// <param name="filter">Filter expression to match documents</param>
	/// <param name="update">Update definition</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>Number of documents updated</returns>
	Task<long> UpdateManyAsync(
		Expression<Func<TDocument, bool>> filter,
		object update,
		CancellationToken cancellationToken = default);

	/// <summary>
	///     Replaces a single document entirely
	/// </summary>
	/// <param name="filter">Filter expression to match document</param>
	/// <param name="replacement">The replacement document</param>
	/// <param name="upsert">Whether to insert if no match found</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>True if a document was replaced or inserted</returns>
	Task<bool> ReplaceOneAsync(
		Expression<Func<TDocument, bool>> filter,
		TDocument replacement,
		bool upsert = false,
		CancellationToken cancellationToken = default);

	/// <summary>
	///     Replaces a document by its identifier
	/// </summary>
	/// <param name="id">The document identifier</param>
	/// <param name="replacement">The replacement document</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>True if the document was replaced</returns>
	Task<bool> ReplaceByIdAsync(
		string id,
		TDocument replacement,
		CancellationToken cancellationToken = default);

	/// <summary>
	///     Deletes a single document matching the filter
	/// </summary>
	/// <param name="filter">Filter expression to match document</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>True if a document was deleted</returns>
	Task<bool> DeleteOneAsync(
		Expression<Func<TDocument, bool>> filter,
		CancellationToken cancellationToken = default);

	/// <summary>
	///     Deletes a document by its identifier
	/// </summary>
	/// <param name="id">The document identifier</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>True if the document was deleted</returns>
	Task<bool> DeleteByIdAsync(
		string id,
		CancellationToken cancellationToken = default);

	/// <summary>
	///     Deletes multiple documents matching the filter
	/// </summary>
	/// <param name="filter">Filter expression to match documents</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>Number of documents deleted</returns>
	Task<long> DeleteManyAsync(
		Expression<Func<TDocument, bool>> filter,
		CancellationToken cancellationToken = default);

	/// <summary>
	///     Performs an upsert operation (update if exists, insert if not)
	/// </summary>
	/// <param name="filter">Filter expression to match document</param>
	/// <param name="document">The document to upsert</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>True if a document was modified, false if inserted</returns>
	Task<bool> UpsertAsync(
		Expression<Func<TDocument, bool>> filter,
		TDocument document,
		CancellationToken cancellationToken = default);

	/// <summary>
	///     Performs a bulk write operation with multiple operations
	/// </summary>
	/// <param name="operations">Collection of write operations</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>Bulk write result with operation counts</returns>
	Task<(long InsertedCount, long ModifiedCount, long DeletedCount, long UpsertedCount)> BulkWriteAsync(
		IEnumerable<object> operations,
		CancellationToken cancellationToken = default);
}