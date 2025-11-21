namespace EasyMeals.Persistence.Abstractions.Repositories;

public interface IRepository<T, TKey> : IReadRepository<T, TKey>, IWriteRepository<T, TKey>
	where T : class
{
}