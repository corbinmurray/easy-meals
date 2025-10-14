using EasyMeals.RecipeEngine.Application.DependencyInjection;
using EasyMeals.RecipeEngine.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var services = new ServiceCollection();

services.AddLogging(opts =>
{
	opts.AddConsole();
	opts.AddDebug();
});

services.AddRecipeEngine();

await using ServiceProvider serviceProvider = services.BuildServiceProvider(true);

var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
var recipeEngine = serviceProvider.GetRequiredService<IRecipeEngine>();

logger.LogInformation("Starting Recipe Engine...");

await recipeEngine.RunAsync();