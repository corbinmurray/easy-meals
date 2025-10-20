using EasyMeals.RecipeEngine.Application.DependencyInjection;
using EasyMeals.RecipeEngine.Domain.Interfaces;
using EasyMeals.RecipeEngine.Infrastructure.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

IConfigurationRoot configuration = new ConfigurationBuilder()
	.SetBasePath(Directory.GetCurrentDirectory())
	.AddJsonFile("appsettings.json", false, true)
	.AddJsonFile("appsettings.Development.json", true)
	.AddEnvironmentVariables()
	.Build();

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

services.AddRecipeEngineInfrastructure(configuration);
services.AddRecipeEngine();


await using ServiceProvider serviceProvider = services.BuildServiceProvider(true);

var recipeEngine = serviceProvider.GetRequiredService<IRecipeEngine>();
await recipeEngine.RunAsync();