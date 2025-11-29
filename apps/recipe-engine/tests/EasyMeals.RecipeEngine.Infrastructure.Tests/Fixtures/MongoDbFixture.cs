using EasyMeals.Persistence.Mongo;
using EasyMeals.Persistence.Mongo.Options;
using MongoDB.Driver;
using Testcontainers.MongoDb;
using Xunit;

namespace EasyMeals.RecipeEngine.Infrastructure.Tests.Fixtures;

/// <summary>
/// Shared MongoDB Testcontainer fixture for integration tests.
/// Implements <see cref="IAsyncLifetime"/> for xUnit lifecycle management.
/// </summary>
public class MongoDbFixture : IAsyncLifetime
{
    private readonly MongoDbContainer _container = new MongoDbBuilder()
        .WithImage("mongo:7.0")
        .Build();

    /// <summary>
    /// Gets the connection string for the running MongoDB container.
    /// </summary>
    public string ConnectionString => _container.GetConnectionString();

    /// <summary>
    /// Gets a MongoDB client connected to the test container.
    /// </summary>
    public IMongoClient Client { get; private set; } = null!;

    /// <summary>
    /// Gets the test database instance.
    /// </summary>
    public IMongoDatabase Database { get; private set; } = null!;

    /// <summary>
    /// Gets the MongoContext for repository tests.
    /// </summary>
    public IMongoContext Context { get; private set; } = null!;

    /// <summary>
    /// Database name used for tests.
    /// </summary>
    public const string DatabaseName = "easy_meals_test";

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        Client = new MongoClient(ConnectionString);
        Database = Client.GetDatabase(DatabaseName);

        var options = new MongoDbOptions
        {
            ConnectionString = ConnectionString,
            DatabaseName = DatabaseName
        };
        Context = new MongoContext(Client, options);
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        await _container.StopAsync();
        await _container.DisposeAsync();
    }

    /// <summary>
    /// Drops the test database to ensure a clean state between test classes.
    /// </summary>
    public async Task ResetDatabaseAsync()
    {
        await Client.DropDatabaseAsync(DatabaseName);
    }
}

/// <summary>
/// Collection definition for MongoDB integration tests.
/// Tests in this collection share the same MongoDB container instance.
/// </summary>
[CollectionDefinition(Name)]
public class MongoDbTestCollection : ICollectionFixture<MongoDbFixture>
{
    public const string Name = "MongoDB";
}
