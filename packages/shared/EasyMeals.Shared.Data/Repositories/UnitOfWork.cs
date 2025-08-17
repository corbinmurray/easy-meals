using EasyMeals.Shared.Data.DbContexts;
using Microsoft.EntityFrameworkCore.Storage;

namespace EasyMeals.Shared.Data.Repositories;

/// <summary>
/// Unit of Work implementation using Entity Framework Core
/// Provides transactional consistency following the Unit of Work pattern from DDD
/// Ensures proper resource management and transaction scope handling
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly EasyMealsDbContext _context;
    private readonly Dictionary<Type, object> _repositories = new();
    private IDbContextTransaction? _transaction;
    private bool _disposed = false;

    public UnitOfWork(EasyMealsDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    public IRepository<TEntity> Repository<TEntity>() where TEntity : class
    {
        var entityType = typeof(TEntity);

        if (_repositories.ContainsKey(entityType))
        {
            return (IRepository<TEntity>)_repositories[entityType];
        }

        var repository = new Repository<TEntity>(_context);
        _repositories.Add(entityType, repository);

        return repository;
    }

    /// <inheritdoc />
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Update audit fields for entities that implement IAuditableEntity
        UpdateAuditFields();

        return await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is not null)
        {
            throw new InvalidOperationException("Transaction already in progress");
        }

        _transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
        {
            throw new InvalidOperationException("No transaction in progress");
        }

        try
        {
            await SaveChangesAsync(cancellationToken);
            await _transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            await DisposeTransactionAsync();
        }
    }

    /// <inheritdoc />
    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
        {
            throw new InvalidOperationException("No transaction in progress");
        }

        try
        {
            await _transaction.RollbackAsync(cancellationToken);
        }
        finally
        {
            await DisposeTransactionAsync();
        }
    }

    /// <summary>
    /// Updates audit fields for entities that implement IAuditableEntity
    /// Ensures consistent audit trail across all entity operations
    /// </summary>
    private void UpdateAuditFields()
    {
        var entries = _context.ChangeTracker.Entries()
            .Where(e => e.Entity is Entities.IAuditableEntity &&
                       (e.State == Microsoft.EntityFrameworkCore.EntityState.Added ||
                        e.State == Microsoft.EntityFrameworkCore.EntityState.Modified));

        foreach (var entry in entries)
        {
            var auditableEntity = (Entities.IAuditableEntity)entry.Entity;

            if (entry.State == Microsoft.EntityFrameworkCore.EntityState.Added)
            {
                auditableEntity.CreatedAt = DateTime.UtcNow;
            }

            auditableEntity.UpdatedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Disposes the current transaction
    /// </summary>
    private async Task DisposeTransactionAsync()
    {
        if (_transaction is not null)
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Protected dispose method following the standard Dispose pattern
    /// </summary>
    /// <param name="disposing">True if disposing managed resources</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _transaction?.Dispose();
            _repositories.Clear();
            _disposed = true;
        }
    }
}
