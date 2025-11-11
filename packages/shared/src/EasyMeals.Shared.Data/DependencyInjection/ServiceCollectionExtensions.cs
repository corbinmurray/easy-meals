using EasyMeals.Shared.Data.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace EasyMeals.Shared.Data.DependencyInjection;

/// <summary>
///     Extension methods for configuring EasyMeals data services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Registers MongoDbOptions from configuration and sets up MongoDB client and database DI.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The configuration root (should include env vars, appsettings, etc.)</param>
    /// <param name="sectionName">The config section name (default: "MongoDB")</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddEasyMealsMongoDb(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "MongoDB")
    {
        // Bind and validate options
        services.AddOptions<MongoDbOptions>()
            .Bind(configuration.GetSection(sectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Register MongoClient as singleton
        services.AddSingleton<IMongoClient>(sp =>
        {
            MongoDbOptions options = sp.GetRequiredService<IOptions<MongoDbOptions>>().Value;
            MongoClientSettings? settings = MongoClientSettings.FromConnectionString(options.ConnectionString);
            if (!string.IsNullOrWhiteSpace(options.ApplicationName))
                settings.ApplicationName = options.ApplicationName;
            return new MongoClient(settings);
        });

        // Register IMongoDatabase as scoped
        services.AddScoped<IMongoDatabase>(sp =>
        {
            MongoDbOptions options = sp.GetRequiredService<IOptions<MongoDbOptions>>().Value;
            var client = sp.GetRequiredService<IMongoClient>();
            return client.GetDatabase(options.DatabaseName);
        });

        return services;
    }

    /// <summary>
    ///     Configure the database that easy meals apps will use (repository builder pattern)
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configure"></param>
    /// <returns></returns>
    public static EasyMealsRepositoryBuilder ConfigureEasyMealsDatabase(this IServiceCollection services,
        Action<EasyMealsRepositoryBuilder>? configure = null)
    {
        var builder = new EasyMealsRepositoryBuilder(services);
        configure?.Invoke(builder);
        return builder;
    }
}