using EasyMeals.RecipeEngine.Application.Interfaces;
using EasyMeals.RecipeEngine.Domain.Interfaces;
using EasyMeals.RecipeEngine.Domain.Repositories;
using EasyMeals.RecipeEngine.Domain.Services;
using EasyMeals.RecipeEngine.Infrastructure.Discovery;
using EasyMeals.RecipeEngine.Infrastructure.Documents;
using EasyMeals.RecipeEngine.Infrastructure.Documents.Fingerprint;
using EasyMeals.RecipeEngine.Infrastructure.Documents.Recipe;
using EasyMeals.RecipeEngine.Infrastructure.Documents.SagaState;
using EasyMeals.RecipeEngine.Infrastructure.Normalization;
using EasyMeals.RecipeEngine.Infrastructure.Repositories;
using EasyMeals.RecipeEngine.Infrastructure.Services;
using EasyMeals.RecipeEngine.Infrastructure.Stealth;
using EasyMeals.Shared.Data.DependencyInjection;
using EasyMeals.Shared.Data.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;
using System.Net;
using IRecipeRepository = EasyMeals.RecipeEngine.Domain.Interfaces.IRecipeRepository;

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

		// Register domain services
		services.AddScoped<IRecipeDuplicationChecker, RecipeDuplicationChecker>();
		services.AddScoped<IBatchCompletionPolicy, BatchCompletionPolicy>();

		// Register application services
		services.AddScoped<IProviderConfigurationLoader, ProviderConfigurationLoader>();
		services.AddScoped<IIngredientNormalizer, IngredientNormalizationService>();

		// T116: Register discovery services (Phase 8)
		services.AddScoped<StaticCrawlDiscoveryService>();
		services.AddScoped<DynamicCrawlDiscoveryService>();
		services.AddScoped<ApiDiscoveryService>();
		services.AddScoped<IDiscoveryServiceFactory, DiscoveryServiceFactory>();
		
		// Register Playwright for dynamic discovery
		services.AddSingleton(_ => Microsoft.Playwright.Playwright.CreateAsync().GetAwaiter().GetResult());

		// T098, T099: Register stealth services for IP ban avoidance
		services.Configure<UserAgentOptions>(options =>
		{
			var userAgents = configuration.GetSection("UserAgents").Get<List<string>>() ?? new List<string>();
			options.UserAgents = userAgents;
		});
		services.AddSingleton<IRandomizedDelayService, RandomizedDelayService>();
		services.AddSingleton<IUserAgentRotationService, UserAgentRotationService>();
		
		// T103: Register stealthy HTTP client
		services.AddScoped<IStealthyHttpClient, StealthyHttpClient>();

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

		return services;
	}

	/// <summary>
	/// T102: Retry policy with exponential backoff for transient HTTP errors
	/// </summary>
	private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
	{
		return HttpPolicyExtensions
			.HandleTransientHttpError()
			.OrResult(msg => msg.StatusCode == HttpStatusCode.TooManyRequests)
			.WaitAndRetryAsync(
				retryCount: 3,
				sleepDurationProvider: retryAttempt =>
					TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)) + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000)),
				onRetry: (outcome, timespan, retryAttempt, context) =>
				{
					// Log retry attempts (structured logging would be done by the saga)
				});
	}

	/// <summary>
	/// T102: Circuit breaker policy to prevent overwhelming failing services
	/// </summary>
	private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
	{
		return HttpPolicyExtensions
			.HandleTransientHttpError()
			.CircuitBreakerAsync(
				handledEventsAllowedBeforeBreaking: 5,
				durationOfBreak: TimeSpan.FromSeconds(30));
	}

	/// <summary>
	/// T102: Timeout policy per request
	/// </summary>
	private static IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicy()
	{
		return Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(30));
	}
}