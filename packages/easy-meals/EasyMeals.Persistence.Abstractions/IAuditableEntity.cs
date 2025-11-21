namespace EasyMeals.Persistence.Abstractions;

/// <summary>
///  Represents an entity that tracks creation and modification timestamps.
/// </summary>
public interface IAuditableEntity
{
	/// <summary>
	///  Gets or sets the date and time when the entity was created.
	/// </summary>
	DateTime CreatedAt { get; set; }

	/// <summary>
	///  Gets or sets the date and time when the entity was last updated.
	/// </summary>
	DateTime UpdatedAt { get; set; }

	/// <summary>
	///  Marks the entity as modified, updating the UpdatedAt timestamp.
	/// </summary>
	void MarkAsModified();
}