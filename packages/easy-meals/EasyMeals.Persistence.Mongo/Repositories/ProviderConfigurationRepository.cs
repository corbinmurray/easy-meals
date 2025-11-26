using EasyMeals.Domain.ProviderConfiguration;
using EasyMeals.Persistence.Abstractions.Exceptions;
using EasyMeals.Persistence.Abstractions.Repositories;
using EasyMeals.Persistence.Mongo.Documents.ProviderConfiguration;
using MongoDB.Bson;
using MongoDB.Driver;

namespace EasyMeals.Persistence.Mongo.Repositories;

/// <summary>
/// MongoDB implementation of the provider configuration repository.
/// Handles domain-to-document mapping and MongoDB-specific operations.
/// </summary>
public class ProviderConfigurationRepository : IProviderConfigurationRepository
{
    private readonly IMongoCollection<ProviderConfigurationDocument> _collection;

    public ProviderConfigurationRepository(IMongoContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _collection = context.GetCollection<ProviderConfigurationDocument>();
    }

    /// <inheritdoc />
    public async Task<ProviderConfiguration?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id) || !ObjectId.TryParse(id, out _))
            return null;

        var filter = Builders<ProviderConfigurationDocument>.Filter.And(
            Builders<ProviderConfigurationDocument>.Filter.Eq(d => d.Id, id),
            Builders<ProviderConfigurationDocument>.Filter.Eq(d => d.IsDeleted, false)
        );

        var document = await _collection.Find(filter).FirstOrDefaultAsync(ct);
        return document is null ? null : ProviderConfigurationMapper.ToDomain(document);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProviderConfiguration>> GetAllEnabledAsync(CancellationToken ct = default)
    {
        var filter = Builders<ProviderConfigurationDocument>.Filter.And(
            Builders<ProviderConfigurationDocument>.Filter.Eq(d => d.IsEnabled, true),
            Builders<ProviderConfigurationDocument>.Filter.Eq(d => d.IsDeleted, false)
        );

        var sort = Builders<ProviderConfigurationDocument>.Sort.Descending(d => d.Priority);

        var documents = await _collection
            .Find(filter)
            .Sort(sort)
            .ToListAsync(ct);

        return documents
            .Select(ProviderConfigurationMapper.ToDomain)
            .ToList()
            .AsReadOnly();
    }

    /// <inheritdoc />
    public async Task<ProviderConfiguration?> GetByProviderNameAsync(string providerName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(providerName))
            return null;

        var normalizedName = providerName.ToLowerInvariant().Trim();

        var filter = Builders<ProviderConfigurationDocument>.Filter.And(
            Builders<ProviderConfigurationDocument>.Filter.Eq(d => d.ProviderName, normalizedName),
            Builders<ProviderConfigurationDocument>.Filter.Eq(d => d.IsDeleted, false)
        );

        var document = await _collection.Find(filter).FirstOrDefaultAsync(ct);
        return document is null ? null : ProviderConfigurationMapper.ToDomain(document);
    }

    /// <inheritdoc />
    public async Task<bool> ExistsByProviderNameAsync(string providerName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(providerName))
            return false;

        var normalizedName = providerName.ToLowerInvariant().Trim();

        var filter = Builders<ProviderConfigurationDocument>.Filter.And(
            Builders<ProviderConfigurationDocument>.Filter.Eq(d => d.ProviderName, normalizedName),
            Builders<ProviderConfigurationDocument>.Filter.Eq(d => d.IsDeleted, false)
        );

        return await _collection.Find(filter).AnyAsync(ct);
    }

    /// <inheritdoc />
    public async Task<string> AddAsync(ProviderConfiguration entity, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        // Check for uniqueness
        if (await ExistsByProviderNameAsync(entity.ProviderName, ct))
            throw new ArgumentException($"A provider with name '{entity.ProviderName}' already exists.");

        var document = ProviderConfigurationMapper.ToDocument(entity);

        // Generate new ID if not set
        if (string.IsNullOrEmpty(document.Id))
            document.Id = ObjectId.GenerateNewId().ToString();

        // Set audit fields
        var now = DateTime.UtcNow;
        document.CreatedAt = now;
        document.UpdatedAt = now;
        document.ConcurrencyToken = 0;
        document.Version = 1;

        await _collection.InsertOneAsync(document, cancellationToken: ct);
        return document.Id;
    }

    /// <inheritdoc />
    public async Task UpdateAsync(ProviderConfiguration entity, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (string.IsNullOrWhiteSpace(entity.Id))
            throw new ArgumentException("Entity must have a valid Id.", nameof(entity));

        var document = ProviderConfigurationMapper.ToDocument(entity);

        // Apply optimistic concurrency check
        var filter = Builders<ProviderConfigurationDocument>.Filter.And(
            Builders<ProviderConfigurationDocument>.Filter.Eq(d => d.Id, entity.Id),
            Builders<ProviderConfigurationDocument>.Filter.Eq(d => d.ConcurrencyToken, entity.ConcurrencyToken)
        );

        // Increment concurrency token and update timestamp
        document.ConcurrencyToken = entity.ConcurrencyToken + 1;
        document.UpdatedAt = DateTime.UtcNow;

        var result = await _collection.ReplaceOneAsync(
            filter,
            document,
            new ReplaceOptions { IsUpsert = false },
            ct);

        if (result.MatchedCount == 0)
            throw new ConcurrencyException(entity.Id, nameof(ProviderConfiguration));
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id) || !ObjectId.TryParse(id, out _))
            throw new ArgumentException("Invalid entity ID.", nameof(id));

        var filter = Builders<ProviderConfigurationDocument>.Filter.Eq(d => d.Id, id);

        var update = Builders<ProviderConfigurationDocument>.Update
            .Set(d => d.IsDeleted, true)
            .Set(d => d.DeletedAt, DateTime.UtcNow)
            .Set(d => d.UpdatedAt, DateTime.UtcNow)
            .Inc(d => d.ConcurrencyToken, 1);

        await _collection.UpdateOneAsync(filter, update, cancellationToken: ct);
    }
}
