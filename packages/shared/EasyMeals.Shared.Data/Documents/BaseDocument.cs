using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EasyMeals.Shared.Data.Documents;

/// <summary>
///     Base document class implementing common audit functionality for MongoDB
///     Follows DDD patterns for aggregate root identification and audit trails
///     Uses MongoDB-specific attributes for proper BSON serialization
/// </summary>
public abstract class BaseDocument : IAuditableDocument
{
    /// <summary>
    ///     Unique identifier for the document
    ///     Uses MongoDB ObjectId for optimal performance and sharding
    /// </summary>
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public virtual string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    /// <summary>
    ///     When the document was first created
    /// </summary>
    [BsonElement("createdAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     When the document was last updated
    /// </summary>
    [BsonElement("updatedAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     Updates the UpdatedAt timestamp
    ///     Called automatically by the repository on save operations
    /// </summary>
    public virtual void MarkAsModified()
    {
        UpdatedAt = DateTime.UtcNow;
    }
}

/// <summary>
///     Base document with soft delete capability for MongoDB
///     Implements both audit trail and soft delete patterns from DDD
/// </summary>
public abstract class BaseSoftDeletableDocument : BaseDocument, ISoftDeletableDocument
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