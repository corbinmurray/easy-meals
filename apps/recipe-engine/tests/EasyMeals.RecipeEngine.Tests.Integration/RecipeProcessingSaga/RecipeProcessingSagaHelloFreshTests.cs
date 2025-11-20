using EasyMeals.RecipeEngine.Domain.Repositories;
using EasyMeals.RecipeEngine.Infrastructure.Repositories;
using MongoDB.Driver;
using Testcontainers.MongoDb;

namespace EasyMeals.RecipeEngine.Tests.Integration.RecipeProcessingSaga;

public sealed class RecipeProcessingSagaHelloFreshTests : IAsyncLifetime
{
	private MongoDbContainer? _mongoContainer;
	private IMongoDatabase? _mongoDatabase;
	private ISagaStateRepository _sagaStateRepository;

	public async Task InitializeAsync()
	{
		// Start MongoDB container for integration testing
		_mongoContainer = new MongoDbBuilder()
			.WithImage("mongo:8.0")
			.Build();

		await _mongoContainer.StartAsync();

		// Connect to MongoDB
		var mongoClient = new MongoClient(_mongoContainer.GetConnectionString());
		_mongoDatabase = mongoClient.GetDatabase("recipe-engine-test");

		_sagaStateRepository = new SagaStateRepository(_mongoDatabase);
	}

	public async Task DisposeAsync()
	{
		if (_mongoContainer is not null)
			await _mongoContainer.DisposeAsync();
	}

	[Fact]
	public async Task RecipeProcessingSaga_CompletesHelloFreshRecipe()
	{
		// Arrange
		
		
		// Act
		
		// Assert
	}
}