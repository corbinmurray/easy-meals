using System.Linq.Expressions;
using EasyMeals.Shared.Data.Documents;

namespace EasyMeals.Shared.Data.Repositories;

/// <summary>
///     Read-only MongoDB repository implementation
///     Enforces read-only access for bounded contexts with limited permissions
/// </summary>
/// <typeparam name="TDocument">The document type</typeparam>
public class ReadOnlyMongoRepository<TDocument>(IMongoRepository<TDocument> mongoRepository) : IReadOnlyRepository<TDocument>
	where TDocument : BaseDocument
{
	private readonly IMongoRepository<TDocument> _mongoRepository = mongoRepository ?? throw new ArgumentNullException(nameof(mongoRepository));

	public async Task<TDocument?> GetByIdAsync(string id, CancellationToken cancellationToken = default) =>
		await _mongoRepository.GetByIdAsync(id, cancellationToken);

	public async Task<IEnumerable<TDocument>> GetAllAsync(
		Expression<Func<TDocument, bool>>? filter = null,
		CancellationToken cancellationToken = default) =>
		await _mongoRepository.GetAllAsync(filter, cancellationToken);
}

/// <summary>
///     Read-only Recipe repository implementation
///     Provides controlled access to recipe data for applications with read-only permissions
/// </summary>
public class ReadOnlyRecipeRepository : ReadOnlyMongoRepository<RecipeDocument>, IReadOnlyRepository<RecipeDocument>
{
	private readonly IRecipeRepository _recipeRepository;

	public ReadOnlyRecipeRepository(IRecipeRepository recipeRepository)
		: base(recipeRepository) =>
		_recipeRepository = recipeRepository ?? throw new ArgumentNullException(nameof(recipeRepository));

	public async Task<IEnumerable<RecipeDocument>> SearchAsync(
		string searchTerm,
		CancellationToken cancellationToken = default) =>
		await _recipeRepository.SearchAsync(searchTerm, cancellationToken);
}