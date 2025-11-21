using EasyMeals.Persistence.Abstractions.Repositories;
using EasyMeals.Persistence.Mongo.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace EasyMeals.Persistence.Mongo.Extensions;

/// <summary>
///     Extension methods for configuring MongoDB services in dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
	/// <summary>
	///     Adds MongoDB persistence infrastructure to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration to bind MongoDB options from.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddEasyMealsMongo(
		this IServiceCollection services,
		IConfiguration configuration,
		Action<MongoRepositoryBuilder>? mongoRepositoryBuilderAction = null)
	{
		services.AddOptions<MongoDbOptions>()
			.Bind(configuration.GetSection(MongoDbOptions.SectionName))
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.AddCoreMongoServices(mongoRepositoryBuilderAction);

		return services;
	}

	/// <summary>
	///     Configures MongoDB with custom options.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configureOptions">Action to configure MongoDB options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddEasyMealsMongo(
		this IServiceCollection services,
		Action<MongoDbOptions> configureOptions,
		Action<MongoRepositoryBuilder>? mongoRepositoryBuilderAction = null)
	{
		services.Configure(configureOptions);

		services.AddCoreMongoServices(mongoRepositoryBuilderAction);

		return services;
	}

	/// <summary>
	///     Adds core MongoDB services to the service collection.
	/// </summary>
	/// <param name="services"></param>
	/// <returns></returns>
	/// <exception cref="InvalidOperationException"></exception>
	private static IServiceCollection AddCoreMongoServices(
		this IServiceCollection services,
		Action<MongoRepositoryBuilder>? mongoRepositoryBuilderAction = null)
	{
		// Register MongoDB client (singleton - connection pooling)
		services.AddSingleton<IMongoClient>(sp =>
		{
			MongoDbOptions options = sp.GetRequiredService<IOptions<MongoDbOptions>>().Value;

			if (string.IsNullOrWhiteSpace(options.ConnectionString))
				throw new InvalidOperationException(
					$"MongoDB connection string is not configured. Please add '{MongoDbOptions.SectionName}:ConnectionString' to your configuration.");

			MongoClientSettings? settings = MongoClientSettings.FromConnectionString(options.ConnectionString);

			// Configure connection pooling and timeouts
			settings.MaxConnectionPoolSize = options.MaxConnectionPoolSize;
			settings.MinConnectionPoolSize = options.MinConnectionPoolSize;
			settings.ServerSelectionTimeout = options.ServerSelectionTimeout;
			settings.ConnectTimeout = options.ConnectTimeout;

			return new MongoClient(settings);
		});

		// Register IMongoDatabase as scoped
		services.AddScoped<IMongoDatabase>(sp =>
		{
			MongoDbOptions options = sp.GetRequiredService<IOptions<MongoDbOptions>>().Value;
			var client = sp.GetRequiredService<IMongoClient>();
			return client.GetDatabase(options.DatabaseName);
		});

		// Register context and unit of work
		services.AddScoped<IMongoContext>(sp =>
		{
			var client = sp.GetRequiredService<IMongoClient>();
			MongoDbOptions options = sp.GetRequiredService<IOptions<MongoDbOptions>>().Value;
			return new MongoContext(client, options);
		});
		services.AddScoped<IUnitOfWork, MongoUnitOfWork>();

		// Build and configure repositories using fluent builder
		var builder = new MongoRepositoryBuilder(services);
		mongoRepositoryBuilderAction?.Invoke(builder);

		// Register all repositories configured via the builder
		foreach ((Type repoType, Type repoImplType) in builder.GetRepositories())
		{
			services.AddScoped(repoType, repoImplType);
		}

		// Conditionally register index creation hosted service based on configuration
		// Only if there are index creators AND the option is enabled
		IReadOnlyCollection<Func<IServiceProvider, Task>> indexCreators = builder.GetIndexCreators();
		if (indexCreators.Count != 0)
			services.AddHostedService<IndexCreationHostedService>(sp =>
			{
				MongoDbOptions options = sp.GetRequiredService<IOptions<MongoDbOptions>>().Value;
				var logger = sp.GetRequiredService<ILogger<IndexCreationHostedService>>();

				// Only create the hosted service if RunMongoIndexesOnStartup is true
				if (options.RunMongoIndexesOnStartup) 
					return new IndexCreationHostedService(sp, indexCreators, logger);
				
				// Return a no-op service that does nothing
				logger.LogInformation("Index creation on startup is disabled via configuration");
				return new IndexCreationHostedService(sp, [], logger);
			});

		return services;
	}
}