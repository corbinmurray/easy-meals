namespace EasyMeals.Persistence.Abstractions.Repositories;

/// <summary>
///     Defines write repository operations for entities.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
/// <typeparam name="TKey">The type of the entity's unique identifier.</typeparam>
public interface IWriteRepository<T, TKey>
	where T : class, IEntity<TKey>
{
	Task<TKey> AddAsync(T entity, CancellationToken cancellationToken = default);
	Task AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);
	Task UpdateAsync(T entity, CancellationToken cancellationToken = default);
	Task DeleteAsync(TKey id, CancellationToken cancellationToken = default);
	Task DeleteRangeAsync(IEnumerable<TKey> ids, CancellationToken cancellationToken = default);
}