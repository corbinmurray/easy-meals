using EasyMeals.Persistence.Mongo.Documents.ProviderConfiguration;
using MongoDB.Driver;

namespace EasyMeals.Persistence.Mongo.Indexes;

/// <summary>
/// Defines and creates MongoDB indexes for the provider_configurations collection.
/// </summary>
public static class ProviderConfigurationIndexes
{
    /// <summary>
    /// Creates all required indexes for the provider_configurations collection.
    /// </summary>
    /// <param name="collection">The MongoDB collection.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task CreateIndexesAsync(
        IMongoCollection<ProviderConfigurationDocument> collection,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(collection);

        var indexModels = new List<CreateIndexModel<ProviderConfigurationDocument>>
        {
            // Unique index on provider name (business key)
            new(
                Builders<ProviderConfigurationDocument>.IndexKeys.Ascending(d => d.ProviderName),
                new CreateIndexOptions
                {
                    Unique = true,
                    Name = "idx_provider_name_unique"
                }
            ),

            // Compound index for enabled provider queries (common read path)
            new(
                Builders<ProviderConfigurationDocument>.IndexKeys
                    .Ascending(d => d.IsEnabled)
                    .Ascending(d => d.IsDeleted)
                    .Descending(d => d.Priority),
                new CreateIndexOptions
                {
                    Name = "idx_enabled_priority"
                }
            ),

            // Index for soft delete filtering
            new(
                Builders<ProviderConfigurationDocument>.IndexKeys.Ascending(d => d.IsDeleted),
                new CreateIndexOptions
                {
                    Name = "idx_is_deleted"
                }
            )
        };

        await collection.Indexes.CreateManyAsync(indexModels, ct);
    }

    /// <summary>
    /// Creates all required indexes using the database instance.
    /// </summary>
    /// <param name="database">The MongoDB database.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task CreateIndexesAsync(IMongoDatabase database, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(database);

        var collection = database.GetCollection<ProviderConfigurationDocument>("provider_configurations");
        await CreateIndexesAsync(collection, ct);
    }

    /// <summary>
    /// Creates all required indexes using the MongoContext.
    /// </summary>
    /// <param name="context">The MongoDB context.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task CreateIndexesAsync(IMongoContext context, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var collection = context.GetCollection<ProviderConfigurationDocument>();
        await CreateIndexesAsync(collection, ct);
    }
}
