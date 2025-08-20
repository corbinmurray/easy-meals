using EasyMeals.Shared.Data.Configuration;
using EasyMeals.Shared.Data.Documents;
using EasyMeals.Shared.Data.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Compression;
using MongoDB.Driver.Core.Configuration;

namespace EasyMeals.Shared.Data.DependencyInjection;

/// <summary>
///     Extension methods for configuring EasyMeals MongoDB data services
///     Provides fluent configuration following the Dependency Inversion Principle
///     Supports MongoDB client configuration and flexible connection options
/// </summary>
public static class ServiceCollectionExtensions
{
	#region New Fluent API

	/// <summary>
	///     Adds a repository with the specified permissions using the fluent API
	///     Requires MongoDB to be configured first via AddEasyMealsDataMongoDB
	/// </summary>
	/// <typeparam name="TDocument">The document type extending BaseDocument</typeparam>
	/// <param name="services">The service collection</param>
	/// <param name="permissions">Repository access permissions (default: ReadWrite)</param>
	/// <returns>Repository builder for chaining</returns>
	/// <exception cref="InvalidOperationException">Thrown when MongoDB is not configured</exception>
	public static EasyMealsRepositoryBuilder AddEasyMealsRepository<TDocument>(
		this IServiceCollection services,
		RepositoryPermissions permissions = RepositoryPermissions.ReadWrite)
		where TDocument : BaseDocument
	{
		ValidateMongoDbConfiguration(services);

		return new EasyMealsRepositoryBuilder(services)
			.AddRepository<TDocument>(permissions);
	}

	/// <summary>
	///     Adds a read-only repository using the fluent API
	///     Requires MongoDB to be configured first via AddEasyMealsDataMongoDB
	/// </summary>
	/// <typeparam name="TDocument">The document type extending BaseDocument</typeparam>
	/// <param name="services">The service collection</param>
	/// <returns>Repository builder for chaining</returns>
	/// <exception cref="InvalidOperationException">Thrown when MongoDB is not configured</exception>
	public static EasyMealsRepositoryBuilder AddReadOnlyEasyMealsRepository<TDocument>(
		this IServiceCollection services)
		where TDocument : BaseDocument
	{
		return services.AddEasyMealsRepository<TDocument>(RepositoryPermissions.Read);
	}

	/// <summary>
	///     Starts building EasyMeals repositories with fluent configuration
	///     Requires MongoDB to be configured first via AddEasyMealsDataMongoDB
	/// </summary>
	/// <param name="services">The service collection</param>
	/// <returns>Repository builder for chaining</returns>
	/// <exception cref="InvalidOperationException">Thrown when MongoDB is not configured</exception>
	public static EasyMealsRepositoryBuilder ConfigureEasyMealsRepositories(
		this IServiceCollection services)
	{
		ValidateMongoDbConfiguration(services);
		return new EasyMealsRepositoryBuilder(services);
	}

	/// <summary>
	///     Validates that MongoDB has been configured
	/// </summary>
	private static void ValidateMongoDbConfiguration(IServiceCollection services)
	{
		var serviceProvider = services.BuildServiceProvider(validateScopes: false);
		try
		{
			serviceProvider.GetService<IMongoDatabase>();
		}
		catch
		{
			throw new InvalidOperationException(
				"MongoDB configuration is missing. Please call one of the AddEasyMealsDataMongoDB methods before registering repositories. " +
				"Example: services.AddEasyMealsDataMongoDB(connectionString, databaseName)");
		}
		finally
		{
			serviceProvider.Dispose();
		}
	}

	#endregion

	#region Core MongoDB Configuration (Existing)
	/// <summary>
	///     Adds EasyMeals data services with MongoDB using connection string
	///     Standard configuration for production and development environments
	/// </summary>
	/// <param name="services">The service collection</param>
	/// <param name="connectionString">MongoDB connection string</param>
	/// <param name="databaseName">Database name (optional, defaults to "easymealsprod")</param>
	/// <param name="configureClient">Optional MongoDB client configuration</param>
	/// <returns>Service collection for chaining</returns>
	public static IServiceCollection AddEasyMealsDataMongoDB(
		this IServiceCollection services,
		string connectionString,
		string databaseName,
		Action<MongoClientSettings>? configureClient = null)
	{
		return services.AddEasyMealsDataCore(clientSettings =>
		{
			// Parse connection string and apply custom settings
			MongoClientSettings? settings = MongoClientSettings.FromConnectionString(connectionString);

			// Apply custom configuration if provided
			configureClient?.Invoke(settings);

			return settings;
		}, databaseName);
	}

	/// <summary>
	///     Adds EasyMeals data services with MongoDB using custom client settings
	///     Advanced configuration for specialized deployment scenarios
	/// </summary>
	/// <param name="services">The service collection</param>
	/// <param name="clientSettings">MongoDB client settings</param>
	/// <param name="databaseName">Database name</param>
	/// <returns>Service collection for chaining</returns>
	public static IServiceCollection AddEasyMealsDataMongoDB(
		this IServiceCollection services,
		MongoClientSettings clientSettings,
		string databaseName)
	{
		return services.AddEasyMealsDataCore(_ => clientSettings, databaseName);
	}

	/// <summary>
	///     Adds EasyMeals data services with in-memory MongoDB for testing
	///     Uses Testcontainers or local MongoDB instance for development
	/// </summary>
	/// <param name="services">The service collection</param>
	/// <param name="databaseName">Database name (optional, defaults to test database)</param>
	/// <returns>Service collection for chaining</returns>
	public static IServiceCollection AddEasyMealsDataInMemory(
		this IServiceCollection services,
		string? databaseName = null)
	{
		string dbName = databaseName ?? $"easymealstests_{Guid.NewGuid():N}";

		return services.AddEasyMealsDataCore(clientSettings =>
		{
			// Use local MongoDB instance for testing
			var settings = new MongoClientSettings
			{
				Server = new MongoServerAddress("localhost", 27017),
				ConnectTimeout = TimeSpan.FromSeconds(5),
				ServerSelectionTimeout = TimeSpan.FromSeconds(5)
			};

			return settings;
		}, dbName);
	}

	/// <summary>
	///     Adds EasyMeals data services with MongoDB using strongly-typed options
	///     Preferred approach using Microsoft Options Pattern for configuration
	/// </summary>
	/// <param name="services">The service collection</param>
	/// <param name="configureOptions">Action to configure MongoDB options</param>
	/// <param name="registerSharedRepositories">Whether to register shared repositories (default: true)</param>
	/// <returns>Service collection for chaining</returns>
	public static IServiceCollection AddEasyMealsDataWithOptions(
		this IServiceCollection services,
		Action<MongoDbOptions>? configureOptions = null,
		bool registerSharedRepositories = true)
	{
		// Configure and validate options
		OptionsBuilder<MongoDbOptions> optionsBuilder = services.AddOptions<MongoDbOptions>()
			.BindConfiguration(MongoDbOptions.SectionName)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		// Apply additional configuration if provided
		if (configureOptions is not null) optionsBuilder.Configure(configureOptions);

		return services.AddEasyMealsDataCore(serviceProvider =>
		{
			MongoDbOptions mongoOptions = serviceProvider.GetRequiredService<IOptions<MongoDbOptions>>().Value;

			// Parse connection string and build settings
			MongoClientSettings? settings = MongoClientSettings.FromConnectionString(mongoOptions.ConnectionString);

			// Apply configuration from options
			settings.ApplicationName = mongoOptions.ApplicationName;
			settings.ConnectTimeout = TimeSpan.FromSeconds(mongoOptions.ConnectionTimeoutSeconds);
			settings.SocketTimeout = TimeSpan.FromSeconds(mongoOptions.SocketTimeoutSeconds);
			settings.ServerSelectionTimeout = TimeSpan.FromSeconds(mongoOptions.ServerSelectionTimeoutSeconds);
			settings.MaxConnectionPoolSize = mongoOptions.MaxConnectionPoolSize;
			settings.MinConnectionPoolSize = mongoOptions.MinConnectionPoolSize;
			settings.MaxConnectionIdleTime = TimeSpan.FromSeconds(mongoOptions.MaxIdleTimeSeconds);
			settings.RetryWrites = mongoOptions.RetryWrites;
			settings.RetryReads = mongoOptions.RetryReads;

			// Configure read preference
			settings.ReadPreference = mongoOptions.ReadPreference.ToLowerInvariant() switch
			{
				"secondary" => ReadPreference.Secondary,
				"secondarypreferred" => ReadPreference.SecondaryPreferred,
				"primarypreferred" => ReadPreference.PrimaryPreferred,
				"nearest" => ReadPreference.Nearest,
				_ => ReadPreference.Primary
			};

			if (!mongoOptions.EnableCompression)
				return settings;

			// Configure compression if enabled
			CompressorType compressor = mongoOptions.CompressionAlgorithm.ToLowerInvariant() switch
			{
				"snappy" => CompressorType.Snappy,
				"zstd" => CompressorType.ZStandard,
				_ => CompressorType.Zlib
			};

			settings.Compressors = [new CompressorConfiguration(compressor)];

			return settings;
		}, serviceProvider =>
		{
			MongoDbOptions mongoOptions = serviceProvider.GetRequiredService<IOptions<MongoDbOptions>>().Value;
			return mongoOptions.DatabaseName;
		}, registerSharedRepositories);
	}

	/// <summary>
	///     Core configuration method for MongoDB data services
	///     Registers all necessary services and repository implementations
	/// </summary>
	private static IServiceCollection AddEasyMealsDataCore(
		this IServiceCollection services,
		Func<IServiceProvider, MongoClientSettings> configureClientSettings,
		Func<IServiceProvider, string> getDatabaseName,
		bool registerSharedRepositories = true)
	{
		// Register MongoDB client as singleton
		services.AddSingleton<IMongoClient>(serviceProvider =>
		{
			MongoClientSettings settings = configureClientSettings(serviceProvider);
			return new MongoClient(settings);
		});

		// Register MongoDB database as scoped
		services.AddScoped<IMongoDatabase>(serviceProvider =>
		{
			var client = serviceProvider.GetRequiredService<IMongoClient>();
			return client.GetDatabase(getDatabaseName(serviceProvider));
		});

		// Register Unit of Work pattern for MongoDB
		services.AddScoped<IUnitOfWork, UnitOfWork>();
		services.AddScoped<IUnitOfWork, UnitOfWork>();

		// Register generic repository pattern
		services.AddScoped(typeof(IMongoRepository<>), typeof(MongoRepository<>));
		services.AddScoped(typeof(IMongoRepository<>), typeof(MongoRepository<>));

		// Register shared repositories only if requested
		if (registerSharedRepositories) services.AddSharedRepositories();

		return services;
	}

	/// <summary>
	///     Adds shared repositories that are used across multiple applications
	///     These repositories manage shared aggregates in the domain
	/// </summary>
	/// <param name="services">The service collection</param>
	/// <returns>Service collection for chaining</returns>
	public static IServiceCollection AddSharedRepositories(this IServiceCollection services)
	{
		// Register shared repositories for common aggregates
		services.AddScoped<IRecipeRepository, RecipeRepository>();

		return services;
	}

	/// <summary>
	///     Core configuration method for MongoDB data services (legacy overload)
	///     Registers all necessary services and repository implementations
	/// </summary>
	private static IServiceCollection AddEasyMealsDataCore(
		this IServiceCollection services,
		Func<MongoClientSettings, MongoClientSettings> configureClientSettings,
		string databaseName)
	{
		return services.AddEasyMealsDataCore(
			serviceProvider => configureClientSettings(new MongoClientSettings()),
			serviceProvider => databaseName);
	}

	/// <summary>
	///     Ensures the MongoDB database exists and creates indexes
	///     Essential for deployment scenarios and development setup
	/// </summary>
	/// <param name="services">Service collection</param>
	/// <returns>Service collection for chaining</returns>
	public static async Task<IServiceCollection> EnsureEasyMealsDatabaseAsync(this IServiceCollection services)
	{
		using IServiceScope scope = services.BuildServiceProvider(false).CreateScope();
		var database = scope.ServiceProvider.GetRequiredService<IMongoDatabase>();

		// The database will be created automatically when first accessed
		// Create comprehensive indexes for optimal performance
		await MongoIndexConfiguration.CreateAllIndexesAsync(database);

		return services;
	}

	/// <summary>
	///     Adds health checks for EasyMeals MongoDB connectivity
	///     Essential for production monitoring and service health validation
	/// </summary>
	/// <param name="services">Service collection</param>
	/// <param name="name">Health check name (optional)</param>
	/// <param name="tags">Health check tags (optional)</param>
	/// <returns>Service collection for chaining</returns>
	public static IServiceCollection AddEasyMealsDataHealthChecks(
		this IServiceCollection services,
		string? name = null,
		string[]? tags = null)
	{
		services.AddHealthChecks()
			.AddCheck<MongoDbHealthCheck>(
				name ?? "easymealsmongodb",
				tags: tags ?? ["database", "mongodb", "ready"]);

		return services;
	}

	#endregion
}

/// <summary>
///     MongoDB health check implementation
///     Verifies database connectivity and basic operations
/// </summary>
public class MongoDbHealthCheck : IHealthCheck
{
	private readonly IMongoDatabase _database;

	public MongoDbHealthCheck(IMongoDatabase database) => _database = database ?? throw new ArgumentNullException(nameof(database));

	public async Task<HealthCheckResult> CheckHealthAsync(
		HealthCheckContext context,
		CancellationToken cancellationToken = default)
	{
		try
		{
			// Perform a simple ping operation
			await _database.RunCommandAsync<BsonDocument>(
				new BsonDocument("ping", 1),
				cancellationToken: cancellationToken);

			return HealthCheckResult.Healthy("MongoDB connection is healthy");
		}
		catch (Exception ex)
		{
			return HealthCheckResult.Unhealthy("MongoDB connection failed", ex);
		}
	}
}