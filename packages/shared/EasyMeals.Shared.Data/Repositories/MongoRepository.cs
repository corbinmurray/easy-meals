using System.Linq.Expressions;
using EasyMeals.Shared.Data.Attributes;
using EasyMeals.Shared.Data.Documents;
using MongoDB.Bson;
using MongoDB.Driver;

namespace EasyMeals.Shared.Data.Repositories;

/// <summary>
///     MongoDB repository implementation following Repository pattern from DDD
///     Provides optimized MongoDB operations while maintaining aggregate boundaries
///     Implements both generic repository interface and MongoDB-specific operations
/// </summary>
/// <typeparam name="TDocument">The document type managed by this repository</typeparam>
public class MongoRepository<TDocument> : IMongoRepository<TDocument>
	where TDocument : BaseDocument
{
	protected readonly IMongoCollection<TDocument> _collection;
	protected readonly IClientSessionHandle? _session;

	public MongoRepository(IMongoDatabase database, IClientSessionHandle? session = null)
	{
		string collectionName = GetCollectionName();
		_collection = database.GetCollection<TDocument>(collectionName);
		_session = session;
	}

	/// <summary>
	///     Gets the MongoDB collection name for the document type
	///     Uses BsonCollection attribute or derives from type name
	/// </summary>
	private static string GetCollectionName()
	{
		var attribute = typeof(TDocument).GetCustomAttributes(typeof(BsonCollectionAttribute), true)
			.FirstOrDefault() as BsonCollectionAttribute;

		return attribute?.CollectionName ?? typeof(TDocument).Name.ToLowerInvariant();
	}

	#region Read Operations

	/// <inheritdoc />
	public async Task<TDocument?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(id, nameof(id));

		FilterDefinition<TDocument>? filter = Builders<TDocument>.Filter.Eq(doc => doc.Id, id);

		return _session is not null
			? await _collection.Find(_session, filter).FirstOrDefaultAsync(cancellationToken)
			: await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
	}

	/// <inheritdoc />
	public async Task<IEnumerable<TDocument>> GetAllAsync(
		Expression<Func<TDocument, bool>>? filter = null,
		CancellationToken cancellationToken = default)
	{
		FilterDefinition<TDocument>? filterDefinition = filter is not null
			? Builders<TDocument>.Filter.Where(filter)
			: Builders<TDocument>.Filter.Empty;

		return _session is not null
			? await _collection.Find(_session, filterDefinition).ToListAsync(cancellationToken)
			: await _collection.Find(filterDefinition).ToListAsync(cancellationToken);
	}

	/// <inheritdoc />
	public async Task<TDocument?> GetFirstOrDefaultAsync(
		Expression<Func<TDocument, bool>> filter,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(filter, nameof(filter));

		FilterDefinition<TDocument>? filterDefinition = Builders<TDocument>.Filter.Where(filter);

		return _session is not null
			? await _collection.Find(_session, filterDefinition).FirstOrDefaultAsync(cancellationToken)
			: await _collection.Find(filterDefinition).FirstOrDefaultAsync(cancellationToken);
	}

	/// <inheritdoc />
	public async Task<(IEnumerable<TDocument> Items, long TotalCount)> GetPagedAsync(
		Expression<Func<TDocument, bool>>? filter = null,
		Expression<Func<TDocument, object>>? sortBy = null,
		int sortDirection = 1,
		int pageNumber = 1,
		int pageSize = 20,
		CancellationToken cancellationToken = default)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(pageNumber, 1, nameof(pageNumber));
		ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1, nameof(pageSize));

		FilterDefinition<TDocument>? filterDefinition = filter is not null
			? Builders<TDocument>.Filter.Where(filter)
			: Builders<TDocument>.Filter.Empty;

		// Get total count for pagination info
		long totalCount = _session is not null
			? await _collection.CountDocumentsAsync(_session, filterDefinition, cancellationToken: cancellationToken)
			: await _collection.CountDocumentsAsync(filterDefinition, cancellationToken: cancellationToken);

		IFindFluent<TDocument, TDocument>? findFluent = _session is not null
			? _collection.Find(_session, filterDefinition)
			: _collection.Find(filterDefinition);

		findFluent = findFluent.Skip((pageNumber - 1) * pageSize).Limit(pageSize);

		// Apply sorting if specified
		if (sortBy is not null)
			findFluent = sortDirection > 0
				? findFluent.SortBy(sortBy)
				: findFluent.SortByDescending(sortBy);

		List<TDocument> items = await findFluent.ToListAsync(cancellationToken);

		return (items, totalCount);
	}

	/// <inheritdoc />
	public async Task<long> CountAsync(
		Expression<Func<TDocument, bool>>? filter = null,
		CancellationToken cancellationToken = default)
	{
		FilterDefinition<TDocument>? filterDefinition = filter is not null
			? Builders<TDocument>.Filter.Where(filter)
			: Builders<TDocument>.Filter.Empty;

		return _session is not null
			? await _collection.CountDocumentsAsync(_session, filterDefinition, cancellationToken: cancellationToken)
			: await _collection.CountDocumentsAsync(filterDefinition, cancellationToken: cancellationToken);
	}

	/// <inheritdoc />
	public async Task<bool> ExistsAsync(
		Expression<Func<TDocument, bool>> filter,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(filter, nameof(filter));

		FilterDefinition<TDocument>? filterDefinition = Builders<TDocument>.Filter.Where(filter);
		var findOptions = new FindOptions<TDocument> { Limit = 1 };

		IAsyncCursor<TDocument>? cursor = _session is not null
			? await _collection.FindAsync(_session, filterDefinition, findOptions, cancellationToken)
			: await _collection.FindAsync(filterDefinition, findOptions, cancellationToken);

		return await cursor.AnyAsync(cancellationToken);
	}

	/// <inheritdoc />
	public async Task<IEnumerable<TDocument>> GetByIdsAsync(
		IEnumerable<string> ids,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(ids, nameof(ids));

		List<string> idsList = ids.ToList();
		if (idsList.Count == 0)
			return [];

		FilterDefinition<TDocument>? filter = Builders<TDocument>.Filter.In(doc => doc.Id, idsList);

		return _session is not null
			? await _collection.Find(_session, filter).ToListAsync(cancellationToken)
			: await _collection.Find(filter).ToListAsync(cancellationToken);
	}

	#endregion

	#region Write Operations

	/// <inheritdoc />
	public async Task<TDocument> InsertOneAsync(TDocument document, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(document, nameof(document));

		// Set audit fields for new document
		document.CreatedAt = DateTime.UtcNow;
		document.UpdatedAt = document.CreatedAt;

		// Generate ID if not already set
		if (string.IsNullOrWhiteSpace(document.Id))
			document.Id = ObjectId.GenerateNewId().ToString();

		if (_session is not null)
			await _collection.InsertOneAsync(_session, document, cancellationToken: cancellationToken);
		else
			await _collection.InsertOneAsync(document, cancellationToken: cancellationToken);

		return document;
	}

	/// <inheritdoc />
	public async Task<IEnumerable<TDocument>> InsertManyAsync(
		IEnumerable<TDocument> documents,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(documents, nameof(documents));

		List<TDocument> documentsList = documents.ToList();
		if (documentsList.Count == 0)
			return [];

		DateTime now = DateTime.UtcNow;

		// Set audit fields and IDs for all documents
		foreach (TDocument document in documentsList)
		{
			document.CreatedAt = now;
			document.UpdatedAt = now;

			if (string.IsNullOrWhiteSpace(document.Id))
				document.Id = ObjectId.GenerateNewId().ToString();
		}

		if (_session is not null)
			await _collection.InsertManyAsync(_session, documentsList, cancellationToken: cancellationToken);
		else
			await _collection.InsertManyAsync(documentsList, cancellationToken: cancellationToken);

		return documentsList;
	}

	/// <inheritdoc />
	public async Task<bool> UpdateOneAsync(
		Expression<Func<TDocument, bool>> filter,
		object update,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(filter, nameof(filter));
		ArgumentNullException.ThrowIfNull(update, nameof(update));

		FilterDefinition<TDocument>? filterDefinition = Builders<TDocument>.Filter.Where(filter);
		UpdateDefinition<TDocument> updateDefinition = BuildUpdateDefinition(update);

		UpdateResult? result = _session is not null
			? await _collection.UpdateOneAsync(_session, filterDefinition, updateDefinition, cancellationToken: cancellationToken)
			: await _collection.UpdateOneAsync(filterDefinition, updateDefinition, cancellationToken: cancellationToken);

		return result.ModifiedCount > 0;
	}

	/// <inheritdoc />
	public async Task<bool> UpdateByIdAsync(string id, object update, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(id, nameof(id));
		ArgumentNullException.ThrowIfNull(update, nameof(update));

		FilterDefinition<TDocument>? filter = Builders<TDocument>.Filter.Eq(doc => doc.Id, id);
		UpdateDefinition<TDocument> updateDefinition = BuildUpdateDefinition(update);

		UpdateResult? result = _session is not null
			? await _collection.UpdateOneAsync(_session, filter, updateDefinition, cancellationToken: cancellationToken)
			: await _collection.UpdateOneAsync(filter, updateDefinition, cancellationToken: cancellationToken);

		return result.ModifiedCount > 0;
	}

	/// <inheritdoc />
	public async Task<long> UpdateManyAsync(
		Expression<Func<TDocument, bool>> filter,
		object update,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(filter, nameof(filter));
		ArgumentNullException.ThrowIfNull(update, nameof(update));

		FilterDefinition<TDocument>? filterDefinition = Builders<TDocument>.Filter.Where(filter);
		UpdateDefinition<TDocument> updateDefinition = BuildUpdateDefinition(update);

		UpdateResult? result = _session is not null
			? await _collection.UpdateManyAsync(_session, filterDefinition, updateDefinition, cancellationToken: cancellationToken)
			: await _collection.UpdateManyAsync(filterDefinition, updateDefinition, cancellationToken: cancellationToken);

		return result.ModifiedCount;
	}

	/// <inheritdoc />
	public async Task<bool> ReplaceOneAsync(
		Expression<Func<TDocument, bool>> filter,
		TDocument replacement,
		bool upsert = false,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(filter, nameof(filter));
		ArgumentNullException.ThrowIfNull(replacement, nameof(replacement));

		FilterDefinition<TDocument>? filterDefinition = Builders<TDocument>.Filter.Where(filter);
		var options = new ReplaceOptions { IsUpsert = upsert };

		// Update audit fields
		replacement.UpdatedAt = DateTime.UtcNow;
		if (upsert && replacement.CreatedAt == default)
			replacement.CreatedAt = replacement.UpdatedAt;

		ReplaceOneResult? result = _session is not null
			? await _collection.ReplaceOneAsync(_session, filterDefinition, replacement, options, cancellationToken)
			: await _collection.ReplaceOneAsync(filterDefinition, replacement, options, cancellationToken);

		return result.ModifiedCount > 0 || result.UpsertedId is not null;
	}

	/// <inheritdoc />
	public async Task<bool> ReplaceByIdAsync(string id, TDocument replacement, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(id, nameof(id));
		ArgumentNullException.ThrowIfNull(replacement, nameof(replacement));

		FilterDefinition<TDocument>? filter = Builders<TDocument>.Filter.Eq(doc => doc.Id, id);

		// Preserve ID and update audit fields
		replacement.Id = id;
		replacement.UpdatedAt = DateTime.UtcNow;

		ReplaceOneResult? result = _session is not null
			? await _collection.ReplaceOneAsync(_session, filter, replacement, cancellationToken: cancellationToken)
			: await _collection.ReplaceOneAsync(filter, replacement, cancellationToken: cancellationToken);

		return result.ModifiedCount > 0;
	}

	/// <inheritdoc />
	public async Task<bool> DeleteOneAsync(
		Expression<Func<TDocument, bool>> filter,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(filter, nameof(filter));

		FilterDefinition<TDocument>? filterDefinition = Builders<TDocument>.Filter.Where(filter);

		DeleteResult? result = _session is not null
			? await _collection.DeleteOneAsync(_session, filterDefinition, cancellationToken: cancellationToken)
			: await _collection.DeleteOneAsync(filterDefinition, cancellationToken);

		return result.DeletedCount > 0;
	}

	/// <inheritdoc />
	public async Task<bool> DeleteByIdAsync(string id, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(id, nameof(id));

		FilterDefinition<TDocument>? filter = Builders<TDocument>.Filter.Eq(doc => doc.Id, id);

		DeleteResult? result = _session is not null
			? await _collection.DeleteOneAsync(_session, filter, cancellationToken: cancellationToken)
			: await _collection.DeleteOneAsync(filter, cancellationToken);

		return result.DeletedCount > 0;
	}

	/// <inheritdoc />
	public async Task<long> DeleteManyAsync(
		Expression<Func<TDocument, bool>> filter,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(filter, nameof(filter));

		FilterDefinition<TDocument>? filterDefinition = Builders<TDocument>.Filter.Where(filter);

		DeleteResult? result = _session is not null
			? await _collection.DeleteManyAsync(_session, filterDefinition, cancellationToken: cancellationToken)
			: await _collection.DeleteManyAsync(filterDefinition, cancellationToken);

		return result.DeletedCount;
	}

	/// <inheritdoc />
	public async Task<bool> UpsertAsync(
		Expression<Func<TDocument, bool>> filter,
		TDocument document,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(filter, nameof(filter));
		ArgumentNullException.ThrowIfNull(document, nameof(document));

		// Set audit fields
		document.UpdatedAt = DateTime.UtcNow;

		FilterDefinition<TDocument>? filterDefinition = Builders<TDocument>.Filter.Where(filter);
		var options = new ReplaceOptions { IsUpsert = true };

		ReplaceOneResult? result = _session is not null
			? await _collection.ReplaceOneAsync(_session, filterDefinition, document, options, cancellationToken)
			: await _collection.ReplaceOneAsync(filterDefinition, document, options, cancellationToken);

		return result.ModifiedCount > 0;
	}

	/// <inheritdoc />
	public async Task<(long InsertedCount, long ModifiedCount, long DeletedCount, long UpsertedCount)> BulkWriteAsync(
		IEnumerable<object> operations,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(operations, nameof(operations));

		var writeModels = new List<WriteModel<TDocument>>();

		foreach (object operation in operations)
		{
			WriteModel<TDocument>? writeModel = BuildWriteModel(operation);
			if (writeModel is not null)
				writeModels.Add(writeModel);
		}

		if (writeModels.Count == 0)
			return (0, 0, 0, 0);

		BulkWriteResult<TDocument>? result = _session is not null
			? await _collection.BulkWriteAsync(_session, writeModels, cancellationToken: cancellationToken)
			: await _collection.BulkWriteAsync(writeModels, cancellationToken: cancellationToken);

		return (result.InsertedCount, result.ModifiedCount, result.DeletedCount, result.Upserts.Count);
	}

	#endregion

	#region Helper Methods

	/// <summary>
	///     Builds an UpdateDefinition from various update object types
	/// </summary>
	private UpdateDefinition<TDocument> BuildUpdateDefinition(object update)
	{
		return update switch
		{
			UpdateDefinition<TDocument> updateDef => updateDef.Set(doc => doc.UpdatedAt, DateTime.UtcNow),
			BsonDocument bsonDoc => new BsonDocumentUpdateDefinition<TDocument>(bsonDoc.Add("updatedAt", DateTime.UtcNow)),
			string json => new JsonUpdateDefinition<TDocument>(json),
			_ => throw new ArgumentException($"Unsupported update type: {update.GetType().Name}", nameof(update))
		};
	}

	/// <summary>
	///     Builds a WriteModel from operation object for bulk operations
	/// </summary>
	private WriteModel<TDocument>? BuildWriteModel(object operation)
	{
		// This is a simplified implementation. In a real scenario, you'd need to define
		// specific operation classes or use a more sophisticated parsing mechanism
		if (operation is InsertOneModel<TDocument> insertModel)
		{
			insertModel.Document.CreatedAt = DateTime.UtcNow;
			insertModel.Document.UpdatedAt = insertModel.Document.CreatedAt;
			return insertModel;
		}

		if (operation is UpdateOneModel<TDocument> updateModel) return updateModel;

		if (operation is DeleteOneModel<TDocument> deleteModel) return deleteModel;

		if (operation is ReplaceOneModel<TDocument> replaceModel)
		{
			replaceModel.Replacement.UpdatedAt = DateTime.UtcNow;
			return replaceModel;
		}

		// For custom operation objects, you could implement a pattern like:
		// if (operation is CustomOperation customOp) { ... }

		return null;
	}

	#endregion
}