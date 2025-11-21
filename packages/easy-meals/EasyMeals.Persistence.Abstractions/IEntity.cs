namespace EasyMeals.Persistence.Abstractions;

/// <summary>
/// Represents a basic entity.
/// </summary>
public interface IEntity
{
	/// <summary>
    /// Gets or sets the unique identifier of the entity.
    /// </summary>
	string Id { get; set; }

	
}