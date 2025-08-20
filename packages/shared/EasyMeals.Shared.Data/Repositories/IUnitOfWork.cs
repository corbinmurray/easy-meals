using MongoDB.Driver;

namespace EasyMeals.Shared.Data.Repositories;

/// <summary>
///     MongoDB-specific unit of work interface extending the generic pattern
///     Provides MongoDB transaction support while maintaining DDD principles
/// </summary>
public interface IUnitOfWork : IDisposable
{
    /// <summary>
    ///     Gets the underlying MongoDB session for transaction management
    ///     Provides access to MongoDB-specific transaction features
    /// </summary>
    IClientSessionHandle? Session { get; }

    /// <summary>
    ///     Gets the MongoDB database instance
    ///     Allows access to database-level operations
    /// </summary>
    IMongoDatabase Database { get; }

    /// <summary>
    ///     Starts a new MongoDB transaction with specified options
    ///     Supports MongoDB-specific transaction configuration
    /// </summary>
    /// <param name="transactionOptions">MongoDB transaction options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task BeginTransactionAsync(TransactionOptions? transactionOptions = null, CancellationToken cancellationToken = default);
}