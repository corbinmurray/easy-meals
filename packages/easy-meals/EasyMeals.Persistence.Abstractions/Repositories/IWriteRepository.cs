namespace EasyMeals.Persistence.Abstractions.Repositories;

public interface IWriteRepository<T, TKey>
	where T : class
{
	Task<TKey> AddAsync(T entity, CancellationToken cancellationToken = default);
	Task AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);
	Task UpdateAsync(T entity, CancellationToken cancellationToken = default);
	Task DeleteAsync(TKey id, CancellationToken cancellationToken = default);
	Task DeleteRangeAsync(IEnumerable<TKey> ids, CancellationToken cancellationToken = default);
}