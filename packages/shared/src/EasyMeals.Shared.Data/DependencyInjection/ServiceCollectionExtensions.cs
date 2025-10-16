using EasyMeals.Shared.Data.Configuration;
using EasyMeals.Shared.Data.Documents.Recipe;
using EasyMeals.Shared.Data.Repositories;
using EasyMeals.Shared.Data.Repositories.Recipe;
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
	#region Fluent API

	/// <summary>
	///     Starts building EasyMeals repositories with fluent configuration
	///     Requires MongoDB to be configured first via AddEasyMealsDataMongoDB.
	///     Defaults to adding any shared repositories
	/// </summary>
	/// <param name="services">The service collection</param>
	/// <returns>Repository builder for chaining</returns>
	/// <exception cref="InvalidOperationException">Thrown when MongoDB is not configured</exception>
	public static EasyMealsRepositoryBuilder ConfigureEasyMealsRepositories(
		this IServiceCollection services)
	{
		ValidateMongoDbConfiguration(services);
		return new EasyMealsRepositoryBuilder(services).AddRepository<IRecipeRepository, RecipeRepository, RecipeDocument>();
	}

	/// <summary>
	///     Validates that MongoDB has been configured
	/// </summary>
	private static void ValidateMongoDbConfiguration(IServiceCollection services)
	{
		ServiceProvider serviceProvider = services.BuildServiceProvider(false);
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

		services.AddEasyMealsDataCore(serviceProvider =>
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

		// Build a temporary service provider to read the configured options
		using (ServiceProvider tempProvider = services.BuildServiceProvider())
		{
			MongoDbOptions mongoOptions = tempProvider.GetRequiredService<IOptions<MongoDbOptions>>().Value;
			if (mongoOptions.EnableHealthChecks)
				services.AddEasyMealsDataHealthChecks(
					null, // Use default name
					mongoOptions.HealthCheckTags);
		}

		return services;
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
				name ?? "easymeals_health_check",
				tags: tags ?? ["database", "mongodb", "ready"]);

		return services;
	}

	#endregion
}

/// <summary>
///     MongoDB health check implementation
///     Verifies database connectivity and basic operations
/// </summary>
public class MongoDbHealthCheck(IMongoDatabase database) : IHealthCheck
{
	private readonly IMongoDatabase _database = database ?? throw new ArgumentNullException(nameof(database));

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