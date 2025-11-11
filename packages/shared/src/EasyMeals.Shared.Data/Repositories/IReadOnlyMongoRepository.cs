using System.Linq.Expressions;

namespace EasyMeals.Shared.Data.Repositories;

/// <summary>
///     Read-only repository interface for querying entities
///     Implements the Principle of Least Privilege for bounded contexts
/// </summary>
/// <typeparam name="TEntity">The entity type</typeparam>
public interface IReadOnlyMongoRepository<TEntity> where TEntity : class
{
    /// <summary>
    ///     Gets an entity by its identifier
    /// </summary>
    /// <param name="id">The entity identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The entity if found, null otherwise</returns>
    Task<TEntity?> GetByIdAsync(
        string id,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets all entities matching the specified filter
    /// </summary>
    /// <param name="filter">Filter expression</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of matching entities</returns>
    Task<IEnumerable<TEntity>> GetAllAsync(
        Expression<Func<TEntity, bool>>? filter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets the first entity matching the specified filter
    /// </summary>
    /// <param name="filter">Filter expression</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The first matching entity or null if not found</returns>
    Task<TEntity?> GetFirstOrDefaultAsync(
        Expression<Func<TEntity, bool>> filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets a paged collection of entities matching the specified filter
    /// </summary>
    /// <param name="filter">Filter expression</param>
    /// <param name="sortBy">Sort expression</param>
    /// <param name="sortDirection">Sort direction (1 for ascending, -1 for descending)</param>
    /// <param name="pageNumber">Page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paged collection with total count</returns>
    Task<(IEnumerable<TEntity> Items, long TotalCount)> GetPagedAsync(
        Expression<Func<TEntity, bool>>? filter = null,
        Expression<Func<TEntity, object>>? sortBy = null,
        int sortDirection = 1,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets the count of entities matching the specified filter
    /// </summary>
    /// <param name="filter">Filter expression</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Count of matching entities</returns>
    Task<long> CountAsync(
        Expression<Func<TEntity, bool>>? filter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Checks if any entity matches the specified filter
    /// </summary>
    /// <param name="filter">Filter expression</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if any entity matches the filter</returns>
    Task<bool> ExistsAsync(
        Expression<Func<TEntity, bool>> filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets entities by multiple identifiers
    /// </summary>
    /// <param name="ids">Collection of entity identifiers</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of found entities</returns>
    Task<IEnumerable<TEntity>> GetByIdsAsync(
        IEnumerable<string> ids,
        CancellationToken cancellationToken = default);
}