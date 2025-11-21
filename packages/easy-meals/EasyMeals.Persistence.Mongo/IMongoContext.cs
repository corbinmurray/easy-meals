using MongoDB.Driver;

namespace EasyMeals.Persistence.Mongo;

/// <summary>
///     Provides access to MongoDB database, collections, and session management.
/// </summary>
public interface IMongoContext
{
    /// <summary>
    ///     Gets the MongoDB database instance.
    /// </summary>
    IMongoDatabase Database { get; }

    /// <summary>
    ///     Gets a collection for the specified type.
    /// </summary>
    /// <typeparam name="T">The document type.</typeparam>
    /// <param name="name">Optional collection name. If not provided, uses type name.</param>
    /// <returns>The MongoDB collection.</returns>
    IMongoCollection<T> GetCollection<T>(string? name = null) where T : class;

    /// <summary>
    ///     Starts a new client session for transactions.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The client session handle.</returns>
    Task<IClientSessionHandle> StartSessionAsync(CancellationToken cancellationToken = default);
}