namespace EasyMeals.Shared.Data.Entities;

/// <summary>
///     Interface for entities that can be soft deleted
///     Follows DDD principles for maintaining data integrity
/// </summary>
public interface ISoftDeletableEntity
{
    /// <summary>
    ///     Indicates if the entity has been soft deleted
    /// </summary>
    bool IsDeleted { get; set; }

    /// <summary>
    ///     When the entity was soft deleted (null if not deleted)
    /// </summary>
    DateTime? DeletedAt { get; set; }
}