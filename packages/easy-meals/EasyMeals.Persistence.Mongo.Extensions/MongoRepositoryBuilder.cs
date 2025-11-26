using EasyMeals.Persistence.Abstractions.Repositories;
using EasyMeals.Persistence.Mongo.Documents;
using EasyMeals.Persistence.Mongo.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace EasyMeals.Persistence.Mongo.Extensions;

/// <summary>
///     Fluent builder for configuring MongoDB repositories.
/// </summary>
public sealed class MongoRepositoryBuilder
{
	private readonly IServiceCollection _services;
	private readonly List<Func<IServiceProvider, Task>> _indexCreators = [];
	private readonly List<(Type repoType, Type repoImplType)> _repositories = [];
	private readonly HashSet<Type> _documentTypes = [];

	internal MongoRepositoryBuilder(IServiceCollection services) => _services = services ?? throw new ArgumentNullException(nameof(services));

	/// <summary>
	///     Registers a custom repository with its implementation and document type.
	///     Use this for domain-specific repository interfaces.
	/// </summary>
	/// <typeparam name="TRepository">The repository interface type.</typeparam>
	/// <typeparam name="TRepositoryImpl">The repository implementation type.</typeparam>
	/// <typeparam name="TDocument">The document type for index creation.</typeparam>
	/// <returns>Builder for method chaining.</returns>
	public MongoRepositoryBuilder AddRepository<TRepository, TRepositoryImpl, TDocument>()
		where TRepository : class
		where TRepositoryImpl : class, TRepository
		where TDocument : BaseDocument
	{
		_repositories.Add((typeof(TRepository), typeof(TRepositoryImpl)));
		_documentTypes.Add(typeof(TDocument));

		return this;
	}

	/// <summary>
	///     Registers a generic read-write repository for a document type.
	///     Enables injection of IRepository&lt;TDocument, string&gt;, IReadRepository&lt;TDocument, string&gt;,
	///     and IWriteRepository&lt;TDocument, string&gt;.
	/// </summary>
	/// <typeparam name="TDocument">The document type.</typeparam>
	/// <returns>Builder for method chaining.</returns>
	public MongoRepositoryBuilder AddGenericRepository<TDocument>()
		where TDocument : BaseDocument
	{
		_repositories.Add((
			typeof(IRepository<TDocument, string>),
			typeof(MongoRepository<TDocument>)));

		_repositories.Add((
			typeof(IReadRepository<TDocument, string>),
			typeof(MongoRepository<TDocument>)));

		_repositories.Add((
			typeof(IWriteRepository<TDocument, string>),
			typeof(MongoRepository<TDocument>)));

		_documentTypes.Add(typeof(TDocument));

		return this;
	}

	/// <summary>
	///     Registers a read-only generic repository for a document type.
	///     Only enables injection of IReadRepository&lt;TDocument, string&gt;.
	///     Use this when a service should only read data, not modify it.
	/// </summary>
	/// <typeparam name="TDocument">The document type.</typeparam>
	/// <returns>Builder for method chaining.</returns>
	public MongoRepositoryBuilder AddReadOnlyRepository<TDocument>()
		where TDocument : BaseDocument
	{
		_repositories.Add((
			typeof(IReadRepository<TDocument, string>),
			typeof(MongoRepository<TDocument>)));

		_documentTypes.Add(typeof(TDocument));

		return this;
	}

	/// <summary>
	///     Adds default indexes for BaseDocument fields across all collections.
	///     Creates indexes for CreatedAt, UpdatedAt that are common to all documents.
	/// </summary>
	/// <returns>Builder for method chaining.</returns>
	public MongoRepositoryBuilder WithDefaultIndexes()
	{
		_indexCreators.Add(sp => MongoIndexConfiguration.CreateBaseDocumentIndexesAsync(sp, _documentTypes));
		return this;
	}

	/// <summary>
	///     Gets the registered index creators.
	/// </summary>
	internal IReadOnlyCollection<Func<IServiceProvider, Task>> GetIndexCreators() => _indexCreators.AsReadOnly();

	/// <summary>
	///     Gets the registered repository types.
	/// </summary>
	internal IReadOnlyCollection<(Type repoType, Type repoImplType)> GetRepositories() => _repositories.AsReadOnly();
}