using EasyMeals.Persistence.Abstractions.Repositories;
using EasyMeals.Persistence.Mongo.Documents;
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
		IConfiguration configuration,
		Action<MongoRepositoryBuilder>? mongoRepositoryBuilderAction = null)
	{
		services.AddOptions<MongoDbOptions>()
			.Bind(configuration.GetSection(MongoDbOptions.SectionName))
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.AddCoreMongoServices();

		var builder = new MongoRepositoryBuilder(services);
		mongoRepositoryBuilderAction?.Invoke(builder);
		
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

		services.AddCoreMongoServices();

		return services;
	}

	/// <summary>
	///     Adds core MongoDB services to the service collection.
	/// </summary>
	/// <param name="services"></param>
	/// <returns></returns>
	/// <exception cref="InvalidOperationException"></exception>
	private static IServiceCollection AddCoreMongoServices(this IServiceCollection services)
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
			settings.MaxConnectionPoolSize = 100;
			settings.MinConnectionPoolSize = 10;
			settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
			settings.ConnectTimeout = TimeSpan.FromSeconds(10);

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

		return services;
	}
}