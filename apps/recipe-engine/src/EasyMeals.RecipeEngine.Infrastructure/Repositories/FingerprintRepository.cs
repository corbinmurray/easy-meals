using System.Text;
using EasyMeals.RecipeEngine.Domain.Entities;
using EasyMeals.RecipeEngine.Domain.Repositories;
using EasyMeals.RecipeEngine.Infrastructure.Documents.Fingerprint;
using EasyMeals.Shared.Data.Repositories;
using MongoDB.Driver;

namespace EasyMeals.RecipeEngine.Infrastructure.Repositories;

public sealed class FingerprintRepository(IMongoDatabase database, IClientSessionHandle? session = null)
	: MongoRepository<FingerprintDocument>(database, session), IFingerprintRepository
{
	public async Task<Fingerprint> AddAsync(Fingerprint fingerprint, CancellationToken cancellationToken = default)
	{
		FingerprintDocument doc = ToDocument(fingerprint);
		FingerprintDocument savedDoc = await InsertOneAsync(doc, cancellationToken);
		return ToDomain(savedDoc);
	}

	public async Task<Fingerprint> UpdateAsync(Fingerprint fingerprint, CancellationToken cancellationToken = default) =>
		throw new NotImplementedException();

	public async Task<bool> ExistsAsync(string fingerprintHash, CancellationToken cancellationToken = default) => throw new NotImplementedException();

	private static FingerprintDocument ToDocument(Fingerprint fingerprint)
	{
		return new FingerprintDocument
		{
			Id = fingerprint.Id.ToString(),
			ContentHash = fingerprint.ContentHash,
			ContentSizeBytes = fingerprint.ContentSizeBytes,
			CreatedAt = fingerprint.CreatedAt,
			ErrorMessage = fingerprint.ErrorMessage,
			FingerprintHash = fingerprint.FingerprintHash,
			Metadata = fingerprint.Metadata.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
			ProcessedAt = fingerprint.ProcessedAt,
			ProviderName = fingerprint.ProviderName,
			Quality = fingerprint.Quality,
			RecipeId = fingerprint.RecipeId.ToString(),
			ScrapedAt = fingerprint.ScrapedAt,
			Status = fingerprint.Status,
			UpdatedAt = fingerprint.UpdatedAt,
			Url = fingerprint.Url,
			Version = 1 // TOOD: How should we handle versioning of these documents?
		};
	}

	private static Fingerprint ToDomain(FingerprintDocument fingerprintDocument) =>
		Fingerprint.Reconstitute(
			Guid.Parse(fingerprintDocument.Id),
			fingerprintDocument.Url,
			fingerprintDocument.ContentHash,
			fingerprintDocument.FingerprintHash,
			fingerprintDocument.ContentSizeBytes,
			fingerprintDocument.ScrapedAt,
			fingerprintDocument.ProviderName,
			fingerprintDocument.Status,
			fingerprintDocument.Quality,
			fingerprintDocument.ErrorMessage,
			fingerprintDocument.Metadata ?? [],
			fingerprintDocument.ProcessedAt,
			!string.IsNullOrWhiteSpace(fingerprintDocument.RecipeId) ? Guid.Parse(fingerprintDocument.RecipeId) : Guid.Empty,
			fingerprintDocument.CreatedAt,
			fingerprintDocument.UpdatedAt);
}