using EasyMeals.RecipeEngine.Application.DependencyInjection;
using EasyMeals.RecipeEngine.Application.Interfaces;
using EasyMeals.RecipeEngine.Application.Options;
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
		opts.AddSerilog(Log.Logger, dispose: true);
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
		
		// Start processing with default configuration
		// In production, these would come from configuration or command-line arguments
		var providerId = "provider_001";
		var batchSize = 100;
		var timeWindow = TimeSpan.FromHours(1);
		
		Log.Information("Starting recipe processing for {ProviderId} with batch size {BatchSize} and time window {TimeWindow}", 
			providerId, batchSize, timeWindow);
		
		await processor.StartProcessingAsync(providerId, batchSize, timeWindow, cts.Token);
		
		Log.Information("Recipe processing completed successfully");
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