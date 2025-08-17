using System.ComponentModel.DataAnnotations;

namespace EasyMeals.Shared.Data.Entities;

/// <summary>
/// Base entity class implementing common audit functionality
/// Follows DDD patterns for aggregate root identification and audit trails
/// </summary>
public abstract class BaseEntity : IAuditableEntity
{
    /// <summary>
    /// Unique identifier for the entity
    /// </summary>
    [Key]
    [MaxLength(450)] // Optimized for database indexes
    public virtual string Id { get; set; } = string.Empty;

    /// <summary>
    /// When the entity was first created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the entity was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Updates the UpdatedAt timestamp
    /// Called automatically by the DbContext on save operations
    /// </summary>
    public virtual void MarkAsModified()
    {
        UpdatedAt = DateTime.UtcNow;
    }
}

/// <summary>
/// Base entity with soft delete capability
/// Implements both audit trail and soft delete patterns from DDD
/// </summary>
public abstract class BaseSoftDeletableEntity : BaseEntity, ISoftDeletableEntity
{
    /// <summary>
    /// Indicates if the entity has been soft deleted
    /// </summary>
    public bool IsDeleted { get; set; } = false;

    /// <summary>
    /// When the entity was soft deleted (null if not deleted)
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// Soft deletes the entity
    /// Maintains data integrity while supporting business requirements for data retention
    /// </summary>
    public virtual void SoftDelete()
    {
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
        MarkAsModified();
    }

    /// <summary>
    /// Restores a soft deleted entity
    /// Supports business scenarios where deletion needs to be reversed
    /// </summary>
    public virtual void Restore()
    {
        IsDeleted = false;
        DeletedAt = null;
        MarkAsModified();
    }
}
