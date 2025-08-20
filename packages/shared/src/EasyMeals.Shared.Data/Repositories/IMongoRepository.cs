using EasyMeals.Shared.Data.Documents;

namespace EasyMeals.Shared.Data.Repositories;

/// <summary>
///     MongoDB-specific repository interface extending the generic repository pattern
///     Provides MongoDB-optimized operations while maintaining DDD principles
/// </summary>
/// <typeparam name="TDocument">The document type managed by this repository</typeparam>
public interface IMongoRepository<TDocument> : IReadOnlyMongoRepository<TDocument>, IWriteRepository<TDocument>
	where TDocument : BaseDocument
{
}