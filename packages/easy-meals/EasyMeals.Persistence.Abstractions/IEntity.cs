namespace EasyMeals.Persistence.Abstractions;

/// <summary>
///     Represents a basic entity with a unique identifier.
/// </summary>
/// <typeparam name="TKey">The type of the entity's unique identifier.</typeparam>
public interface IEntity<TKey>
{
	/// <summary>
	///     Gets or sets the unique identifier of the entity.
	/// </summary>
	TKey Id { get; set; }
}