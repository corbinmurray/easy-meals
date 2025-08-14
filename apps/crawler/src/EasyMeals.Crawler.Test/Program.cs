using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using EasyMeals.Crawler.Infrastructure.Services;
using EasyMeals.Crawler.Domain.Interfaces;

namespace EasyMeals.Crawler.Test;

/// <summary>
///     Simple test program to verify HelloFresh HTTP service functionality
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("HelloFresh Crawler Test Program - Enhanced Discovery");
        Console.WriteLine("===================================================");

        // Build the host with DI
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                services.AddHttpClient<IHelloFreshHttpService, HelloFreshHttpService>(client =>
                {
                    client.DefaultRequestHeaders.Add("User-Agent",
                        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
                    client.Timeout = TimeSpan.FromSeconds(30);
                })
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
                {
                    AutomaticDecompression = System.Net.DecompressionMethods.All
                });
                
                services.AddLogging(builder =>
                {
                    builder.SetMinimumLevel(LogLevel.Debug);
                    builder.AddConsole();
                });
            })
            .Build();

        var httpService = host.Services.GetRequiredService<IHelloFreshHttpService>();
        var logger = host.Services.GetRequiredService<ILogger<Program>>();

        try
        {
            // Test 1: Enhanced recipe discovery (multi-level strategy)
            logger.LogInformation("Test 1: Enhanced multi-level recipe discovery...");
            var urls = await httpService.DiscoverRecipeUrlsAsync(20); // Get more URLs to test effectiveness
            
            if (urls.Count > 0)
            {
                logger.LogInformation("Discovered {Count} URLs:", urls.Count);
                
                // Group URLs to analyze discovery effectiveness
                var uniqueRecipeNames = new HashSet<string>();
                foreach (var url in urls)
                {
                    var recipeName = ExtractRecipeNameFromUrl(url);
                    uniqueRecipeNames.Add(recipeName);
                    logger.LogInformation("  - {Url} -> {RecipeName}", url, recipeName);
                }

                logger.LogInformation("Analysis: {UniqueRecipes} unique recipes discovered", uniqueRecipeNames.Count);

                // Test 2: Fetch HTML for a few different recipes to ensure they're valid
                var testUrls = urls.Take(3).ToList();
                logger.LogInformation("Test 2: Validating {Count} recipe URLs...", testUrls.Count);

                foreach (string testUrl in testUrls)
                {
                    logger.LogInformation("Testing URL: {Url}", testUrl);
                    
                    string? html = await httpService.FetchRecipeHtmlAsync(testUrl);
                    
                    if (!string.IsNullOrEmpty(html))
                    {
                        // Analyze the HTML content
                        bool hasRecipeSchema = html.Contains("@type", StringComparison.OrdinalIgnoreCase) && 
                                             html.Contains("Recipe", StringComparison.OrdinalIgnoreCase);
                        bool hasIngredients = html.Contains("ingredient", StringComparison.OrdinalIgnoreCase);
                        bool hasInstructions = html.Contains("instruction", StringComparison.OrdinalIgnoreCase) ||
                                             html.Contains("method", StringComparison.OrdinalIgnoreCase);
                        
                        logger.LogInformation("  ✓ Valid HTML ({Length} chars) - Schema: {Schema}, Ingredients: {Ingredients}, Instructions: {Instructions}",
                            html.Length, hasRecipeSchema, hasIngredients, hasInstructions);
                    }
                    else
                    {
                        logger.LogWarning("  ✗ Failed to fetch HTML content");
                    }
                    
                    // Small delay between requests
                    await Task.Delay(1000);
                }
            }
            else
            {
                logger.LogWarning("No URLs discovered - this indicates an issue with the discovery strategy");
            }

            // Test 3: Test category page detection
            logger.LogInformation("Test 3: Testing category vs individual recipe URL detection...");
            var testUrls = new[]
            {
                "https://www.hellofresh.com/recipes", // Main page
                "https://www.hellofresh.com/recipes/american-recipes", // Category
                "https://www.hellofresh.com/recipes/quick-easy", // Category
                "https://www.hellofresh.com/recipes/some-long-recipe-name-with-id-12345", // Individual (example)
            };

            foreach (string testUrl in testUrls)
            {
                logger.LogInformation("  {Url} -> Category: {IsCategory}, Recipe: {IsRecipe}",
                    testUrl, IsValidHelloFreshCategoryUrl(testUrl), IsValidHelloFreshUrl(testUrl));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Test failed with exception: {Message}", ex.Message);
        }
        finally
        {
            httpService.Dispose();
        }

        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }

    private static string ExtractRecipeNameFromUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            string[] segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            return segments.Length > 1 ? segments[^1] : "unknown";
        }
        catch
        {
            return "invalid-url";
        }
    }

    // Copy these methods from the service for testing (in real app, you'd make them public or create a utility class)
    private static bool IsValidHelloFreshUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;

        try
        {
            var uri = new Uri(url);
            if (!uri.Host.EndsWith("hellofresh.com", StringComparison.OrdinalIgnoreCase) ||
                !uri.AbsolutePath.StartsWith("/recipes", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string[] pathSegments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            
            if (pathSegments.Length >= 2 && pathSegments[0] == "recipes")
            {
                string recipePath = pathSegments[1];
                return recipePath.Length > 10 || 
                       char.IsDigit(recipePath[^1]) || 
                       recipePath.Contains('-') && recipePath.Length > 15;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsValidHelloFreshCategoryUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;

        try
        {
            var uri = new Uri(url);
            if (!uri.Host.EndsWith("hellofresh.com", StringComparison.OrdinalIgnoreCase) ||
                !uri.AbsolutePath.StartsWith("/recipes", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return uri.AbsolutePath == "/recipes" || 
                   (uri.AbsolutePath.StartsWith("/recipes/") && !IsValidHelloFreshUrl(url));
        }
        catch
        {
            return false;
        }
    }
}
