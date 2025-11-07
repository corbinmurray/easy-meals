using EasyMeals.RecipeEngine.Domain.Entities;
using EasyMeals.RecipeEngine.Domain.Repositories;
using EasyMeals.RecipeEngine.Infrastructure.Documents;
using EasyMeals.Shared.Data.Repositories;
using MongoDB.Driver;

namespace EasyMeals.RecipeEngine.Infrastructure.Repositories;

/// <summary>
///     MongoDB implementation of IRecipeFingerprintRepository.
/// </summary>
public class RecipeFingerprintRepository : MongoRepository<RecipeFingerprintDocument>, IRecipeFingerprintRepository
{
	public RecipeFingerprintRepository(IMongoDatabase database, IClientSessionHandle? session = null)
		: base(database, session)
	{
	}

	public async Task<RecipeFingerprint?> GetByUrlAsync(
		string url,
		string providerId,
		CancellationToken cancellationToken = default)
	{
		var document = await base.GetFirstOrDefaultAsync(
			d => d.RecipeUrl == url && d.ProviderId == providerId,
			cancellationToken);

		return document != null ? ToDomain(document) : null;
	}

	public async Task<bool> ExistsAsync(
		string url,
		string providerId,
		CancellationToken cancellationToken = default)
	{
		return await base.ExistsAsync(
			d => d.RecipeUrl == url && d.ProviderId == providerId,
			cancellationToken);
	}

	public async Task SaveAsync(RecipeFingerprint fingerprint, CancellationToken cancellationToken = default)
	{
		RecipeFingerprintDocument document = ToDocument(fingerprint);
		await base.ReplaceOneAsync(d => d.Id == document.Id, document, upsert: true, cancellationToken);
	}

	public async Task SaveBatchAsync(
		IEnumerable<RecipeFingerprint> fingerprints,
		CancellationToken cancellationToken = default)
	{
		List<RecipeFingerprintDocument> documents = fingerprints.Select(ToDocument).ToList();
		await base.InsertManyAsync(documents, cancellationToken);
	}

	public async Task<int> CountByProviderAsync(string providerId, CancellationToken cancellationToken = default)
	{
		var count = await base.CountAsync(d => d.ProviderId == providerId, cancellationToken);
		return (int)count;
	}

	private static RecipeFingerprint ToDomain(RecipeFingerprintDocument document) =>
		new(document.ProviderId, document.RecipeUrl, document.FingerprintHash);

	private static RecipeFingerprintDocument ToDocument(RecipeFingerprint fingerprint) =>
		new()
		{
			Id = Guid.NewGuid().ToString(),
			ProviderId = fingerprint.ProviderId,
			RecipeUrl = fingerprint.RecipeUrl,
			FingerprintHash = fingerprint.FingerprintHash,
			ProcessedAt = DateTime.UtcNow
		};
}