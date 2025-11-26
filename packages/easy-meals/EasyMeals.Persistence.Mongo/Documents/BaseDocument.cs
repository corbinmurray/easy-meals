using EasyMeals.Persistence.Abstractions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EasyMeals.Persistence.Mongo.Documents;

/// <summary>
///     Base document for MongoDB entities.
///     Uses string (ObjectId) as the key type for MongoDB compatibility.
/// </summary>
public abstract class BaseDocument : IEntity<string>, IAuditableEntity, IVersionedEntity, IOptimisticConcurrency
{
	/// <summary>
	///     Unique identifier for the document
	///     Uses MongoDB ObjectId for optimal performance and sharding
	/// </summary>
	[BsonId]
	[BsonRepresentation(BsonType.ObjectId)]
	public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

	/// <summary>
	///     When the document was first created
	/// </summary>
	[BsonElement("createdAt")]
	[BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
	public DateTime CreatedAt { get; set; }

	/// <summary>
	///     When the document was last updated
	/// </summary>
	[BsonElement("updatedAt")]
	[BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
	public DateTime UpdatedAt { get; set; }

	/// <summary>
	///     Updates the UpdatedAt timestamp
	///     Called automatically by the repository on save operations
	/// </summary>
	public virtual void MarkAsModified()
	{
		UpdatedAt = DateTime.UtcNow;
	}

	/// <summary>
	///     Document schema version for backward/forward compatibility
	///     Enables safe document evolution and migration strategies
	///     Default to 1 for new documents, increment when schema changes
	/// </summary>
	[BsonElement("version")]
	[BsonDefaultValue(1)]
	public int Version { get; set; } = 1;

	/// <summary>
	///     Updates the document version and modification timestamp
	///     Call when making schema-breaking changes to document structure
	/// </summary>
	public virtual void IncrementVersion()
	{
		Version++;
		MarkAsModified();
	}

	/// <summary>
	///     The concurrency token used for optimistic locking.
	///     Incremented on each update to detect concurrent modifications.
	/// </summary>
	[BsonElement("__concurrencyToken")]
	public long ConcurrencyToken { get; set; }

	/// <summary>
	///     Increments the concurrency token.
	/// </summary>
	public void IncrementConcurrencyToken()
	{
		ConcurrencyToken++;
	}
}

/// <summary>
///     Base document with soft delete capability for MongoDB
///     Implements both audit trail and soft delete patterns from DDD
/// </summary>
public abstract class BaseSoftDeletableDocument : BaseDocument, ISoftDeletableEntity
{
	/// <summary>
	///     Indicates if the document has been soft deleted
	/// </summary>
	[BsonElement("isDeleted")]
	public bool IsDeleted { get; set; }

	/// <summary>
	///     When the document was soft deleted (null if not deleted)
	/// </summary>
	[BsonElement("deletedAt")]
	[BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
	[BsonIgnoreIfNull]
	public DateTime? DeletedAt { get; set; }

	/// <summary>
	///     Soft deletes the document
	///     Maintains data integrity while supporting business requirements for data retention
	/// </summary>
	public virtual void SoftDelete()
	{
		if (IsDeleted) return;
		IsDeleted = true;
		DeletedAt = DateTime.UtcNow;
		MarkAsModified();
	}

	/// <summary>
	///     Restores a soft deleted document
	///     Supports business scenarios where deletion needs to be reversed
	/// </summary>
	public virtual void Restore()
	{
		IsDeleted = false;
		DeletedAt = null;
		MarkAsModified();
	}
}