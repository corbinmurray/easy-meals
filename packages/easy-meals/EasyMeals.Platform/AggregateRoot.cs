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
		CreatedAt = DateTime.UtcNow;
		UpdatedAt = DateTime.UtcNow;
	}

	/// <summary>
	///     Instantiates a new aggregate root with the given ID
	/// </summary>
	/// <param name="id"></param>
	protected AggregateRoot(TKey id)
	{
		Id = id;
		CreatedAt = DateTime.UtcNow;
		UpdatedAt = DateTime.UtcNow;
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
	///     Gets the last updated timestamp of this aggregate root
	/// </summary>
	public DateTime UpdatedAt { get; private set; }

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

	/// <summary>
	///     Explicit interface implementation for IAggregateRoot
	/// </summary>
	DateTime IAggregateRoot<TKey>.UpdatedAt
	{
		get => UpdatedAt;
		set => UpdatedAt = value;
	}
}