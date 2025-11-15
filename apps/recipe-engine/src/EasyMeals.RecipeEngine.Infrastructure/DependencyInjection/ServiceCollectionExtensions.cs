using System.Net;
using EasyMeals.RecipeEngine.Application.Interfaces;
using EasyMeals.RecipeEngine.Domain.Interfaces;
using EasyMeals.RecipeEngine.Domain.Repositories;
using EasyMeals.RecipeEngine.Infrastructure.Discovery;
using EasyMeals.RecipeEngine.Infrastructure.Documents;
using EasyMeals.RecipeEngine.Infrastructure.Documents.Fingerprint;
using EasyMeals.RecipeEngine.Infrastructure.Documents.Recipe;
using EasyMeals.RecipeEngine.Infrastructure.Documents.SagaState;
using EasyMeals.RecipeEngine.Infrastructure.Extraction;
using EasyMeals.RecipeEngine.Infrastructure.Fingerprinting;
using EasyMeals.RecipeEngine.Infrastructure.HealthChecks;
using EasyMeals.RecipeEngine.Infrastructure.Normalization;
using EasyMeals.RecipeEngine.Infrastructure.RateLimiting;
using EasyMeals.RecipeEngine.Infrastructure.Repositories;
using EasyMeals.RecipeEngine.Infrastructure.Services;
using EasyMeals.RecipeEngine.Infrastructure.Stealth;
using EasyMeals.Shared.Data.DependencyInjection;
using EasyMeals.Shared.Data.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Polly;
using Polly.Extensions.Http;

namespace EasyMeals.RecipeEngine.Infrastructure.DependencyInjection;

/// <summary>
///     Dependency injection extensions for the RecipeEngine Infrastructure layer
///     Follows DDD principles and Clean Architecture patterns
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Adds recipe-engine infrastructure services including MongoDB data layer
    ///     Uses shared MongoDB options and database with recipe-engine-specific collections
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The application configuration</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddRecipeEngineInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Add MongoDB data services using shared options pattern with shared database
        services
            .AddEasyMealsMongoDb(configuration)
            .ConfigureEasyMealsDatabase(builder =>
            {
                builder.IncludeSharedRepositories = true;

                builder
                    .AddRepository<ISagaStateRepository, SagaStateRepository, SagaStateDocument>()
                    .WithSoftDeletableIndexes<SagaStateDocument>();

                builder
                    .AddRepository<IFingerprintRepository, FingerprintRepository, FingerprintDocument>()
                    .WithDefaultIndexes();
            })
            .EnsureDatabaseAsync().GetAwaiter().GetResult();

        // Register MongoDB document repositories for Recipe Engine
        services.AddScoped<IMongoRepository<ProviderConfigurationDocument>, MongoRepository<ProviderConfigurationDocument>>();
        services.AddScoped<IMongoRepository<RecipeBatchDocument>, MongoRepository<RecipeBatchDocument>>();
        services.AddScoped<IMongoRepository<IngredientMappingDocument>, MongoRepository<IngredientMappingDocument>>();
        services.AddScoped<IMongoRepository<RecipeFingerprintDocument>, MongoRepository<RecipeFingerprintDocument>>();
        services.AddScoped<IMongoRepository<RecipeDocument>, MongoRepository<RecipeDocument>>();

        // Register domain repositories
        services.AddScoped<IRecipeBatchRepository, RecipeBatchRepository>();
        services.AddScoped<IIngredientMappingRepository, IngredientMappingRepository>();
        services.AddScoped<IRecipeFingerprintRepository, RecipeFingerprintRepository>();
        services.AddScoped<IRecipeRepository, RecipeRepository>();
        
        // Register application services
        services.AddScoped<IProviderConfigurationLoader, ProviderConfigurationLoader>();
        services.AddScoped<IIngredientNormalizer, IngredientNormalizationService>();

        // T125: Register fingerprinting service (Phase 9)
        services.AddScoped<IRecipeFingerprinter, RecipeFingerprintService>();

        // Register recipe extraction service
        services.AddScoped<IRecipeExtractor, RecipeExtractorService>();

        // T116: Register discovery services (Phase 8)
        services.AddScoped<StaticCrawlDiscoveryService>();
        services.AddScoped<DynamicCrawlDiscoveryService>();
        services.AddScoped<ApiDiscoveryService>();
        services.AddScoped<IDiscoveryServiceFactory, DiscoveryServiceFactory>();

        // Register Playwright for dynamic discovery
        services.AddSingleton(_ => Playwright.CreateAsync().GetAwaiter().GetResult());

        // T098, T099: Register stealth services for IP ban avoidance
        services.Configure<UserAgentOptions>(options =>
        {
            List<string> userAgents = configuration.GetSection("UserAgents").Get<List<string>>() ?? new List<string>();
            options.UserAgents = userAgents;
        });
        services.AddSingleton<IRandomizedDelayService, RandomizedDelayService>();
        services.AddSingleton<IUserAgentRotationService, UserAgentRotationService>();

        // T103: Register stealthy HTTP client
        services.AddScoped<IStealthyHttpClient, StealthyHttpClient>();

        // T100: Register rate limiter with default settings
        // These can be overridden by provider-specific configurations
        services.AddSingleton<IRateLimiter>(sp => new TokenBucketRateLimiter(
            maxTokens: 20,           // Max 20 requests can burst
            refillRatePerMinute: 20  // Refill at 20 requests per minute
        ));

        // T101, T102: Configure HttpClient with connection pooling and Polly policies
        services.AddHttpClient("RecipeEngineHttpClient", client =>
            {
                client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
                client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
                client.Timeout = TimeSpan.FromSeconds(30); // Default timeout
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                // T101: Connection pooling settings
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                MaxConnectionsPerServer = 10,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            })
            .AddPolicyHandler(GetRetryPolicy()) // T102: Retry policy with exponential backoff
            .AddPolicyHandler(GetCircuitBreakerPolicy()) // T102: Circuit breaker policy
            .AddPolicyHandler(GetTimeoutPolicy()); // T102: Timeout policy per request

        // Register hosted services
        services.AddHostedService<ProviderConfigurationHostedService>();

        // T128: Register health checks
        services.AddHealthChecks()
            .AddCheck<MongoDbHealthCheck>("mongodb", tags: new[] { "database", "ready" })
            .AddCheck<RateLimiterHealthCheck>("rate-limiter", tags: new[] { "application", "ready" })
            .AddCheck<DiscoveryServiceHealthCheck>("discovery-services", tags: new[] { "application", "ready" });

        return services;
    }

    /// <summary>
    ///     T102: Retry policy with exponential backoff for transient HTTP errors
    /// </summary>
    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => msg.StatusCode == HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(
                3,
                retryAttempt =>
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)) + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000)),
                (outcome, timespan, retryAttempt, context) =>
                {
                    // Log retry attempts (structured logging would be done by the saga)
                });
    }

    /// <summary>
    ///     T102: Circuit breaker policy to prevent overwhelming failing services
    /// </summary>
    private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy() =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                5,
                TimeSpan.FromSeconds(30));

    /// <summary>
    ///     T102: Timeout policy per request
    /// </summary>
    private static IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicy() => Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(30));
}