using EasyMeals.Platform.Abstractions;

namespace EasyMeals.Platform;

/// <summary>
///     Base class for aggregate roots in a DDD context
/// </summary>
/// <typeparam name="TKey"></typeparam>
public abstract class AggregateRoot<TKey> : IAggregateRoot<TKey>
{
	/// <summary>
	///     Instantiates a new aggregate root with a default ID
	/// </summary>
	protected AggregateRoot()
	{
		Id = default;
		DateTime now = DateTime.UtcNow;
		CreatedAt = now;
		UpdatedAt = now;
	}

	/// <summary>
	///     Instantiates a new aggregate root with the given ID
	/// </summary>
	/// <param name="id"></param>
	/// <param name="createdAt"></param>
	/// <param name="updatedAt"></param>
	protected AggregateRoot(TKey id, DateTime? createdAt = null, DateTime? updatedAt = null)
	{
		Id = id;
		DateTime now = DateTime.UtcNow;
		CreatedAt = createdAt ?? now;
		UpdatedAt = updatedAt ?? now;
	}

	/// <summary>
	///     Gets the unique identifier for this aggregate root
	/// </summary>
	public TKey Id { get; private set; }

	/// <summary>
	///     Gets the creation timestamp of this aggregate root
	/// </summary>
	public DateTime CreatedAt { get; private set; }

	/// <summary>
	///     Gets or sets the last updated timestamp of this aggregate root
	/// </summary>
	public DateTime UpdatedAt { get; set; }

	/// <summary>
	///     Explicit interface implementation for IAggregateRoot
	/// </summary>
	TKey IAggregateRoot<TKey>.Id
	{
		get => Id;
		set => Id = value;
	}

	/// <summary>
	///     Explicit interface implementation for IAggregateRoot
	/// </summary>
	DateTime IAggregateRoot<TKey>.CreatedAt
	{
		get => CreatedAt;
		set => CreatedAt = value;
	}
}