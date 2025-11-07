using EasyMeals.RecipeEngine.Domain.Entities;
using EasyMeals.RecipeEngine.Domain.Repositories;
using EasyMeals.RecipeEngine.Infrastructure.Documents;
using EasyMeals.Shared.Data.Repositories;
using MongoDB.Driver;

namespace EasyMeals.RecipeEngine.Infrastructure.Repositories;

/// <summary>
///     MongoDB implementation of IIngredientMappingRepository.
/// </summary>
public class IngredientMappingRepository : MongoRepository<IngredientMappingDocument>, IIngredientMappingRepository
{
	public IngredientMappingRepository(IMongoDatabase database, IClientSessionHandle? session = null)
		: base(database, session)
	{
	}

	public async Task<IngredientMapping?> GetByCodeAsync(
		string providerId,
		string providerCode,
		CancellationToken cancellationToken = default)
	{
		var document = await base.GetFirstOrDefaultAsync(
			d => d.ProviderId == providerId && d.ProviderCode == providerCode,
			cancellationToken);

		return document != null ? ToDomain(document) : null;
	}

	public async Task<IEnumerable<IngredientMapping>> GetAllByProviderAsync(
		string providerId,
		CancellationToken cancellationToken = default)
	{
		var documents = await base.GetAllAsync(d => d.ProviderId == providerId, cancellationToken);
		return documents.Select(ToDomain);
	}

	public async Task SaveAsync(IngredientMapping mapping, CancellationToken cancellationToken = default)
	{
		IngredientMappingDocument document = ToDocument(mapping);
		await base.ReplaceOneAsync(d => d.Id == document.Id, document, upsert: true, cancellationToken);
	}

	public async Task<IEnumerable<IngredientMapping>> GetUnmappedCodesAsync(
		string providerId,
		CancellationToken cancellationToken = default)
	{
		var documents = await base.GetAllAsync(
			d => d.ProviderId == providerId && (d.CanonicalForm == null || d.CanonicalForm == ""),
			cancellationToken);

		return documents.Select(ToDomain).ToList();
	}

	private static IngredientMapping ToDomain(IngredientMappingDocument document) =>
		IngredientMapping.Create(
			document.ProviderId,
			document.ProviderCode,
			document.CanonicalForm);

	private static IngredientMappingDocument ToDocument(IngredientMapping mapping) =>
		new()
		{
			Id = Guid.NewGuid().ToString(),
			ProviderId = mapping.ProviderId,
			ProviderCode = mapping.ProviderCode,
			CanonicalForm = mapping.CanonicalForm,
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		};
}