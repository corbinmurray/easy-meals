namespace EasyMeals.Persistence.Abstractions.Repositories;

public interface IReadRepository<T, TKey> where T : class
{
	Task<T?> GetByIdAsync(TKey id, CancellationToken ct = default);
	Task<IReadOnlyList<T>> GetByIdsAsync(IEnumerable<TKey> ids, CancellationToken ct = default);
	Task<IReadOnlyList<T>> ListAsync(CancellationToken ct = default);
	Task<bool> ExistsAsync(TKey id, CancellationToken ct = default);
}