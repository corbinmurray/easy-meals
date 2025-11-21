using EasyMeals.Persistence.Mongo.Documents;
using Microsoft.Extensions.DependencyInjection;

namespace EasyMeals.Persistence.Mongo.Extensions;

/// <summary>
///  Fluent builder for configuring MongoDB repositories.
/// </summary>
public sealed class MongoRepositoryBuilder
{
	private readonly IServiceCollection _services;
	private readonly List<Func<IServiceProvider, Task>> _indexCreators = [];
	private readonly List<(Type repoType, Type repoImplType)> _repositories = [];
	private readonly HashSet<Type> _documentTypes = [];
	
	internal MongoRepositoryBuilder(IServiceCollection services)
	{
		_services = services ?? throw new ArgumentNullException(nameof(services));
	}
	
	/// <summary>
	///     Registers a repository with its implementation and document types
	/// </summary>
	/// <typeparam name="TRepository"></typeparam>
	/// <typeparam name="TRepositoryImpl"></typeparam>
	/// <typeparam name="TDocument"></typeparam>
	/// <returns></returns>
	public MongoRepositoryBuilder AddRepository<TRepository, TRepositoryImpl, TDocument>()
		where TDocument : BaseDocument
	{
		_repositories.Add((typeof(TRepository), typeof(TRepositoryImpl)));
		_documentTypes.Add(typeof(TDocument));

		return this;
	}

	
	/// <summary>
	///     Adds default indexes for BaseDocument fields across all collections
	///     Creates indexes for CreatedAt, UpdatedAt that are common to all documents
	/// </summary>
	/// <returns>Builder for method chaining</returns>
	public MongoRepositoryBuilder WithDefaultIndexes()
	{
		_indexCreators.Add(sp => MongoIndexConfiguration.CreateBaseDocumentIndexesAsync(sp, _documentTypes));
		return this;
	}
}