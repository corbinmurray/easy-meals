using EasyMeals.Shared.Data.Attributes;
using EasyMeals.Shared.Data.Configuration;
using EasyMeals.Shared.Data.Documents;
using EasyMeals.Shared.Data.Documents.Recipe;
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
    private readonly List<Func<IServiceProvider, Task>> _indexCreators = [];
    private readonly List<(Type repoType, Type repoImplType)> _repositories = [];
    private readonly HashSet<Type> _documentTypes = [];

    internal EasyMealsRepositoryBuilder(IServiceCollection services) => _services = services ?? throw new ArgumentNullException(nameof(services));

    /// <summary>
    ///     Flag to determine if shared repositories should be added to the DI services collection.
    /// </summary>
    public bool IncludeSharedRepositories { get; set; } = false;

    /// <summary>
    ///     Registers a repository with its implementation and document types
    /// </summary>
    /// <typeparam name="TRepository"></typeparam>
    /// <typeparam name="TRepositoryImpl"></typeparam>
    /// <typeparam name="TDocument"></typeparam>
    /// <returns></returns>
    public EasyMealsRepositoryBuilder AddRepository<TRepository, TRepositoryImpl, TDocument>() where TDocument : BaseDocument
    {
        _repositories.Add((typeof(TRepository), typeof(TRepositoryImpl)));
        _documentTypes.Add(typeof(TDocument));

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
            // Ensure the database exists
            var database = serviceProvider.GetRequiredService<IMongoDatabase>();

            // Register repositories in DI
            foreach ((Type repoType, Type repoImplType) in _repositories)
            {
                _services.AddScoped(repoType, repoImplType);
            }

            if (IncludeSharedRepositories) AddRepository<IRecipeRepository, RecipeRepository, RecipeDocument>();

            // Ensure collections exist for all registered repositories
            var docTypes = new HashSet<Type>(_documentTypes);
            List<string> collectionNames = await (await database.ListCollectionNamesAsync()).ToListAsync();
            foreach (string collectionName in docTypes.Select(GetCollectionName).Where(collectionName => !collectionNames.Contains(collectionName)))
            {
                await database.CreateCollectionAsync(collectionName);
            }

            // Create indexes
            foreach (Func<IServiceProvider, Task> indexCreator in _indexCreators)
            {
                await indexCreator(serviceProvider);
            }

            return _services;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
        finally
        {
            await serviceProvider.DisposeAsync();
        }
    }

    /// <summary>
    ///     Registers a repository for a specific repository type
    /// </summary>
    private void RegisterRepository(Type repositoryType, RepositoryPermissions permissions)
    {
        // Extract the document type from the repository interface (assumes TRepository is IMongoRepository<TDocument>)
        Type[] genericArgs = repositoryType.GetGenericArguments();
        if (genericArgs.Length == 0 || !typeof(BaseDocument).IsAssignableFrom(genericArgs[0]))
            throw new InvalidOperationException(
                $"Repository type {repositoryType} must be IMongoRepository<TDocument> where TDocument : BaseDocument.");
        Type documentType = genericArgs[0];

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
    ///     Gets the collection name for a document type
    /// </summary>
    private static string GetCollectionName<TDocument>() where TDocument : BaseDocument => GetCollectionName(typeof(TDocument));

    private static string GetCollectionName(Type documentType)
    {
        var attribute = documentType.GetCustomAttributes(typeof(BsonCollectionAttribute), true)
            .FirstOrDefault() as BsonCollectionAttribute;
        return attribute?.CollectionName ?? documentType.Name.ToLowerInvariant();
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