namespace EasyMeals.Persistence.Abstractions;

/// <summary>
///     Represents an entity that has versioning for forwards & backwards compatability and concurrency control.
/// </summary>
public interface IVersionedEntity
{
    /// <summary>
    ///     Gets or sets the version number of the entity for concurrency control.
    /// </summary>
    int Version { get; set; }

    /// <summary>
    ///     Increments the version number of the entity.
    /// </summary>
    void IncrementVersion();
}