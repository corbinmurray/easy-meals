namespace EasyMeals.Persistence.Abstractions;

/// <summary>
///  Represents an entity that supports soft deletion.
/// </summary>
public interface ISoftDeletableEntity
{
    /// <summary>
    /// Gets or sets a value indicating whether the entity is soft deleted.
    /// </summary>
    bool IsDeleted { get; set; }

    /// <summary>
    ///   Gets or sets the date and time when the entity was soft deleted.
    /// </summary>
    DateTime? DeletedAt { get; set; }

    /// <summary>
    /// Marks the entity as soft deleted, setting IsDeleted to true and updating DeletedAt timestamp.
    /// </summary>
    void SoftDelete();

    /// <summary>
    ///  Restores the entity from soft deletion, setting IsDeleted to false and clearing DeletedAt timestamp.
    /// </summary>
    void Restore();
}