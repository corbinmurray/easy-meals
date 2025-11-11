using EasyMeals.RecipeEngine.Domain.Entities;
using EasyMeals.RecipeEngine.Domain.Interfaces;
using EasyMeals.RecipeEngine.Infrastructure.Documents.SagaState;
using EasyMeals.Shared.Data.Repositories;
using MongoDB.Driver;

namespace EasyMeals.RecipeEngine.Infrastructure.Repositories;

public class SagaStateRepository(IMongoDatabase database, IClientSessionHandle? session = null)
	: MongoRepository<SagaStateDocument>(database, session), ISagaStateRepository
{
	public async Task<SagaState?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
		(await base.GetByIdAsync(id.ToString(), cancellationToken))?.ToDomain();

	public async Task<SagaState?> GetByCorrelationIdAsync(Guid correlationId, CancellationToken cancellationToken = default)
	{
		FilterDefinition<SagaStateDocument>? filter = Builders<SagaStateDocument>.Filter.Eq(s => s.CorrelationId, correlationId.ToString());
		SagaStateDocument? document = await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
		return document?.ToDomain();
	}

	public async Task<IEnumerable<SagaState>> GetBySagaTypeAsync(string sagaType, CancellationToken cancellationToken = default) =>
		throw new NotImplementedException();

	public async Task<IEnumerable<SagaState>> GetByStatusAsync(SagaStatus status, CancellationToken cancellationToken = default) =>
		throw new NotImplementedException();

	public async Task<IEnumerable<SagaState>> GetResumableAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();

	public async Task<IEnumerable<SagaState>> GetFailedForRetryAsync(int maxRetries = 3, TimeSpan retryDelay = default,
		CancellationToken cancellationToken = default) => throw new NotImplementedException();

	public async Task<IEnumerable<SagaState>> GetTimedOutAsync(TimeSpan timeout, CancellationToken cancellationToken = default) =>
		throw new NotImplementedException();

	public async Task<IEnumerable<SagaState>> GetStaleAsync(TimeSpan maxAge, SagaStatus? statusFilter = null,
		CancellationToken cancellationToken = default) => throw new NotImplementedException();

	public async Task<SagaState> AddAsync(SagaState sagaState, CancellationToken cancellationToken = default)
		=> (await InsertOneAsync(SagaStateDocument.FromDomain(sagaState), cancellationToken)).ToDomain();

	public async Task<SagaState> UpdateAsync(SagaState sagaState, CancellationToken cancellationToken = default)
	{
		SagaStateDocument document = SagaStateDocument.FromDomain(sagaState);
		await ReplaceByIdAsync(document.Id, document, cancellationToken);
		return sagaState;
	}

	public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotImplementedException();

	public async Task<int> DeleteStaleAsync(TimeSpan maxAge, SagaStatus? statusFilter = null, CancellationToken cancellationToken = default) =>
		throw new NotImplementedException();

	public async Task<bool> ExistsByCorrelationIdAsync(Guid correlationId, CancellationToken cancellationToken = default) =>
		throw new NotImplementedException();

	public async Task<SagaStateStatistics> GetStatisticsAsync(DateTime? since = null, string? sagaType = null,
		CancellationToken cancellationToken = default) => throw new NotImplementedException();
}