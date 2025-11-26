namespace EasyMeals.Persistence.Abstractions.Repositories;

/// <summary>
///     Defines a full repository with read and write operations for entities.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
/// <typeparam name="TKey">The type of the entity's unique identifier.</typeparam>
public interface IRepository<T, TKey> : IReadRepository<T, TKey>, IWriteRepository<T, TKey>
	where T : class, IEntity<TKey>
{
}