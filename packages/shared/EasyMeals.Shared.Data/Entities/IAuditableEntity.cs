namespace EasyMeals.Shared.Data.Entities;

/// <summary>
///     Interface for entities that track creation and modification times
///     Supports DDD domain events and audit trail requirements
/// </summary>
public interface IAuditableEntity
{
    /// <summary>
    ///     When the entity was first created
    /// </summary>
    DateTime CreatedAt { get; set; }

    /// <summary>
    ///     When the entity was last updated
    /// </summary>
    DateTime UpdatedAt { get; set; }
}