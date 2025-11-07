using EasyMeals.RecipeEngine.Application.DependencyInjection;
using EasyMeals.RecipeEngine.Application.Interfaces;
using EasyMeals.RecipeEngine.Application.Options;
using EasyMeals.RecipeEngine.Infrastructure.DependencyInjection;
using EasyMeals.Shared.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Configuration setup
IConfigurationRoot configuration = new ConfigurationBuilder()
	.SetBasePath(Directory.GetCurrentDirectory())
	.AddJsonFile("appsettings.json", false, true)
	.AddJsonFile("appsettings.Development.json", true)
	.AddEnvironmentVariables()
	.AddUserSecrets<Program>()
	.Build();

// Services setup
var services = new ServiceCollection();

services.AddSingleton<IConfiguration>(configuration);

services.AddLogging(opts =>
{
	opts.AddJsonConsole(consoleOpts =>
	{
		consoleOpts.IncludeScopes = true;
		consoleOpts.UseUtcTimestamp = true;
		consoleOpts.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
	});

	opts.AddDebug();
});

// MongoDB configuration
var connectionString = configuration["MongoDB:ConnectionString"]
	?? throw new InvalidOperationException("MongoDB:ConnectionString not configured");
var databaseName = configuration["MongoDB:DatabaseName"] ?? "EasyMeals";

services.AddMongoDb(connectionString, databaseName);

services.AddRecipeEngineInfrastructure(configuration);
services.AddRecipeEngine();

// Options setup
services.Configure<Dictionary<string, SiteOptions>>(configuration);

await using ServiceProvider serviceProvider = services.BuildServiceProvider(true);

// Setup cancellation
var cts = new CancellationTokenSource();
ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
{
	Console.WriteLine("Cancellation requested. Shutting down...");
	cts.Cancel();
	eventArgs.Cancel = true;
};

Console.CancelKeyPress += cancelHandler;

// Run the app
try
{
	await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();
	var processor = scope.ServiceProvider.GetRequiredService<IRecipeProcessingSaga>();
	await processor.StartProcessingAsync(cts.Token);
}
catch (OperationCanceledException)
{
	Console.WriteLine("Application shutdown requested.");
}
catch (Exception e)
{
	Console.WriteLine($"Application failed: {e.Message}");
	Environment.Exit(-1);
}
finally
{
	Console.CancelKeyPress -= cancelHandler;
	cts?.Dispose();
}