using EasyMeals.Persistence.Abstractions;
using EasyMeals.Persistence.Abstractions.Exceptions;
using EasyMeals.Persistence.Abstractions.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace EasyMeals.Persistence.Mongo.Repositories;

/// <summary>
///     MongoDB implementation of the repository pattern with support for CRUD operations,
///     pagination, soft deletes, optimistic concurrency, and transactions.
/// </summary>
/// <typeparam name="T">The entity type that implements IEntity.</typeparam>
public class MongoRepository<T>(IMongoContext context) : IRepository<T, string>
	where T : class, IEntity
{
	protected readonly IMongoCollection<T> Collection = context.GetCollection<T>();
	protected readonly IMongoContext Context = context ?? throw new ArgumentNullException(nameof(context));

	#region Read Operations

	/// <inheritdoc />
	public virtual async Task<T?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(id)) return null;

		if (!ObjectId.TryParse(id, out ObjectId objectId)) return null;

		FilterDefinition<T> filter = CreateIdFilter(objectId);
		filter = ApplySoftDeleteFilter(filter);

		return await Collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
	}

	/// <inheritdoc />
	public virtual async Task<IReadOnlyList<T>> GetByIdsAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
	{
		List<string> idList = ids?.ToList() ?? [];

		if (idList.Count == 0) return Array.Empty<T>();

		List<ObjectId> objectIds = idList
			.Where(id => ObjectId.TryParse(id, out _))
			.Select(ObjectId.Parse)
			.ToList();

		if (objectIds.Count == 0) return Array.Empty<T>();

		FilterDefinition<T>? filter = Builders<T>.Filter.In("_id", objectIds);
		filter = ApplySoftDeleteFilter(filter);

		return await Collection.Find(filter).ToListAsync(cancellationToken);
	}

	/// <inheritdoc />
	public virtual async Task<IReadOnlyList<T>> ListAsync(CancellationToken cancellationToken = default)
	{
		FilterDefinition<T> filter = ApplySoftDeleteFilter(Builders<T>.Filter.Empty);
		return await Collection.Find(filter).ToListAsync(cancellationToken);
	}

	/// <inheritdoc />
	public virtual async Task<bool> ExistsAsync(string id, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(id) || !ObjectId.TryParse(id, out ObjectId objectId)) return false;

		FilterDefinition<T> filter = CreateIdFilter(objectId);
		filter = ApplySoftDeleteFilter(filter);

		return await Collection.Find(filter).AnyAsync(cancellationToken);
	}

	/// <summary>
	///     Lists entities with pagination support.
	/// </summary>
	public virtual async Task<PagedResult<T>> ListPagedAsync(
		PagedRequest request,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(request);

		FilterDefinition<T> filter = ApplySoftDeleteFilter(Builders<T>.Filter.Empty);

		long totalCount = await Collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);

		List<T>? items = await Collection
			.Find(filter)
			.Skip(request.Skip)
			.Limit(request.Take)
			.ToListAsync(cancellationToken);

		return new PagedResult<T>(items, totalCount, request.Page, request.PageSize);
	}

	#endregion

	#region Write Operations

	/// <inheritdoc />
	public virtual async Task<string> AddAsync(T entity, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(entity);

		PrepareEntityForInsert(entity);

		await Collection.InsertOneAsync(entity, cancellationToken: cancellationToken);

		return entity.Id;
	}

	/// <inheritdoc />
	public virtual async Task AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
	{
		List<T> entityList = entities?.ToList() ?? throw new ArgumentNullException(nameof(entities));

		if (entityList.Count == 0) return;

		foreach (T entity in entityList)
		{
			PrepareEntityForInsert(entity);
		}

		await Collection.InsertManyAsync(entityList, cancellationToken: cancellationToken);
	}

	/// <inheritdoc />
	public virtual async Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(entity);

		if (string.IsNullOrWhiteSpace(entity.Id) || !ObjectId.TryParse(entity.Id, out ObjectId objectId))
			throw new ArgumentException("Entity must have a valid Id.", nameof(entity));

		FilterDefinition<T>? filter = CreateIdFilter(objectId);

		// Apply optimistic concurrency check
		if (entity is IOptimisticConcurrency concurrentEntity)
		{
			long currentToken = concurrentEntity.ConcurrencyToken;
			filter = Builders<T>.Filter.And(
				filter,
				Builders<T>.Filter.Eq(nameof(IOptimisticConcurrency.ConcurrencyToken), currentToken)
			);

			concurrentEntity.ConcurrencyToken++;
		}

		// Update audit fields
		if (entity is IAuditableEntity auditableEntity) auditableEntity.MarkAsModified();

		ReplaceOneResult? result = await Collection.ReplaceOneAsync(
			filter,
			entity,
			new ReplaceOptions { IsUpsert = false },
			cancellationToken);

		if (result.MatchedCount == 0)
			throw new ConcurrencyException(
				entity.Id,
				typeof(T).Name);
	}

	/// <inheritdoc />
	public virtual async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(id) || !ObjectId.TryParse(id, out ObjectId objectId))
			throw new ArgumentException("Invalid entity ID.", nameof(id));

		T? entity = await GetByIdAsync(id, cancellationToken);

		if (entity == null) return;

		// Soft delete if supported
		if (entity is ISoftDeletableEntity softDeletable)
		{
			softDeletable.SoftDelete();
			await UpdateAsync(entity, cancellationToken);
		}
		else
		{
			// Hard delete
			FilterDefinition<T> filter = CreateIdFilter(objectId);
			await Collection.DeleteOneAsync(filter, cancellationToken);
		}
	}

	/// <inheritdoc />
	public virtual async Task DeleteRangeAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
	{
		List<string> idList = ids?.ToList() ?? throw new ArgumentNullException(nameof(ids));

		if (idList.Count == 0) return;

		foreach (string id in idList)
		{
			await DeleteAsync(id, cancellationToken);
		}
	}

	/// <summary>
	///     Permanently deletes an entity (bypasses soft delete).
	/// </summary>
	public virtual async Task HardDeleteAsync(string id, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(id) || !ObjectId.TryParse(id, out ObjectId objectId))
			throw new ArgumentException("Invalid entity ID.", nameof(id));

		FilterDefinition<T> filter = CreateIdFilter(objectId);
		await Collection.DeleteOneAsync(filter, cancellationToken);
	}

	#endregion

	#region Helper Methods

	/// <summary>
	///     Creates a filter for finding a document by ObjectId.
	/// </summary>
	protected virtual FilterDefinition<T> CreateIdFilter(ObjectId objectId) => Builders<T>.Filter.Eq("_id", objectId);

	/// <summary>
	///     Applies soft delete filter if the entity supports soft deletion.
	/// </summary>
	protected virtual FilterDefinition<T> ApplySoftDeleteFilter(FilterDefinition<T> filter)
	{
		if (!typeof(ISoftDeletableEntity).IsAssignableFrom(typeof(T)))
			return filter;

		FilterDefinition<T>? notDeletedFilter = Builders<T>.Filter.Eq(
			nameof(ISoftDeletableEntity.IsDeleted),
			false);

		return Builders<T>.Filter.And(filter, notDeletedFilter);
	}

	/// <summary>
	///     Prepares an entity for insertion by setting audit fields.
	/// </summary>
	protected virtual void PrepareEntityForInsert(T entity)
	{
		if (entity is IAuditableEntity auditable)
		{
			DateTime now = DateTime.UtcNow;
			auditable.CreatedAt = now;
			auditable.UpdatedAt = now;
		}

		if (entity is IOptimisticConcurrency concurrent)
			concurrent.ConcurrencyToken = 0;

		if (entity is IVersionedEntity { Version: 0 } versioned)
			versioned.Version = 1;
	}

	#endregion
}