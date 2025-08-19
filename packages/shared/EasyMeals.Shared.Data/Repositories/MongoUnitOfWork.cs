using System.Collections.Concurrent;
using MongoDB.Driver;
using EasyMeals.Shared.Data.Documents;

namespace EasyMeals.Shared.Data.Repositories;

/// <summary>
/// MongoDB Unit of Work implementation following the Unit of Work pattern from DDD
/// Provides transactional consistency across multiple repositories using MongoDB sessions
/// Essential for maintaining aggregate boundaries and ACID properties in MongoDB
/// </summary>
public class MongoUnitOfWork : IMongoUnitOfWork
{
    private readonly IMongoClient _mongoClient;
    private readonly IMongoDatabase _database;
    private readonly ConcurrentDictionary<Type, object> _repositories;
    private IClientSessionHandle? _session;
    private bool _disposed = false;

    public MongoUnitOfWork(IMongoClient mongoClient, IMongoDatabase database)
    {
        _mongoClient = mongoClient ?? throw new ArgumentNullException(nameof(mongoClient));
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _repositories = new ConcurrentDictionary<Type, object>();
    }

    /// <summary>
    /// Gets the underlying MongoDB session for transaction management
    /// </summary>
    public IClientSessionHandle? Session => _session;

    /// <summary>
    /// Gets the MongoDB database instance
    /// </summary>
    public IMongoDatabase Database => _database;

    /// <summary>
    /// Gets a repository for the specified document type
    /// Ensures all repositories share the same session and transaction context
    /// </summary>
    /// <typeparam name="TEntity">The document type</typeparam>
    /// <returns>Repository instance for the document type</returns>
    public IRepository<TEntity> Repository<TEntity>() where TEntity : class
    {
        if (!typeof(BaseDocument).IsAssignableFrom(typeof(TEntity)))
        {
            throw new ArgumentException($"Entity type {typeof(TEntity).Name} must inherit from BaseDocument");
        }

        return (IRepository<TEntity>)_repositories.GetOrAdd(typeof(TEntity), _ =>
        {
            var repositoryType = typeof(MongoRepository<>).MakeGenericType(typeof(TEntity));
            return Activator.CreateInstance(repositoryType, _database, _session)!;
        });
    }

    /// <summary>
    /// Saves all changes made in this unit of work to the database
    /// In MongoDB, this commits the current transaction if one is active
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of operations completed (always 1 for MongoDB)</returns>
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (_session?.IsInTransaction == true)
        {
            await _session.CommitTransactionAsync(cancellationToken);
        }

        // MongoDB doesn't have a direct equivalent to EF's SaveChanges count
        // Return 1 to indicate successful completion
        return 1;
    }

    /// <summary>
    /// Begins a new database transaction
    /// Creates a new MongoDB session and starts a transaction
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        await BeginTransactionAsync(null, cancellationToken);
    }

    /// <summary>
    /// Starts a new MongoDB transaction with specified options
    /// </summary>
    /// <param name="transactionOptions">MongoDB transaction options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task BeginTransactionAsync(TransactionOptions? transactionOptions = null, CancellationToken cancellationToken = default)
    {
        if (_session != null)
        {
            throw new InvalidOperationException("A transaction is already active.");
        }

        _session = await _mongoClient.StartSessionAsync(cancellationToken: cancellationToken);

        if (transactionOptions != null)
        {
            _session.StartTransaction(transactionOptions);
        }
        else
        {
            _session.StartTransaction();
        }

        // Clear existing repositories so they pick up the new session
        _repositories.Clear();
    }

    /// <summary>
    /// Commits the current transaction
    /// Finalizes all changes made within the transaction scope
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_session?.IsInTransaction == true)
        {
            await _session.CommitTransactionAsync(cancellationToken);
        }
        else
        {
            throw new InvalidOperationException("No active transaction to commit.");
        }
    }

    /// <summary>
    /// Rolls back the current transaction
    /// Discards all changes made within the transaction scope
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_session?.IsInTransaction == true)
        {
            await _session.AbortTransactionAsync(cancellationToken);
        }
        else
        {
            throw new InvalidOperationException("No active transaction to rollback.");
        }
    }

    /// <summary>
    /// Disposes the unit of work and cleans up resources
    /// Ensures proper cleanup of MongoDB session and repositories
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            if (_session != null)
            {
                if (_session.IsInTransaction)
                {
                    // Auto-rollback if transaction is still active
                    try
                    {
                        _session.AbortTransaction();
                    }
                    catch
                    {
                        // Ignore errors during cleanup
                    }
                }

                _session.Dispose();
                _session = null;
            }

            _repositories.Clear();
            _disposed = true;
        }
    }
}
