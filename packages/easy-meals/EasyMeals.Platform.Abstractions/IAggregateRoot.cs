namespace EasyMeals.Platform.Abstractions;

/// <summary>
///     Interface for aggregate roots in a DDD context
/// </summary>
/// <typeparam name="TKey"></typeparam>
public interface IAggregateRoot<TKey>
{
	/// <summary>
	///     Gets or sets the unique identifier for this aggregate root
	/// </summary>
	TKey Id { get; set; }

	/// <summary>
	///     Gets or sets the creation timestamp of this aggregate root
	/// </summary>
	DateTime CreatedAt { get; set; }

	/// <summary>
	///     Gets or sets the last updated timestamp of this aggregate root
	/// </summary>
	DateTime UpdatedAt { get; set; }
}