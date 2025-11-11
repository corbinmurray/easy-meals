using EasyMeals.RecipeEngine.Application.DependencyInjection;
using EasyMeals.RecipeEngine.Application.Interfaces;
using EasyMeals.RecipeEngine.Application.Options;
using EasyMeals.RecipeEngine.Domain.ValueObjects;
using EasyMeals.RecipeEngine.Infrastructure.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

// T126: Configure Serilog with comprehensive structured logging
Log.Logger = new LoggerConfiguration()
	.MinimumLevel.Debug()
	.MinimumLevel.Override("Microsoft", LogEventLevel.Information)
	.MinimumLevel.Override("System", LogEventLevel.Information)
	.Enrich.FromLogContext()
	.Enrich.WithProperty("Application", "RecipeEngine")
	.Enrich.WithProperty("Environment", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production")
	.Enrich.WithProperty("MachineName", Environment.MachineName)
	.WriteTo.Console(
		outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
	.CreateLogger();

try
{
	Log.Information("Starting Recipe Engine application");

	// Configuration setup
	IConfigurationRoot configuration = new ConfigurationBuilder()
		.SetBasePath(Directory.GetCurrentDirectory())
		.AddJsonFile("appsettings.json", false, true)
		.AddJsonFile("appsettings.Development.json", true)
		.AddEnvironmentVariables()
		.Build();

	// Services setup
	var services = new ServiceCollection();

	services.AddSingleton<IConfiguration>(configuration);

	// T126: Add Serilog logging with enrichment
	services.AddLogging(opts =>
	{
		opts.ClearProviders();
		opts.AddSerilog(Log.Logger, true);
	});

	// Recipe Engine infrastructure includes MongoDB setup
	services.AddRecipeEngineInfrastructure(configuration);
	services.AddRecipeEngine();

	// Options setup
	services.Configure<Dictionary<string, SiteOptions>>(configuration);

	await using ServiceProvider serviceProvider = services.BuildServiceProvider(true);

	// Setup cancellation
	var cts = new CancellationTokenSource();
	ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
	{
		Log.Information("Cancellation requested. Shutting down gracefully...");
		cts.Cancel();
		eventArgs.Cancel = true;
	};

	Console.CancelKeyPress += cancelHandler;

	// Run the app
	try
	{
		await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();
		var processor = scope.ServiceProvider.GetRequiredService<IRecipeProcessingSaga>();
		var configLoader = scope.ServiceProvider.GetRequiredService<IProviderConfigurationLoader>();

		// Load all enabled provider configurations from MongoDB
		Log.Information("Loading enabled provider configurations from MongoDB...");
		IEnumerable<ProviderConfiguration> providers = await configLoader.GetAllEnabledAsync(cts.Token);
		List<ProviderConfiguration> providerList = providers.ToList();

		if (providerList.Count == 0)
		{
			Log.Warning("No enabled providers found in MongoDB. Please seed provider configurations.");
			return;
		}

		Log.Information("Found {ProviderCount} enabled provider(s)", providerList.Count);

		// Process each enabled provider
		foreach (ProviderConfiguration providerConfig in providerList)
		{
			try
			{
				Log.Information(
					"Starting recipe processing for provider {ProviderId} with batch size {BatchSize} and time window {TimeWindow}",
					providerConfig.ProviderId,
					providerConfig.BatchSize,
					providerConfig.TimeWindow);

				Guid batchId = await processor.StartProcessingAsync(
					providerConfig.ProviderId,
					providerConfig.BatchSize,
					providerConfig.TimeWindow,
					cts.Token);

				Log.Information(
					"Recipe processing completed successfully for provider {ProviderId} with batch ID {BatchId}",
					providerConfig.ProviderId,
					batchId);
			}
			catch (Exception ex)
			{
				Log.Error(
					ex,
					"Failed to process provider {ProviderId}: {ErrorMessage}",
					providerConfig.ProviderId,
					ex.Message);
				// Continue with next provider instead of failing completely
			}
		}

		Log.Information("All enabled providers processed");
	}
	catch (OperationCanceledException)
	{
		Log.Information("Application shutdown requested");
	}
	catch (Exception e)
	{
		Log.Fatal(e, "Application failed with unexpected error");
		Environment.Exit(-1);
	}
	finally
	{
		Console.CancelKeyPress -= cancelHandler;
		cts?.Dispose();
	}
}
catch (Exception ex)
{
	Log.Fatal(ex, "Application terminated unexpectedly during startup");
	Environment.Exit(-1);
}
finally
{
	Log.Information("Recipe Engine application shutdown complete");
	await Log.CloseAndFlushAsync();
}