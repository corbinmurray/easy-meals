using EasyMeals.Crawler.Domain.Entities;
using EasyMeals.Crawler.Domain.Interfaces;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;

namespace EasyMeals.Crawler.Infrastructure.Services;

/// <summary>
/// HelloFresh-specific implementation of IRecipeExtractor using HtmlAgilityPack
/// Extracts recipe data from HelloFresh recipe pages
/// </summary>
public class HelloFreshRecipeExtractor : IRecipeExtractor
{
    private readonly ILogger<HelloFreshRecipeExtractor> _logger;

    public HelloFreshRecipeExtractor(ILogger<HelloFreshRecipeExtractor> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<Recipe?> ExtractRecipeAsync(string htmlContent, string sourceUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Extracting recipe from URL: {Url}", sourceUrl);

            if (string.IsNullOrWhiteSpace(htmlContent))
            {
                _logger.LogWarning("HTML content is empty for URL: {Url}", sourceUrl);
                return Task.FromResult<Recipe?>(null);
            }

            var doc = new HtmlDocument();
            doc.LoadHtml(htmlContent);

            // Try to extract recipe data
            var recipe = ExtractRecipe(doc, sourceUrl);
            
            if (recipe != null)
            {
                _logger.LogDebug("Successfully extracted recipe: {Title}", recipe.Title);
            }
            else
            {
                _logger.LogWarning("Failed to extract recipe data from URL: {Url}", sourceUrl);
            }
            
            return Task.FromResult(recipe);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract recipe from URL: {Url}", sourceUrl);
            return Task.FromResult<Recipe?>(null);
        }
    }

    /// <summary>
    /// Extracts recipe data from parsed HTML document
    /// </summary>
    private Recipe? ExtractRecipe(HtmlDocument doc, string sourceUrl)
    {
        try
        {
            // Try NextJS data first (HelloFresh specific)
            var recipe = TryExtractFromNextJsData(doc, sourceUrl);
            if (recipe != null)
            {
                _logger.LogDebug("Successfully extracted recipe from NextJS data");
                return recipe;
            }

            // Try JSON-LD structured data
            recipe = TryExtractFromJsonLd(doc, sourceUrl);
            if (recipe != null)
            {
                _logger.LogDebug("Successfully extracted recipe from JSON-LD structured data");
                return recipe;
            }

            // Fallback to HTML parsing
            recipe = TryExtractFromHtml(doc, sourceUrl);
            if (recipe != null)
            {
                _logger.LogDebug("Successfully extracted recipe from HTML parsing");
                return recipe;
            }

            _logger.LogWarning("Could not extract recipe using any method");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during recipe extraction");
            return null;
        }
    }

    /// <summary>
    /// Attempts to extract recipe from NextJS data (HelloFresh specific)
    /// </summary>
    private Recipe? TryExtractFromNextJsData(HtmlDocument doc, string sourceUrl)
    {
        try
        {
            _logger.LogDebug("Attempting NextJS data extraction");
            
            // Look for NextJS data script with ID
            var nextDataScript = doc.DocumentNode
                .SelectSingleNode("//script[@id='__NEXT_DATA__']");

            if (nextDataScript == null) 
            {
                _logger.LogDebug("No __NEXT_DATA__ script found");
                return null;
            }

            var jsonContent = nextDataScript.InnerText;
            if (string.IsNullOrWhiteSpace(jsonContent))
            {
                _logger.LogDebug("__NEXT_DATA__ script is empty");
                return null;
            }

            _logger.LogDebug("Found __NEXT_DATA__ script with {Length} characters", jsonContent.Length);

            // Parse the JSON
            using var document = JsonDocument.Parse(jsonContent);
            var root = document.RootElement;

            // Navigate to recipe data in HelloFresh structure:
            // props.pageProps.dehydratedState.queries[].state.data
            if (!root.TryGetProperty("props", out var props))
            {
                _logger.LogDebug("No 'props' property found in NextJS data");
                return null;
            }
            
            if (!props.TryGetProperty("pageProps", out var pageProps))
            {
                _logger.LogDebug("No 'pageProps' property found in props");
                return null;
            }
            
            if (!pageProps.TryGetProperty("dehydratedState", out var dehydratedState))
            {
                _logger.LogDebug("No 'dehydratedState' property found in pageProps");
                return null;
            }
            
            if (!dehydratedState.TryGetProperty("queries", out var queries))
            {
                _logger.LogDebug("No 'queries' property found in dehydratedState");
                return null;
            }

            _logger.LogDebug("Found queries array with {Count} items", queries.GetArrayLength());

            // Look for recipe data in queries array
            foreach (var query in queries.EnumerateArray())
            {
                if (!query.TryGetProperty("state", out var state) ||
                    !state.TryGetProperty("data", out var data))
                    continue;

                // Try single recipe structure
                if (data.TryGetProperty("recipe", out var recipeData))
                {
                    _logger.LogDebug("Found individual recipe data in queries");
                    var recipe = ParseHelloFreshRecipe(recipeData, sourceUrl);
                    if (recipe != null) return recipe;
                }

                // Try recipe list structure (get first recipe)
                if (data.ValueKind == JsonValueKind.Array && data.GetArrayLength() > 0)
                {
                    _logger.LogDebug("Found recipe array with {Count} items", data.GetArrayLength());
                    
                    // Look for the first recipe with a name
                    foreach (var item in data.EnumerateArray())
                    {
                        if (item.TryGetProperty("name", out _))
                        {
                            _logger.LogDebug("Found recipe item with name");
                            var recipe = ParseHelloFreshRecipe(item, sourceUrl);
                            if (recipe != null) return recipe;
                        }
                    }
                }
            }

            _logger.LogDebug("No recipe data found in any query");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error extracting from NextJS data: {Error}", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Parses recipe from HelloFresh NextJS data structure
    /// </summary>
    private Recipe? ParseHelloFreshRecipe(JsonElement element, string sourceUrl)
    {
        try
        {
            _logger.LogDebug("Parsing HelloFresh recipe from JSON element");
            
            // Extract title
            var title = element.TryGetProperty("name", out var nameElement)
                ? nameElement.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(title))
            {
                _logger.LogDebug("No recipe name found in JSON element");
                return null;
            }

            _logger.LogDebug("Found recipe title: {Title}", title);

            var recipe = new Recipe
            {
                Id = GenerateRecipeId(sourceUrl),
                Title = title,
                SourceUrl = sourceUrl,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Extract description/headline
            if (element.TryGetProperty("headline", out var headlineElement))
                recipe.Description = headlineElement.GetString() ?? "";
            else if (element.TryGetProperty("description", out var descElement))
                recipe.Description = descElement.GetString() ?? "";

            // Extract prep time
            if (element.TryGetProperty("prepTime", out var prepElement))
            {
                var prepTimeString = prepElement.GetString();
                recipe.PrepTimeMinutes = ParseDuration(prepTimeString);
            }

            // Extract ingredients
            var ingredients = new List<string>();
            if (element.TryGetProperty("ingredients", out var ingredientsElement))
            {
                foreach (var ingredient in ingredientsElement.EnumerateArray())
                {
                    var ingredientName = ingredient.TryGetProperty("name", out var nameEl)
                        ? nameEl.GetString()
                        : null;
                    if (!string.IsNullOrWhiteSpace(ingredientName))
                        ingredients.Add(ingredientName);
                }
            }

            // Extract yields for ingredient amounts
            if (element.TryGetProperty("yields", out var yieldsElement) && yieldsElement.GetArrayLength() > 0)
            {
                var firstYield = yieldsElement[0];
                if (firstYield.TryGetProperty("ingredients", out var yieldIngredients))
                {
                    ingredients.Clear(); // Replace with yield-specific ingredients
                    foreach (var yieldIngredient in yieldIngredients.EnumerateArray())
                    {
                        var amount = yieldIngredient.TryGetProperty("amount", out var amountEl)
                            ? amountEl.GetDecimal()
                            : 0;
                        var unit = yieldIngredient.TryGetProperty("unit", out var unitEl)
                            ? unitEl.GetString()
                            : "";
                        var id = yieldIngredient.TryGetProperty("id", out var idEl)
                            ? idEl.GetString()
                            : "";

                        // Find ingredient name from main ingredients list
                        if (element.TryGetProperty("ingredients", out var mainIngredients))
                        {
                            foreach (var mainIngredient in mainIngredients.EnumerateArray())
                            {
                                if (mainIngredient.TryGetProperty("id", out var mainIdEl) &&
                                    mainIdEl.GetString() == id &&
                                    mainIngredient.TryGetProperty("name", out var nameEl))
                                {
                                    var ingredientName = nameEl.GetString();
                                    if (!string.IsNullOrWhiteSpace(ingredientName))
                                    {
                                        var formattedIngredient = amount > 0 
                                            ? $"{amount} {unit} {ingredientName}".Trim()
                                            : ingredientName;
                                        ingredients.Add(formattedIngredient);
                                    }
                                    break;
                                }
                            }
                        }
                    }
                }

                // Extract servings from first yield
                if (firstYield.TryGetProperty("yields", out var servingsElement))
                {
                    recipe.Servings = servingsElement.GetInt32();
                }
            }

            recipe.Ingredients = ingredients;
            _logger.LogDebug("Extracted {Count} ingredients", ingredients.Count);

            // Extract instructions
            var instructions = new List<string>();
            if (element.TryGetProperty("steps", out var stepsElement))
            {
                foreach (var step in stepsElement.EnumerateArray())
                {
                    var instruction = step.TryGetProperty("instructions", out var instructionElement)
                        ? instructionElement.GetString()
                        : step.TryGetProperty("instructionsMarkdown", out var markdownElement)
                            ? markdownElement.GetString()
                            : null;

                    if (!string.IsNullOrWhiteSpace(instruction))
                        instructions.Add(instruction);
                }
            }

            recipe.Instructions = instructions;
            _logger.LogDebug("Extracted {Count} instructions", instructions.Count);

            // Extract image
            if (element.TryGetProperty("imagePath", out var imagePathElement))
            {
                var imagePath = imagePathElement.GetString();
                if (!string.IsNullOrWhiteSpace(imagePath))
                {
                    // HelloFresh images are relative paths, make them absolute
                    recipe.ImageUrl = imagePath.StartsWith("http") 
                        ? imagePath 
                        : $"https://d3hvwccx09j84u.cloudfront.net/0,0{imagePath}";
                }
            }

            // Extract nutrition information
            if (element.TryGetProperty("nutrition", out var nutritionElement))
            {
                var nutrition = new Dictionary<string, string>();
                foreach (var nutritionItem in nutritionElement.EnumerateArray())
                {
                    if (nutritionItem.TryGetProperty("name", out var nutritionName) &&
                        nutritionItem.TryGetProperty("amount", out var nutritionAmount) &&
                        nutritionItem.TryGetProperty("unit", out var nutritionUnit))
                    {
                        var name = nutritionName.GetString()?.ToLowerInvariant();
                        var amount = nutritionAmount.GetDecimal();
                        var unit = nutritionUnit.GetString();

                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            nutrition[name] = $"{amount} {unit}".Trim();
                        }
                    }
                }
                recipe.NutritionInfo = nutrition;
                _logger.LogDebug("Extracted {Count} nutrition items", nutrition.Count);
            }

            _logger.LogDebug("Successfully parsed HelloFresh recipe: {Title}", recipe.Title);
            return recipe;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error parsing HelloFresh recipe: {Error}", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Attempts to extract recipe from JSON-LD structured data
    /// </summary>
    private Recipe? TryExtractFromJsonLd(HtmlDocument doc, string sourceUrl)
    {
        try
        {
            var jsonLdScripts = doc.DocumentNode
                .SelectNodes("//script[@type='application/ld+json']");

            if (jsonLdScripts == null) return null;

            foreach (var script in jsonLdScripts)
            {
                var jsonContent = script.InnerText?.Trim();
                if (string.IsNullOrEmpty(jsonContent)) continue;

                try
                {
                    using var jsonDoc = JsonDocument.Parse(jsonContent);
                    var root = jsonDoc.RootElement;

                    // Handle array of objects
                    if (root.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in root.EnumerateArray())
                        {
                            if (item.TryGetProperty("@type", out var typeProperty) &&
                                typeProperty.GetString() == "Recipe")
                            {
                                return ParseRecipeFromJsonLd(item, sourceUrl);
                            }
                        }
                    }
                    // Handle single object
                    else if (root.TryGetProperty("@type", out var typeProperty) &&
                             typeProperty.GetString() == "Recipe")
                    {
                        return ParseRecipeFromJsonLd(root, sourceUrl);
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogDebug("Invalid JSON-LD content: {Error}", ex.Message);
                    continue;
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Error extracting from JSON-LD: {Error}", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Parses recipe from JSON-LD structured data
    /// </summary>
    private Recipe ParseRecipeFromJsonLd(JsonElement recipeElement, string sourceUrl)
    {
        var recipe = new Recipe
        {
            Id = GenerateRecipeId(sourceUrl),
            SourceUrl = sourceUrl,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Extract basic properties
        if (recipeElement.TryGetProperty("name", out var nameElement))
            recipe.Title = nameElement.GetString() ?? "";

        if (recipeElement.TryGetProperty("description", out var descElement))
            recipe.Description = descElement.GetString() ?? "";

        // Extract ingredients
        if (recipeElement.TryGetProperty("recipeIngredient", out var ingredientsElement))
        {
            recipe.Ingredients = ingredientsElement.EnumerateArray()
                .Select(i => i.GetString())
                .Where(i => !string.IsNullOrWhiteSpace(i))
                .ToList()!;
        }

        // Extract instructions
        if (recipeElement.TryGetProperty("recipeInstructions", out var instructionsElement))
        {
            recipe.Instructions = new List<string>();
            foreach (var instruction in instructionsElement.EnumerateArray())
            {
                if (instruction.TryGetProperty("text", out var textElement))
                {
                    recipe.Instructions.Add(textElement.GetString() ?? "");
                }
                else if (instruction.ValueKind == JsonValueKind.String)
                {
                    recipe.Instructions.Add(instruction.GetString() ?? "");
                }
            }
        }

        // Extract timing
        if (recipeElement.TryGetProperty("prepTime", out var prepTimeElement))
            recipe.PrepTimeMinutes = ParseDuration(prepTimeElement.GetString());

        if (recipeElement.TryGetProperty("cookTime", out var cookTimeElement))
            recipe.CookTimeMinutes = ParseDuration(cookTimeElement.GetString());

        // Extract servings
        if (recipeElement.TryGetProperty("recipeYield", out var yieldElement))
            recipe.Servings = ParseServings(yieldElement);

        // Extract image
        if (recipeElement.TryGetProperty("image", out var imageElement))
            recipe.ImageUrl = ExtractImageUrl(imageElement);

        // Extract nutrition info
        if (recipeElement.TryGetProperty("nutrition", out var nutritionElement))
            recipe.NutritionInfo = ExtractNutritionInfo(nutritionElement);

        return recipe;
    }

    /// <summary>
    /// Attempts to extract recipe from HTML elements (fallback method)
    /// </summary>
    private Recipe? TryExtractFromHtml(HtmlDocument doc, string sourceUrl)
    {
        try
        {
            var recipe = new Recipe
            {
                Id = GenerateRecipeId(sourceUrl),
                SourceUrl = sourceUrl,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Extract title
            recipe.Title = ExtractTitleFromHtml(doc);
            if (string.IsNullOrWhiteSpace(recipe.Title))
            {
                _logger.LogWarning("Could not extract title from HTML");
                return null;
            }

            // Extract other properties
            recipe.Description = ExtractDescriptionFromHtml(doc);
            recipe.Ingredients = ExtractIngredientsFromHtml(doc);
            recipe.Instructions = ExtractInstructionsFromHtml(doc);
            recipe.ImageUrl = ExtractImageFromHtml(doc);
            recipe.NutritionInfo = ExtractNutritionFromHtml(doc);
            
            // Extract timing and servings from text
            ExtractTimingAndServingsFromHtml(doc, recipe);

            return recipe;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting from HTML");
            return null;
        }
    }

    /// <summary>
    /// Extracts title from HTML elements
    /// </summary>
    private string ExtractTitleFromHtml(HtmlDocument doc)
    {
        // Try h1 elements first
        var h1Elements = doc.DocumentNode.SelectNodes("//h1");
        if (h1Elements?.Any() == true)
        {
            return HttpUtility.HtmlDecode(h1Elements.First().InnerText?.Trim() ?? "");
        }

        // Try meta title
        var titleMeta = doc.DocumentNode.SelectSingleNode("//meta[@property='og:title']");
        if (titleMeta != null)
        {
            return HttpUtility.HtmlDecode(titleMeta.GetAttributeValue("content", ""));
        }

        // Try page title
        var title = doc.DocumentNode.SelectSingleNode("//title");
        if (title != null)
        {
            var titleText = HttpUtility.HtmlDecode(title.InnerText?.Trim() ?? "");
            // Remove common suffixes
            return titleText.Replace(" | HelloFresh", "").Trim();
        }

        return "";
    }

    /// <summary>
    /// Extracts description from HTML elements
    /// </summary>
    private string ExtractDescriptionFromHtml(HtmlDocument doc)
    {
        // Try meta description
        var descMeta = doc.DocumentNode.SelectSingleNode("//meta[@property='og:description']") ??
                       doc.DocumentNode.SelectSingleNode("//meta[@name='description']");
        
        if (descMeta != null)
        {
            return HttpUtility.HtmlDecode(descMeta.GetAttributeValue("content", ""));
        }

        // Try to find description paragraphs near the title
        var descElements = doc.DocumentNode.SelectNodes("//p[contains(@class, 'description')] | //div[contains(@class, 'description')]//p");
        if (descElements?.Any() == true)
        {
            return HttpUtility.HtmlDecode(descElements.First().InnerText?.Trim() ?? "");
        }

        return "";
    }

    /// <summary>
    /// Extracts ingredients from HTML elements
    /// </summary>
    private List<string> ExtractIngredientsFromHtml(HtmlDocument doc)
    {
        var ingredients = new List<string>();

        // Look for ingredient lists
        var ingredientElements = doc.DocumentNode.SelectNodes(
            "//li[contains(@class, 'ingredient')] | " +
            "//div[contains(@class, 'ingredient')]//text()[normalize-space()] | " +
            "//section[contains(@class, 'ingredient')]//li | " +
            "//*[contains(@class, 'ingredient-list')]//li"
        );

        if (ingredientElements != null)
        {
            foreach (var element in ingredientElements)
            {
                var text = HttpUtility.HtmlDecode(element.InnerText?.Trim() ?? "");
                if (!string.IsNullOrWhiteSpace(text) && text.Length > 2)
                {
                    ingredients.Add(text);
                }
            }
        }

        return ingredients;
    }

    /// <summary>
    /// Extracts instructions from HTML elements
    /// </summary>
    private List<string> ExtractInstructionsFromHtml(HtmlDocument doc)
    {
        var instructions = new List<string>();

        // Look for instruction lists
        var instructionElements = doc.DocumentNode.SelectNodes(
            "//ol[contains(@class, 'instruction')]//li | " +
            "//div[contains(@class, 'instruction')]//li | " +
            "//section[contains(@class, 'instruction')]//li | " +
            "//*[contains(@class, 'instruction-list')]//li"
        );

        if (instructionElements != null)
        {
            foreach (var element in instructionElements)
            {
                var text = HttpUtility.HtmlDecode(element.InnerText?.Trim() ?? "");
                if (!string.IsNullOrWhiteSpace(text) && text.Length > 5)
                {
                    instructions.Add(text);
                }
            }
        }

        return instructions;
    }

    /// <summary>
    /// Extracts main image from HTML elements
    /// </summary>
    private string ExtractImageFromHtml(HtmlDocument doc)
    {
        // Try Open Graph image
        var ogImage = doc.DocumentNode.SelectSingleNode("//meta[@property='og:image']");
        if (ogImage != null)
        {
            return ogImage.GetAttributeValue("content", "");
        }

        // Try to find main recipe image
        var imageElements = doc.DocumentNode.SelectNodes(
            "//img[contains(@class, 'recipe-image')] | " +
            "//img[contains(@class, 'hero-image')] | " +
            "//img[contains(@alt, 'recipe')] | " +
            "//img[contains(@src, 'recipe')]"
        );

        if (imageElements?.Any() == true)
        {
            return imageElements.First().GetAttributeValue("src", "");
        }

        return "";
    }

    /// <summary>
    /// Extracts nutrition information from HTML elements
    /// </summary>
    private Dictionary<string, string> ExtractNutritionFromHtml(HtmlDocument doc)
    {
        var nutrition = new Dictionary<string, string>();

        // Look for nutrition elements
        var nutritionElements = doc.DocumentNode.SelectNodes(
            "//*[contains(@class, 'nutrition')] | " +
            "//*[contains(@class, 'nutritional')] | " +
            "//*[contains(text(), 'Calories')] | " +
            "//*[contains(text(), 'kcal')]"
        );

        if (nutritionElements != null)
        {
            foreach (var element in nutritionElements)
            {
                var text = element.InnerText?.Trim() ?? "";
                
                // Parse common nutrition patterns
                var caloriesMatch = Regex.Match(text, @"(\d+)\s*(?:kcal|calories)", RegexOptions.IgnoreCase);
                if (caloriesMatch.Success)
                {
                    nutrition["calories"] = caloriesMatch.Groups[1].Value;
                }

                var proteinMatch = Regex.Match(text, @"(\d+(?:\.\d+)?)\s*g?\s*protein", RegexOptions.IgnoreCase);
                if (proteinMatch.Success)
                {
                    nutrition["protein"] = proteinMatch.Groups[1].Value + "g";
                }

                var fatMatch = Regex.Match(text, @"(\d+(?:\.\d+)?)\s*g?\s*fat", RegexOptions.IgnoreCase);
                if (fatMatch.Success)
                {
                    nutrition["fat"] = fatMatch.Groups[1].Value + "g";
                }

                var carbsMatch = Regex.Match(text, @"(\d+(?:\.\d+)?)\s*g?\s*carb", RegexOptions.IgnoreCase);
                if (carbsMatch.Success)
                {
                    nutrition["carbs"] = carbsMatch.Groups[1].Value + "g";
                }
            }
        }

        return nutrition;
    }

    /// <summary>
    /// Extracts timing and servings information from HTML
    /// </summary>
    private void ExtractTimingAndServingsFromHtml(HtmlDocument doc, Recipe recipe)
    {
        var allText = doc.DocumentNode.InnerText;

        // Look for prep time
        var prepMatch = Regex.Match(allText, @"prep\s*time?\s*(\d+)\s*(?:min|minute)", RegexOptions.IgnoreCase);
        if (prepMatch.Success && int.TryParse(prepMatch.Groups[1].Value, out var prepTime))
        {
            recipe.PrepTimeMinutes = prepTime;
        }

        // Look for cook time
        var cookMatch = Regex.Match(allText, @"cook\s*time?\s*(\d+)\s*(?:min|minute)", RegexOptions.IgnoreCase);
        if (cookMatch.Success && int.TryParse(cookMatch.Groups[1].Value, out var cookTime))
        {
            recipe.CookTimeMinutes = cookTime;
        }

        // Look for total time
        var totalMatch = Regex.Match(allText, @"total\s*time?\s*(\d+)\s*(?:min|minute)", RegexOptions.IgnoreCase);
        if (totalMatch.Success && int.TryParse(totalMatch.Groups[1].Value, out var totalTime))
        {
            if (recipe.PrepTimeMinutes == 0 && recipe.CookTimeMinutes == 0)
            {
                recipe.CookTimeMinutes = totalTime;
            }
        }

        // Look for servings
        var servingsMatch = Regex.Match(allText, @"(?:serves?|servings?)\s*(\d+)", RegexOptions.IgnoreCase);
        if (servingsMatch.Success && int.TryParse(servingsMatch.Groups[1].Value, out var servings))
        {
            recipe.Servings = servings;
        }
    }

    /// <summary>
    /// Generates a unique recipe ID from the source URL
    /// </summary>
    private string GenerateRecipeId(string sourceUrl)
    {
        // Extract recipe ID from HelloFresh URL pattern
        var match = Regex.Match(sourceUrl, @"/recipes/([^/]+)/?$");
        if (match.Success)
        {
            return $"hellofresh-{match.Groups[1].Value}";
        }

        // Fallback: use URL hash
        var urlHash = Math.Abs(sourceUrl.GetHashCode()).ToString("X");
        return $"hellofresh-{urlHash}";
    }

    /// <summary>
    /// Parses ISO 8601 duration to minutes
    /// </summary>
    private int ParseDuration(string? duration)
    {
        if (string.IsNullOrEmpty(duration)) return 0;

        // Handle ISO 8601 duration format (PT15M)
        var match = Regex.Match(duration, @"PT(?:(\d+)H)?(?:(\d+)M)?");
        if (match.Success)
        {
            var hours = int.TryParse(match.Groups[1].Value, out var h) ? h : 0;
            var minutes = int.TryParse(match.Groups[2].Value, out var m) ? m : 0;
            return (hours * 60) + minutes;
        }

        // Handle simple minute format
        var minuteMatch = Regex.Match(duration, @"(\d+)");
        if (minuteMatch.Success && int.TryParse(minuteMatch.Groups[1].Value, out var mins))
        {
            return mins;
        }

        return 0;
    }

    /// <summary>
    /// Parses servings from JSON element
    /// </summary>
    private int ParseServings(JsonElement yieldElement)
    {
        if (yieldElement.ValueKind == JsonValueKind.Number)
        {
            return yieldElement.GetInt32();
        }

        if (yieldElement.ValueKind == JsonValueKind.String)
        {
            var yieldText = yieldElement.GetString() ?? "";
            var match = Regex.Match(yieldText, @"(\d+)");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var servings))
            {
                return servings;
            }
        }

        return 4; // Default servings
    }

    /// <summary>
    /// Extracts image URL from JSON element
    /// </summary>
    private string ExtractImageUrl(JsonElement imageElement)
    {
        if (imageElement.ValueKind == JsonValueKind.String)
        {
            return imageElement.GetString() ?? "";
        }

        if (imageElement.ValueKind == JsonValueKind.Array && imageElement.GetArrayLength() > 0)
        {
            var firstImage = imageElement[0];
            if (firstImage.ValueKind == JsonValueKind.String)
            {
                return firstImage.GetString() ?? "";
            }
            if (firstImage.TryGetProperty("url", out var urlProperty))
            {
                return urlProperty.GetString() ?? "";
            }
        }

        if (imageElement.TryGetProperty("url", out var urlProp))
        {
            return urlProp.GetString() ?? "";
        }

        return "";
    }

    /// <summary>
    /// Extracts nutrition information from JSON element
    /// </summary>
    private Dictionary<string, string> ExtractNutritionInfo(JsonElement nutritionElement)
    {
        var nutrition = new Dictionary<string, string>();

        if (nutritionElement.TryGetProperty("calories", out var caloriesElement))
            nutrition["calories"] = caloriesElement.GetString() ?? "";

        if (nutritionElement.TryGetProperty("proteinContent", out var proteinElement))
            nutrition["protein"] = proteinElement.GetString() ?? "";

        if (nutritionElement.TryGetProperty("fatContent", out var fatElement))
            nutrition["fat"] = fatElement.GetString() ?? "";

        if (nutritionElement.TryGetProperty("carbohydrateContent", out var carbElement))
            nutrition["carbs"] = carbElement.GetString() ?? "";

        if (nutritionElement.TryGetProperty("fiberContent", out var fiberElement))
            nutrition["fiber"] = fiberElement.GetString() ?? "";

        if (nutritionElement.TryGetProperty("sodiumContent", out var sodiumElement))
            nutrition["sodium"] = sodiumElement.GetString() ?? "";

        return nutrition;
    }
}
