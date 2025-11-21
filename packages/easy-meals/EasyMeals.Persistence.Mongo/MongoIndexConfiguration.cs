using System.Reflection;
using EasyMeals.Persistence.Abstractions;
using EasyMeals.Persistence.Mongo.Attributes;
using EasyMeals.Persistence.Mongo.Documents;
using MongoDB.Bson;
using MongoDB.Driver;

namespace EasyMeals.Persistence.Mongo;

/// <summary>
///     Provides MongoDB index configuration helpers for documents in the EasyMeals.Persistence.Mongo project.
/// </summary>
public static class MongoIndexConfiguration
{
	/// <summary>
	///     Creates default indexes used by base documents for all document types discovered in the assembly.
	/// </summary>
	/// <param name="database">The Mongo database instance.</param>
	public static Task CreateBaseDocumentIndexesAsync(IMongoDatabase database)
	{
		ArgumentNullException.ThrowIfNull(database);

		// Discover document types in this assembly that derive from BaseDocument
		Assembly assembly = Assembly.GetAssembly(typeof(BaseDocument)) ?? Assembly.GetExecutingAssembly();
		Type[] docTypes = assembly.GetTypes()
			.Where(t => typeof(BaseDocument).IsAssignableFrom(t) && !t.IsAbstract)
			.ToArray();

		return CreateBaseDocumentIndexesAsync(database, docTypes);
	}

	/// <summary>
	///     Creates default indexes using an IServiceProvider. This keeps callers from having to reference the
	///     MongoDB.Driver types (IMongoDatabase) directly and will discover document types automatically.
	/// </summary>
	/// <param name="serviceProvider">ServiceProvider that can resolve IMongoDatabase.</param>
	public static Task CreateBaseDocumentIndexesAsync(IServiceProvider serviceProvider)
	{
		ArgumentNullException.ThrowIfNull(serviceProvider);

		return serviceProvider.GetService(typeof(IMongoDatabase)) is not IMongoDatabase database
			? throw new InvalidOperationException("IMongoDatabase not registered in the service provider.")
			: CreateBaseDocumentIndexesAsync(database);
	}

	/// <summary>
	///     Creates default indexes using an IServiceProvider and an explicit set of document types.
	///     This is intended for use by builders that track which document types were registered.
	/// </summary>
	public static async Task CreateBaseDocumentIndexesAsync(IServiceProvider serviceProvider, IEnumerable<Type> documentTypes)
	{
		ArgumentNullException.ThrowIfNull(serviceProvider);
		ArgumentNullException.ThrowIfNull(documentTypes);

		if (serviceProvider.GetService(typeof(IMongoDatabase)) is not IMongoDatabase database)
			throw new InvalidOperationException("IMongoDatabase not registered in the service provider.");

		await CreateBaseDocumentIndexesAsync(database, documentTypes);
	}

	/// <summary>
	///     Creates default indexes used by base documents for the provided document types.
	///     Uses non-generic collection access (BsonDocument) so callers do not need the generic type at compile time.
	/// </summary>
	public static async Task CreateBaseDocumentIndexesAsync(IMongoDatabase database, IEnumerable<Type> documentTypes)
	{
		ArgumentNullException.ThrowIfNull(database);
		ArgumentNullException.ThrowIfNull(documentTypes);

		Type[] types = documentTypes.Distinct().ToArray();
		foreach (Type documentType in types)
		{
			// Resolve collection name from attribute if present, otherwise default to type name
			var attr = documentType.GetCustomAttribute<BsonCollectionAttribute>();
			string collectionName = attr?.CollectionName ?? documentType.Name;

			var indexModels = new List<CreateIndexModel<BsonDocument>>();

			// Auditable entity indexes (createdAt, updatedAt)
			if (typeof(IAuditableEntity).IsAssignableFrom(documentType))
			{
				string createdAtFieldName = BsonFieldNames.Get((BaseDocument d) => d.CreatedAt);
				string updatedAtFieldName = BsonFieldNames.Get((BaseDocument d) => d.UpdatedAt);

				indexModels.Add(new CreateIndexModel<BsonDocument>(
					Builders<BsonDocument>.IndexKeys.Ascending(createdAtFieldName),
					new CreateIndexOptions { Name = $"ix_{collectionName}_{createdAtFieldName}" }));

				indexModels.Add(new CreateIndexModel<BsonDocument>(
					Builders<BsonDocument>.IndexKeys.Ascending(updatedAtFieldName),
					new CreateIndexOptions { Name = $"ix_{collectionName}_{updatedAtFieldName}" }));
			}

			// Versioned entity index
			if (typeof(IVersionedEntity).IsAssignableFrom(documentType))
			{
				string versionFieldName = BsonFieldNames.Get((BaseDocument d) => d.Version);
				indexModels.Add(new CreateIndexModel<BsonDocument>(
					Builders<BsonDocument>.IndexKeys.Ascending(versionFieldName),
					new CreateIndexOptions { Name = $"ix_{collectionName}_{versionFieldName}" }));
			}

			// Optimistic concurrency index
			if (typeof(IOptimisticConcurrency).IsAssignableFrom(documentType))
			{
				string concurrencyTokenFieldName = BsonFieldNames.Get((BaseDocument d) => d.ConcurrencyToken);
				indexModels.Add(new CreateIndexModel<BsonDocument>(
					Builders<BsonDocument>.IndexKeys.Ascending(concurrencyTokenFieldName),
					new CreateIndexOptions { Name = $"ix_{collectionName}_{concurrencyTokenFieldName}" }));
			}

			// Soft-deletable entity indexes
			if (typeof(ISoftDeletableEntity).IsAssignableFrom(documentType))
			{
				string isDeletedFieldName = BsonFieldNames.Get((BaseSoftDeletableDocument d) => d.IsDeleted);
				string deletedAtFieldName = BsonFieldNames.Get((BaseSoftDeletableDocument d) => d.DeletedAt);

				// Index on isDeleted for filtering active vs deleted documents
				indexModels.Add(new CreateIndexModel<BsonDocument>(
					Builders<BsonDocument>.IndexKeys.Ascending(isDeletedFieldName),
					new CreateIndexOptions { Name = $"ix_{collectionName}_{isDeletedFieldName}" }));

				// Index on deletedAt for queries on deletion timestamp
				indexModels.Add(new CreateIndexModel<BsonDocument>(
					Builders<BsonDocument>.IndexKeys.Ascending(deletedAtFieldName),
					new CreateIndexOptions { Name = $"ix_{collectionName}_{deletedAtFieldName}" }));
			}

			// Create indexes idempotently (CreateOneAsync will be a no-op if index already exists with same spec/name)
			IMongoCollection<BsonDocument> collection = database.GetCollection<BsonDocument>(collectionName);
			IEnumerable<Task<string>> tasks = indexModels.Select(m => collection.Indexes.CreateOneAsync(m));
			await Task.WhenAll(tasks);
		}
	}
}