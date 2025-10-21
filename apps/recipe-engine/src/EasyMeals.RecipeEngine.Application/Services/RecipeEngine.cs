using EasyMeals.RecipeEngine.Application.Options;
using EasyMeals.RecipeEngine.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EasyMeals.RecipeEngine.Application.Services;

/// <summary>
///     Main recipe engine that orchestrates recipe processing using Saga pattern
///     Demonstrates Clean Architecture and Domain-Driven Design principles
/// </summary>
public class RecipeEngine(
	ILogger<RecipeEngine> logger,
	IOptionsMonitor<SiteOptions> siteOptions) : IRecipeEngine
{
	public async Task RunAsync()
	{
		try
		{

		}
		catch (Exception e)
		{
			Console.WriteLine(e);
			throw;
		}
	}
}