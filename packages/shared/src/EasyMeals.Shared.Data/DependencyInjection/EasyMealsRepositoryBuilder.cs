using EasyMeals.Shared.Data.Attributes;
using EasyMeals.Shared.Data.Configuration;
using EasyMeals.Shared.Data.Documents;
using EasyMeals.Shared.Data.Repositories;
using EasyMeals.Shared.Data.Repositories.Recipe;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace EasyMeals.Shared.Data.DependencyInjection;

/// <summary>
///     Fluent builder for configuring EasyMeals repository registration
///     Provides a modern, intuitive API for MongoDB repository setup
/// </summary>
public class EasyMealsRepositoryBuilder
{
	private readonly IServiceCollection _services;
	private readonly List<(Type DocumentType, RepositoryPermissions Permissions)> _repositories = [];
	private readonly List<Type> _sharedRepositories = [];
	private readonly List<Func<IServiceProvider, Task>> _indexCreators = [];

	internal EasyMealsRepositoryBuilder(IServiceCollection services) => _services = services ?? throw new ArgumentNullException(nameof(services));

	/// <summary>
	///     Adds a custom repository for the specified document type
	/// </summary>
	/// <typeparam name="TDocument">The document type extending BaseDocument</typeparam>
	/// <param name="permissions">Repository access permissions</param>
	/// <returns>Builder for method chaining</returns>
	public EasyMealsRepositoryBuilder AddRepository<TDocument>(RepositoryPermissions permissions = RepositoryPermissions.ReadWrite)
		where TDocument : BaseDocument
	{
		_repositories.Add((typeof(TDocument), permissions));
		return this;
	}

	/// <summary>
	///     Adds a shared repository that's predefined in the EasyMeals system
	/// </summary>
	/// <typeparam name="TSharedRepository">The shared repository interface type</typeparam>
	/// <returns>Builder for method chaining</returns>
	public EasyMealsRepositoryBuilder AddSharedRepository<TSharedRepository>()
		where TSharedRepository : ISharedMongoRepository<BaseDocument>
	{
		_sharedRepositories.Add(typeof(TSharedRepository));
		return this;
	}

	/// <summary>
	///     Adds default indexes for BaseDocument fields across all collections
	///     Creates indexes for Id, CreatedAt, UpdatedAt that are common to all documents
	/// </summary>
	/// <returns>Builder for method chaining</returns>
	public EasyMealsRepositoryBuilder WithDefaultIndexes()
	{
		_indexCreators.Add(async serviceProvider =>
		{
			var database = serviceProvider.GetRequiredService<IMongoDatabase>();
			await MongoIndexConfiguration.CreateBaseDocumentIndexesAsync(database);
		});
		return this;
	}

	/// <summary>
	///     Adds soft-deletable document indexes for collections that extend BaseSoftDeletableDocument
	/// </summary>
	/// <typeparam name="TDocument">Document type extending BaseSoftDeletableDocument</typeparam>
	/// <returns>Builder for method chaining</returns>
	public EasyMealsRepositoryBuilder WithSoftDeletableIndexes<TDocument>()
		where TDocument : BaseSoftDeletableDocument
	{
		_indexCreators.Add(async serviceProvider =>
		{
			var database = serviceProvider.GetRequiredService<IMongoDatabase>();
			await MongoIndexConfiguration.CreateSoftDeletableIndexesAsync<TDocument>(database);
		});
		return this;
	}

	/// <summary>
	///     Adds custom indexes for a specific document type
	/// </summary>
	/// <typeparam name="TDocument">The document type</typeparam>
	/// <param name="indexCreator">Custom index creation logic</param>
	/// <returns>Builder for method chaining</returns>
	public EasyMealsRepositoryBuilder WithCustomIndexes<TDocument>(
		Func<IMongoCollection<TDocument>, Task> indexCreator)
		where TDocument : BaseDocument
	{
		ArgumentNullException.ThrowIfNull(indexCreator, nameof(indexCreator));

		_indexCreators.Add(async serviceProvider =>
		{
			var database = serviceProvider.GetRequiredService<IMongoDatabase>();
			IMongoCollection<TDocument>? collection = database.GetCollection<TDocument>(GetCollectionName<TDocument>());
			await indexCreator(collection);
		});
		return this;
	}

	/// <summary>
	///     Creates an index builder for advanced index configuration
	/// </summary>
	/// <returns>Index builder for fluent index creation</returns>
	public EasyMealsIndexBuilder ConfigureIndexes() => new(this);

	/// <summary>
	///     Ensures the database and all configurations are set up successfully
	///     Must be called at the end of the configuration chain
	/// </summary>
	/// <returns>Task representing the setup completion</returns>
	/// <exception cref="InvalidOperationException">Thrown when MongoDB configuration is missing</exception>
	public async Task<IServiceCollection> EnsureDatabaseAsync()
	{
		// Validate that MongoDB is configured
		ServiceProvider serviceProvider = _services.BuildServiceProvider(false);

		try
		{
			var database = serviceProvider.GetRequiredService<IMongoDatabase>();

			// Register all repositories
			RegisterRepositories();

			// Register automatic health checks
			RegisterHealthChecks();

			// Create indexes
			foreach (Func<IServiceProvider, Task> indexCreator in _indexCreators)
			{
				await indexCreator(serviceProvider);
			}

			return _services;
		}
		catch (InvalidOperationException)
		{
			throw new InvalidOperationException(
				"MongoDB configuration is missing. Please call one of the AddEasyMealsDataMongoDB methods before registering repositories. " +
				"Example: services.AddEasyMealsDataMongoDB(connectionString, databaseName)");
		}
		finally
		{
			await serviceProvider.DisposeAsync();
		}
	}

	/// <summary>
	///     Registers all configured repositories with the DI container
	/// </summary>
	private void RegisterRepositories()
	{
		// Register custom repositories
		foreach ((Type documentType, RepositoryPermissions permissions) in _repositories)
		{
			RegisterRepositoryForDocument(documentType, permissions);
		}

		// Register shared repositories
		foreach (Type sharedRepoType in _sharedRepositories)
		{
			RegisterSharedRepositoryType(sharedRepoType);
		}
	}

	/// <summary>
	///     Registers a repository for a specific document type
	/// </summary>
	private void RegisterRepositoryForDocument(Type documentType, RepositoryPermissions permissions)
	{
		// Create generic repository types
		Type mongoRepoType = typeof(IMongoRepository<>).MakeGenericType(documentType);
		Type readOnlyRepoType = typeof(IReadOnlyMongoRepository<>).MakeGenericType(documentType);
		Type implType = typeof(MongoRepository<>).MakeGenericType(documentType);

		if (permissions == RepositoryPermissions.ReadWrite)
		{
			// Register full repository
			_services.AddScoped(mongoRepoType, implType);
			_services.AddScoped(readOnlyRepoType, serviceProvider =>
				serviceProvider.GetRequiredService(mongoRepoType));
		}
		else
		{
			// Register read-only repository
			_services.AddScoped(readOnlyRepoType, implType);
		}
	}

	/// <summary>
	///     Registers a shared repository type
	/// </summary>
	private void RegisterSharedRepositoryType(Type sharedRepoType)
	{
		// This would need to be expanded based on your shared repository implementations
		// For now, we'll use a simple mapping approach
		if (sharedRepoType.Name == nameof(IRecipeRepository)) _services.AddScoped<IRecipeRepository, RecipeRepository>();
		// Add more shared repository mappings as needed
	}

	/// <summary>
	///     Registers health checks for all configured repositories
	/// </summary>
	private void RegisterHealthChecks()
	{
		if (_repositories.Count > 0 || _sharedRepositories.Count > 0) _services.AddEasyMealsDataHealthChecks();
	}

	/// <summary>
	///     Gets the collection name for a document type
	/// </summary>
	private static string GetCollectionName<TDocument>() where TDocument : BaseDocument
	{
		var attribute = typeof(TDocument).GetCustomAttributes(typeof(BsonCollectionAttribute), true)
			.FirstOrDefault() as BsonCollectionAttribute;

		return attribute?.CollectionName ?? typeof(TDocument).Name.ToLowerInvariant();
	}
}

/// <summary>
///     Index configuration builder for fluent index creation
/// </summary>
public class EasyMealsIndexBuilder
{
	private readonly EasyMealsRepositoryBuilder _repositoryBuilder;

	internal EasyMealsIndexBuilder(EasyMealsRepositoryBuilder repositoryBuilder) =>
		_repositoryBuilder = repositoryBuilder ?? throw new ArgumentNullException(nameof(repositoryBuilder));

	/// <summary>
	///     Creates a compound index on the specified fields
	/// </summary>
	/// <typeparam name="TDocument">The document type</typeparam>
	/// <param name="indexFields">Index field definitions</param>
	/// <param name="options">Index creation options</param>
	/// <returns>Index builder for chaining</returns>
	public EasyMealsIndexBuilder CreateCompoundIndex<TDocument>(
		IndexKeysDefinition<TDocument> indexFields,
		CreateIndexOptions? options = null)
		where TDocument : BaseDocument
	{
		ArgumentNullException.ThrowIfNull(indexFields, nameof(indexFields));

		_repositoryBuilder.WithCustomIndexes<TDocument>(async collection =>
		{
			var indexModel = new CreateIndexModel<TDocument>(indexFields, options);
			await collection.Indexes.CreateOneAsync(indexModel);
		});

		return this;
	}

	/// <summary>
	///     Creates a text search index on the specified fields
	/// </summary>
	/// <typeparam name="TDocument">The document type</typeparam>
	/// <param name="textFields">Text fields for search index</param>
	/// <param name="options">Index creation options</param>
	/// <returns>Index builder for chaining</returns>
	public EasyMealsIndexBuilder CreateTextIndex<TDocument>(
		IndexKeysDefinition<TDocument> textFields,
		CreateIndexOptions? options = null)
		where TDocument : BaseDocument
	{
		ArgumentNullException.ThrowIfNull(textFields, nameof(textFields));

		_repositoryBuilder.WithCustomIndexes<TDocument>(async collection =>
		{
			var indexModel = new CreateIndexModel<TDocument>(textFields, options);
			await collection.Indexes.CreateOneAsync(indexModel);
		});

		return this;
	}

	/// <summary>
	///     Returns to the repository builder for final configuration
	/// </summary>
	/// <returns>Repository builder</returns>
	public EasyMealsRepositoryBuilder BuildIndexes() => _repositoryBuilder;
}