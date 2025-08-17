namespace EasyMeals.Shared.Data.Repositories;

/// <summary>
/// Unit of Work interface following the Unit of Work pattern from DDD
/// Provides transactional consistency across multiple repositories
/// Essential for maintaining aggregate boundaries and ACID properties
/// </summary>
public interface IUnitOfWork : IDisposable
{
    /// <summary>
    /// Gets a repository for the specified entity type
    /// Ensures all repositories share the same DbContext and transaction
    /// </summary>
    /// <typeparam name="TEntity">The entity type</typeparam>
    /// <returns>Repository instance for the entity type</returns>
    IRepository<TEntity> Repository<TEntity>() where TEntity : class;

    /// <summary>
    /// Saves all changes made in this unit of work to the database
    /// Maintains transactional consistency across all operations
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of entities written to the database</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Begins a new database transaction
    /// Supports explicit transaction management for complex operations
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Transaction instance</returns>
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits the current transaction
    /// Finalizes all changes made within the transaction scope
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back the current transaction
    /// Discards all changes made within the transaction scope
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}
