using System.Linq.Expressions;

namespace EasyMeals.Shared.Data.Repositories;

/// <summary>
///     Read-only repository interface for querying entities
///     Implements the Principle of Least Privilege for bounded contexts
/// </summary>
/// <typeparam name="TEntity">The entity type</typeparam>
public interface IReadOnlyRepository<TEntity> where TEntity : class
{
	/// <summary>
	///     Gets an entity by its identifier
	/// </summary>
	/// <param name="id">The entity identifier</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>The entity if found, null otherwise</returns>
	Task<TEntity?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

	/// <summary>
	///     Gets all entities matching the specified filter
	/// </summary>
	/// <param name="filter">Filter expression</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>Collection of matching entities</returns>
	Task<IEnumerable<TEntity>> GetAllAsync(
		Expression<Func<TEntity, bool>>? filter = null,
		CancellationToken cancellationToken = default);
}