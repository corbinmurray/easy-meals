using EasyMeals.RecipeEngine.Domain.Entities;
using EasyMeals.RecipeEngine.Domain.Repositories;
using EasyMeals.RecipeEngine.Infrastructure.Documents;
using EasyMeals.Shared.Data.Repositories;
using MongoDB.Driver;

namespace EasyMeals.RecipeEngine.Infrastructure.Repositories;

/// <summary>
///     MongoDB implementation of IRecipeFingerprintRepository.
/// </summary>
public class RecipeFingerprintRepository(IMongoDatabase database, IClientSessionHandle? session = null)
	: MongoRepository<RecipeFingerprintDocument>(database, session), IRecipeFingerprintRepository
{
	public async Task<RecipeFingerprint?> GetByUrlAsync(
		string url,
		string providerId,
		CancellationToken cancellationToken = default)
	{
		RecipeFingerprintDocument? document = await GetFirstOrDefaultAsync(
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
		await ReplaceOneAsync(d => d.Id == document.Id, document, true, cancellationToken);
	}

	public async Task SaveBatchAsync(
		IEnumerable<RecipeFingerprint> fingerprints,
		CancellationToken cancellationToken = default)
	{
		List<RecipeFingerprintDocument> documents = fingerprints.Select(ToDocument).ToList();
		await InsertManyAsync(documents, cancellationToken);
	}

	public async Task<int> CountByProviderAsync(string providerId, CancellationToken cancellationToken = default)
	{
		long count = await CountAsync(d => d.ProviderId == providerId, cancellationToken);
		return (int)count;
	}

	private static RecipeFingerprint ToDomain(RecipeFingerprintDocument document) =>
		RecipeFingerprint.Create(
			document.FingerprintHash,
			document.ProviderId,
			document.RecipeUrl,
			document.RecipeId);

	private static RecipeFingerprintDocument ToDocument(RecipeFingerprint fingerprint) =>
		new()
		{
			Id = fingerprint.Id.ToString(),
			ProviderId = fingerprint.ProviderId,
			RecipeUrl = fingerprint.RecipeUrl,
			FingerprintHash = fingerprint.FingerprintHash,
			RecipeId = fingerprint.RecipeId,
			ProcessedAt = fingerprint.ProcessedAt
		};
}