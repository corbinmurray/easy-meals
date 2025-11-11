using System.Reflection;
using EasyMeals.RecipeEngine.Domain.Entities;
using EasyMeals.RecipeEngine.Domain.Repositories;
using EasyMeals.RecipeEngine.Domain.ValueObjects;
using EasyMeals.RecipeEngine.Infrastructure.Documents;
using EasyMeals.Shared.Data.Repositories;
using MongoDB.Driver;

namespace EasyMeals.RecipeEngine.Infrastructure.Repositories;

/// <summary>
///     MongoDB implementation of IRecipeBatchRepository.
/// </summary>
public class RecipeBatchRepository(IMongoDatabase database, IClientSessionHandle? session = null)
    : MongoRepository<RecipeBatchDocument>(database, session), IRecipeBatchRepository
{
    public async Task<RecipeBatch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        RecipeBatchDocument? document = await base.GetByIdAsync(id.ToString(), cancellationToken);
        return document != null ? ToDomain(document) : null;
    }

    public async Task<RecipeBatch?> GetActiveAsync(string providerId, CancellationToken cancellationToken = default)
    {
        RecipeBatchDocument? document = await GetFirstOrDefaultAsync(
            d => d.ProviderId == providerId && d.Status == BatchStatus.InProgress.ToString(),
            cancellationToken);

        return document != null ? ToDomain(document) : null;
    }

    public async Task<RecipeBatch> CreateAsync(
        string providerId,
        ProviderConfiguration config,
        CancellationToken cancellationToken = default)
    {
        var batch = RecipeBatch.CreateBatch(providerId, config.BatchSize, config.TimeWindow);
        await SaveAsync(batch, cancellationToken);
        return batch;
    }

    public async Task SaveAsync(RecipeBatch batch, CancellationToken cancellationToken = default)
    {
        RecipeBatchDocument document = ToDocument(batch);
        await ReplaceOneAsync(d => d.Id == document.Id, document, true, cancellationToken);
    }

    public async Task<IEnumerable<RecipeBatch>> GetRecentBatchesAsync(
        string providerId,
        int count,
        CancellationToken cancellationToken = default)
    {
        (IEnumerable<RecipeBatchDocument> items, _) = await GetPagedAsync(
            d => d.ProviderId == providerId,
            d => d.StartedAt,
            -1,
            1,
            count,
            cancellationToken);

        return items.Select(ToDomain);
    }

    private static RecipeBatch ToDomain(RecipeBatchDocument document)
    {
        var batch = RecipeBatch.CreateBatch(
            document.ProviderId,
            document.BatchSize,
            TimeSpan.FromMinutes(document.TimeWindowMinutes)
        );

        // Use reflection to set private fields for reconstitution
        PropertyInfo? idProperty = typeof(RecipeBatch).GetProperty(nameof(RecipeBatch.Id));
        idProperty?.SetValue(batch, document.Id);

        return batch;
    }

    private static RecipeBatchDocument ToDocument(RecipeBatch batch) =>
        new()
        {
            Id = batch.Id.ToString(),
            ProviderId = batch.ProviderId,
            BatchSize = batch.BatchSize,
            TimeWindowMinutes = (int)batch.TimeWindow.TotalMinutes,
            StartedAt = batch.StartedAt,
            CompletedAt = batch.CompletedAt,
            ProcessedCount = batch.ProcessedCount,
            SkippedCount = batch.SkippedCount,
            FailedCount = batch.FailedCount,
            Status = batch.Status.ToString(),
            ProcessedUrls = batch.ProcessedUrls.ToList(),
            FailedUrls = batch.FailedUrls.ToList()
        };
}