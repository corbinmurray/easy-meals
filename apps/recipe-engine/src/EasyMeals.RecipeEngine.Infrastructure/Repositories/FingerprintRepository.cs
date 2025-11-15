using EasyMeals.RecipeEngine.Domain.Entities;
using EasyMeals.RecipeEngine.Domain.Interfaces;
using EasyMeals.RecipeEngine.Domain.Repositories;
using EasyMeals.RecipeEngine.Infrastructure.Documents.Fingerprint;
using EasyMeals.Shared.Data.Repositories;
using MongoDB.Driver;

public class FingerprintRepository(IMongoDatabase database, IClientSessionHandle? session = null)
    : MongoRepository<FingerprintDocument>(database, session), IFingerprintRepository
{
    public Task<Fingerprint> AddAsync(Fingerprint fingerprint) => throw new NotImplementedException();

    public Task<int> DeleteStaleAsync(TimeSpan maxAge) => throw new NotImplementedException();

    public Task<Fingerprint?> FindByContentHashAsync(string contentHash) => throw new NotImplementedException();

    public Task<IEnumerable<Fingerprint>> FindByProviderAsync(string provider, DateTime? since = null) => throw new NotImplementedException();

    public Task<Fingerprint?> FindByUrlAsync(string url) => throw new NotImplementedException();

    public Task<IEnumerable<Fingerprint>> FindReadyForProcessingAsync(int maxCount = 100) => throw new NotImplementedException();

    public Task<IEnumerable<Fingerprint>> FindRetryableFingerprintsAsync(int maxRetries = 3, TimeSpan retryDelay = default) =>
        throw new NotImplementedException();

    public Task<IEnumerable<Fingerprint>> FindStaleAsync(TimeSpan maxAge, int maxCount = 1000) => throw new NotImplementedException();

    public Task<FingerprintStatistics> GetStatisticsAsync(DateTime? since = null, string? provider = null) => throw new NotImplementedException();

    public Task<bool> HasBeenScrapedRecentlyAsync(string url, TimeSpan timeWindow) => throw new NotImplementedException();

    public Task<Fingerprint> UpdateAsync(Fingerprint fingerprint) => throw new NotImplementedException();
}