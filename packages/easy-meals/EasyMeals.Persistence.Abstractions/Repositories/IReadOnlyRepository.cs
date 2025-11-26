namespace EasyMeals.Persistence.Abstractions.Repositories;

/// <summary>
///     Defines read-only repository operations for entities.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
/// <typeparam name="TKey">The type of the entity's unique identifier.</typeparam>
public interface IReadRepository<T, TKey>
	where T : class, IEntity<TKey>
{
	Task<T?> GetByIdAsync(TKey id, CancellationToken ct = default);
	Task<IReadOnlyList<T>> GetByIdsAsync(IEnumerable<TKey> ids, CancellationToken ct = default);
	Task<IReadOnlyList<T>> ListAsync(CancellationToken ct = default);
	Task<bool> ExistsAsync(TKey id, CancellationToken ct = default);
}