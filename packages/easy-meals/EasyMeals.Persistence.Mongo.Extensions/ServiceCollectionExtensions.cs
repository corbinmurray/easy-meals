using EasyMeals.Persistence.Abstractions;
using EasyMeals.Persistence.Abstractions.Repositories;
using EasyMeals.Persistence.Mongo.Options;
using EasyMeals.Persistence.Mongo.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
		IConfiguration configuration)
	{
		// Configure options - bind the configuration section to MongoDbOptions
		services.Configure<MongoDbOptions>(configuration.Bind);

		// Register MongoDB client (singleton - connection pooling)
		services.AddSingleton<IMongoClient>(sp =>
		{
			MongoDbOptions options = sp.GetRequiredService<IOptions<MongoDbOptions>>().Value;

			if (string.IsNullOrWhiteSpace(options.ConnectionString))
				throw new InvalidOperationException(
					$"MongoDB connection string is not configured. Please add '{MongoDbOptions.SectionName}:ConnectionString' to your configuration.");

			MongoClientSettings? settings = MongoClientSettings.FromConnectionString(options.ConnectionString);

			// Configure connection pooling and timeouts
			settings.MaxConnectionPoolSize = 100;
			settings.MinConnectionPoolSize = 10;
			settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
			settings.ConnectTimeout = TimeSpan.FromSeconds(10);

			return new MongoClient(settings);
		});

		// Register context and unit of work
		services.AddScoped<IMongoContext>(sp =>
		{
			var client = sp.GetRequiredService<IMongoClient>();
			MongoDbOptions options = sp.GetRequiredService<IOptions<MongoDbOptions>>().Value;
			return new MongoContext(client, options);
		});
		services.AddScoped<IUnitOfWork, MongoUnitOfWork>();

		return services;
	}

	/// <summary>
	///     Registers a MongoDB repository for the specified entity type.
	/// </summary>
	/// <typeparam name="T">The entity type.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddMongoRepository<T>(this IServiceCollection services)
		where T : class, IEntity
	{
		// Register the concrete repository
		services.AddScoped<MongoRepository<T>>();

		// Register all interface variations pointing to the same instance
		services.AddScoped<IReadRepository<T, string>>(sp => sp.GetRequiredService<MongoRepository<T>>());
		services.AddScoped<IWriteRepository<T, string>>(sp => sp.GetRequiredService<MongoRepository<T>>());
		services.AddScoped<IRepository<T, string>>(sp => sp.GetRequiredService<MongoRepository<T>>());

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
		Action<MongoDbOptions> configureOptions)
	{
		services.Configure(configureOptions);

		// Register MongoDB client
		services.AddSingleton<IMongoClient>(sp =>
		{
			MongoDbOptions options = sp.GetRequiredService<IOptions<MongoDbOptions>>().Value;

			if (string.IsNullOrWhiteSpace(options.ConnectionString)) throw new InvalidOperationException("MongoDB connection string is required.");

			MongoClientSettings? settings = MongoClientSettings.FromConnectionString(options.ConnectionString);
			settings.MaxConnectionPoolSize = 100;
			settings.MinConnectionPoolSize = 10;

			return new MongoClient(settings);
		});

		services.AddScoped<IMongoContext>(sp =>
		{
			var client = sp.GetRequiredService<IMongoClient>();
			MongoDbOptions options = sp.GetRequiredService<IOptions<MongoDbOptions>>().Value;
			return new MongoContext(client, options);
		});
		services.AddScoped<IUnitOfWork, MongoUnitOfWork>();

		return services;
	}
}