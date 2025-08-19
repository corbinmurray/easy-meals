using System.Linq.Expressions;

namespace EasyMeals.Shared.Data.Repositories;

/// <summary>
///     Generic repository interface following Repository pattern from DDD
///     Provides common CRUD operations while maintaining aggregate boundaries
///     Supports specification pattern for complex queries
/// </summary>
/// <typeparam name="TEntity">The entity type managed by this repository</typeparam>
public interface IRepository<TEntity> where TEntity : class
{
    /// <summary>
    ///     Gets an entity by its identifier
    /// </summary>
    /// <param name="id">The entity identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The entity if found, null otherwise</returns>
    Task<TEntity?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets all entities optionally filtered by a predicate
    /// </summary>
    /// <param name="predicate">Optional filter predicate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of entities matching the criteria</returns>
    Task<IEnumerable<TEntity>> GetAllAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets entities with pagination support
    ///     Essential for handling large datasets in production
    /// </summary>
    /// <param name="pageNumber">Page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <param name="predicate">Optional filter predicate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated result with entities and metadata</returns>
    Task<PagedResult<TEntity>> GetPagedAsync(int pageNumber, int pageSize, Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Finds the first entity matching the predicate
    /// </summary>
    /// <param name="predicate">Search predicate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>First matching entity or null</returns>
    Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Checks if any entity matches the predicate
    ///     Efficient existence checking without loading entities
    /// </summary>
    /// <param name="predicate">Search predicate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if any entity matches, false otherwise</returns>
    Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Counts entities matching the predicate
    /// </summary>
    /// <param name="predicate">Optional filter predicate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Count of matching entities</returns>
    Task<int> CountAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Adds a new entity to the repository
    ///     Note: Call SaveChangesAsync on UnitOfWork to persist
    /// </summary>
    /// <param name="entity">Entity to add</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Adds multiple entities to the repository
    ///     Optimized for bulk operations
    /// </summary>
    /// <param name="entities">Entities to add</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Updates an existing entity
    ///     Note: Call SaveChangesAsync on UnitOfWork to persist
    /// </summary>
    /// <param name="entity">Entity to update</param>
    void Update(TEntity entity);

    /// <summary>
    ///     Updates multiple entities
    ///     Optimized for bulk operations
    /// </summary>
    /// <param name="entities">Entities to update</param>
    void UpdateRange(IEnumerable<TEntity> entities);

    /// <summary>
    ///     Removes an entity from the repository
    ///     Note: Call SaveChangesAsync on UnitOfWork to persist
    /// </summary>
    /// <param name="entity">Entity to remove</param>
    void Remove(TEntity entity);

    /// <summary>
    ///     Removes multiple entities
    ///     Optimized for bulk operations
    /// </summary>
    /// <param name="entities">Entities to remove</param>
    void RemoveRange(IEnumerable<TEntity> entities);
}

/// <summary>
///     Represents a paginated result set
///     Essential for API pagination and performance optimization
/// </summary>
/// <typeparam name="T">The type of items in the result</typeparam>
public class PagedResult<T>
{
    public IEnumerable<T> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
}