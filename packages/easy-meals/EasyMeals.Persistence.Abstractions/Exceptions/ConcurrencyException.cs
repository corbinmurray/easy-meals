namespace EasyMeals.Persistence.Abstractions.Exceptions;

/// <summary>
///     Indicates a concurrency conflict when attempting to update an entity that has been modified by another process.
/// </summary>
public sealed class ConcurrencyException : Exception
{
	/// <summary>
	///     The ID of the entity that caused the concurrency conflict.
	/// </summary>
	public string? EntityId { get; }

	/// <summary>
	///     The type of the entity that caused the concurrency conflict.
	/// </summary>
	public string? EntityType { get; }

	/// <summary>
	///     Initializes a new instance of the <see cref="ConcurrencyException" /> class with a specified error message.
	/// </summary>
	/// <param name="message"></param>
	public ConcurrencyException(string message) : base(message)
	{
	}

	/// <summary>
	///     Initializes a new instance of the <see cref="ConcurrencyException" /> class with the specified entity ID and type.
	/// </summary>
	/// <param name="entityId"></param>
	/// <param name="entityType"></param>
	public ConcurrencyException(string entityId, string entityType)
		: base($"Concurrency conflict detected for {entityType} with ID {entityId}. The entity was modified by another process.")
	{
		EntityId = entityId;
		EntityType = entityType;
	}
}