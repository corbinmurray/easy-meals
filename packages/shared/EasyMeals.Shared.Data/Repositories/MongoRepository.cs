using System.Linq.Expressions;
using MongoDB.Driver;
using EasyMeals.Shared.Data.Documents;
using EasyMeals.Shared.Data.Attributes;

namespace EasyMeals.Shared.Data.Repositories;

/// <summary>
/// MongoDB repository implementation following Repository pattern from DDD
/// Provides optimized MongoDB operations while maintaining aggregate boundaries
/// Implements both generic repository interface and MongoDB-specific operations
/// </summary>
/// <typeparam name="TDocument">The document type managed by this repository</typeparam>
public class MongoRepository<TDocument> : IMongoRepository<TDocument>
    where TDocument : BaseDocument
{
    protected readonly IMongoCollection<TDocument> _collection;
    protected readonly IClientSessionHandle? _session;

    public MongoRepository(IMongoDatabase database, IClientSessionHandle? session = null)
    {
        var collectionName = GetCollectionName();
        _collection = database.GetCollection<TDocument>(collectionName);
        _session = session;
    }

    /// <summary>
    /// Gets the MongoDB collection name for the document type
    /// Uses BsonCollection attribute or derives from type name
    /// </summary>
    private static string GetCollectionName()
    {
        var attribute = typeof(TDocument).GetCustomAttributes(typeof(BsonCollectionAttribute), true)
            .FirstOrDefault() as BsonCollectionAttribute;

        return attribute?.CollectionName ?? typeof(TDocument).Name.ToLowerInvariant();
    }

    public virtual async Task<TDocument?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<TDocument>.Filter.Eq(doc => doc.Id, id);

        if (typeof(ISoftDeletableDocument).IsAssignableFrom(typeof(TDocument)))
        {
            filter = Builders<TDocument>.Filter.And(filter,
                Builders<TDocument>.Filter.Ne("isDeleted", true));
        }

        return _session != null
            ? await _collection.Find(_session, filter).FirstOrDefaultAsync(cancellationToken)
            : await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public virtual async Task<IEnumerable<TDocument>> GetAllAsync(
        Expression<Func<TDocument, bool>>? predicate = null,
        CancellationToken cancellationToken = default)
    {
        var filter = predicate != null
            ? Builders<TDocument>.Filter.Where(predicate)
            : Builders<TDocument>.Filter.Empty;

        if (typeof(ISoftDeletableDocument).IsAssignableFrom(typeof(TDocument)))
        {
            filter = Builders<TDocument>.Filter.And(filter,
                Builders<TDocument>.Filter.Ne("isDeleted", true));
        }

        var cursor = _session != null
            ? await _collection.FindAsync(_session, filter, cancellationToken: cancellationToken)
            : await _collection.FindAsync(filter, cancellationToken: cancellationToken);

        return await cursor.ToListAsync(cancellationToken);
    }

    public virtual async Task<PagedResult<TDocument>> GetPagedAsync(
        int pageNumber,
        int pageSize,
        Expression<Func<TDocument, bool>>? predicate = null,
        CancellationToken cancellationToken = default)
    {
        var filter = predicate != null
            ? Builders<TDocument>.Filter.Where(predicate)
            : Builders<TDocument>.Filter.Empty;

        if (typeof(ISoftDeletableDocument).IsAssignableFrom(typeof(TDocument)))
        {
            filter = Builders<TDocument>.Filter.And(filter,
                Builders<TDocument>.Filter.Ne("isDeleted", true));
        }

        var totalCount = _session != null
            ? await _collection.CountDocumentsAsync(_session, filter, cancellationToken: cancellationToken)
            : await _collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);

        var skip = (pageNumber - 1) * pageSize;
        var findOptions = new FindOptions<TDocument>
        {
            Skip = skip,
            Limit = pageSize
        };

        var cursor = _session != null
            ? await _collection.FindAsync(_session, filter, findOptions, cancellationToken)
            : await _collection.FindAsync(filter, findOptions, cancellationToken);

        var items = await cursor.ToListAsync(cancellationToken);

        return new PagedResult<TDocument>
        {
            Items = items,
            TotalCount = (int)totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public virtual async Task<TDocument?> FirstOrDefaultAsync(
        Expression<Func<TDocument, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<TDocument>.Filter.Where(predicate);

        if (typeof(ISoftDeletableDocument).IsAssignableFrom(typeof(TDocument)))
        {
            filter = Builders<TDocument>.Filter.And(filter,
                Builders<TDocument>.Filter.Ne("isDeleted", true));
        }

        return _session != null
            ? await _collection.Find(_session, filter).FirstOrDefaultAsync(cancellationToken)
            : await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public virtual async Task<bool> AnyAsync(
        Expression<Func<TDocument, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<TDocument>.Filter.Where(predicate);

        if (typeof(ISoftDeletableDocument).IsAssignableFrom(typeof(TDocument)))
        {
            filter = Builders<TDocument>.Filter.And(filter,
                Builders<TDocument>.Filter.Ne("isDeleted", true));
        }

        var count = _session != null
            ? await _collection.CountDocumentsAsync(_session, filter, new CountOptions { Limit = 1 }, cancellationToken)
            : await _collection.CountDocumentsAsync(filter, new CountOptions { Limit = 1 }, cancellationToken);

        return count > 0;
    }

    public virtual async Task<int> CountAsync(
        Expression<Func<TDocument, bool>>? predicate = null,
        CancellationToken cancellationToken = default)
    {
        var filter = predicate != null
            ? Builders<TDocument>.Filter.Where(predicate)
            : Builders<TDocument>.Filter.Empty;

        if (typeof(ISoftDeletableDocument).IsAssignableFrom(typeof(TDocument)))
        {
            filter = Builders<TDocument>.Filter.And(filter,
                Builders<TDocument>.Filter.Ne("isDeleted", true));
        }

        var count = _session != null
            ? await _collection.CountDocumentsAsync(_session, filter, cancellationToken: cancellationToken)
            : await _collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);

        return (int)count;
    }

    public virtual async Task AddAsync(TDocument entity, CancellationToken cancellationToken = default)
    {
        entity.MarkAsModified();

        if (_session != null)
            await _collection.InsertOneAsync(_session, entity, cancellationToken: cancellationToken);
        else
            await _collection.InsertOneAsync(entity, cancellationToken: cancellationToken);
    }

    public virtual async Task AddRangeAsync(IEnumerable<TDocument> entities, CancellationToken cancellationToken = default)
    {
        var documents = entities.ToList();
        foreach (var doc in documents)
        {
            doc.MarkAsModified();
        }

        if (documents.Count > 0)
        {
            if (_session != null)
                await _collection.InsertManyAsync(_session, documents, cancellationToken: cancellationToken);
            else
                await _collection.InsertManyAsync(documents, cancellationToken: cancellationToken);
        }
    }

    public virtual void Update(TDocument entity)
    {
        entity.MarkAsModified();
        var filter = Builders<TDocument>.Filter.Eq(doc => doc.Id, entity.Id);

        if (_session != null)
            _collection.ReplaceOne(_session, filter, entity);
        else
            _collection.ReplaceOne(filter, entity);
    }

    public virtual void UpdateRange(IEnumerable<TDocument> entities)
    {
        foreach (var entity in entities)
        {
            Update(entity);
        }
    }

    public virtual void Remove(TDocument entity)
    {
        var filter = Builders<TDocument>.Filter.Eq(doc => doc.Id, entity.Id);

        if (_session != null)
            _collection.DeleteOne(_session, filter);
        else
            _collection.DeleteOne(filter);
    }

    public virtual void RemoveRange(IEnumerable<TDocument> entities)
    {
        var ids = entities.Select(e => e.Id).ToList();
        var filter = Builders<TDocument>.Filter.In(doc => doc.Id, ids);

        if (_session != null)
            _collection.DeleteMany(_session, filter);
        else
            _collection.DeleteMany(filter);
    }

    public virtual async Task<IEnumerable<TProjection>> GetWithProjectionAsync<TProjection>(
        Expression<Func<TDocument, bool>>? filter = null,
        Expression<Func<TDocument, TProjection>>? projection = null,
        CancellationToken cancellationToken = default)
    {
        var mongoFilter = filter != null
            ? Builders<TDocument>.Filter.Where(filter)
            : Builders<TDocument>.Filter.Empty;

        if (typeof(ISoftDeletableDocument).IsAssignableFrom(typeof(TDocument)))
        {
            mongoFilter = Builders<TDocument>.Filter.And(mongoFilter,
                Builders<TDocument>.Filter.Ne("isDeleted", true));
        }

        var findFluent = _session != null
            ? _collection.Find(_session, mongoFilter)
            : _collection.Find(mongoFilter);

        if (projection != null)
        {
            return await findFluent.Project(projection).ToListAsync(cancellationToken);
        }

        // If no projection specified, return the documents as TProjection (requires compatible types)
        var cursor = await findFluent.ToListAsync(cancellationToken);
        return cursor.Cast<TProjection>();
    }

    public virtual async Task<long> BulkUpdateAsync(
        Expression<Func<TDocument, bool>> filter,
        object update,
        CancellationToken cancellationToken = default)
    {
        var mongoFilter = Builders<TDocument>.Filter.Where(filter);
        var updateDefinition = Builders<TDocument>.Update.Set("updatedAt", DateTime.UtcNow);

        // This is a simplified implementation - in practice, you'd want to handle
        // different update types more robustly
        var result = _session != null
            ? await _collection.UpdateManyAsync(_session, mongoFilter, updateDefinition, cancellationToken: cancellationToken)
            : await _collection.UpdateManyAsync(mongoFilter, updateDefinition, cancellationToken: cancellationToken);

        return result.ModifiedCount;
    }

    public virtual async Task<IEnumerable<TResult>> AggregateAsync<TResult>(
        object[] pipeline,
        CancellationToken cancellationToken = default)
    {
        // Convert the pipeline objects to BsonDocuments
        var bsonPipeline = pipeline.Select(stage => MongoDB.Bson.BsonDocument.Parse(stage.ToString()!)).ToArray();

        var cursor = _session != null
            ? await _collection.AggregateAsync<TResult>(_session, bsonPipeline, cancellationToken: cancellationToken)
            : await _collection.AggregateAsync<TResult>(bsonPipeline, cancellationToken: cancellationToken);

        return await cursor.ToListAsync(cancellationToken);
    }
}
