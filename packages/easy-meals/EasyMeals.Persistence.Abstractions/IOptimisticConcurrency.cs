namespace EasyMeals.Persistence.Abstractions;

/// <summary>
///     Represents an entity that supports optimistic concurrency control.
/// </summary>
public interface IOptimisticConcurrency
{
	/// <summary>
	///     Gets or sets the concurrency token used for optimistic locking.
	///     Incremented on each update to detect concurrent modifications.
	/// </summary>
	long ConcurrencyToken { get; set; }

	/// <summary>
	///     Increments the concurrency token.
	/// </summary>
	void IncrementConcurrencyToken();
}