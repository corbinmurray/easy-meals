using System.Text.Json;
using System.Text.RegularExpressions;
using EasyMeals.RecipeEngine.Domain.Entities;
using EasyMeals.RecipeEngine.Domain.Interfaces;
using EasyMeals.RecipeEngine.Domain.ValueObjects.Recipe;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace EasyMeals.RecipeEngine.Infrastructure.Extraction;

/// <summary>
///     Domain service implementation for extracting structured recipe data from HTML content.
///     Implements a multi-strategy approach: JSON-LD structured data first, then HTML parsing fallback.
///     Follows DDD principles with proper encapsulation and business logic.
/// </summary>
public class RecipeExtractorService : IRecipeExtractor
{
    private readonly ILogger<RecipeExtractorService> _logger;

    public RecipeExtractorService(ILogger<RecipeExtractorService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    ///     Extracts a structured recipe from a fingerprint containing HTML content.
    ///     Uses JSON-LD structured data (schema.org Recipe) as primary strategy,
    ///     with HTML parsing as fallback.
    /// </summary>
    public async Task<Recipe?> ExtractRecipeAsync(Fingerprint fingerprint, CancellationToken cancellationToken = default)
    {
        if (fingerprint == null)
            throw new ArgumentNullException(nameof(fingerprint));

        if (string.IsNullOrEmpty(fingerprint.RawContent))
        {
            _logger.LogWarning(
                "Fingerprint {FingerprintId} has no raw content to extract from",
                fingerprint.Id);
            return null;
        }

        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(fingerprint.RawContent);

            // Strategy 1: Try to extract from JSON-LD structured data
            Recipe? recipe = await ExtractFromJsonLdAsync(doc, fingerprint, cancellationToken);

            if (recipe != null)
            {
                _logger.LogDebug(
                    "Successfully extracted recipe from JSON-LD structured data for URL {Url}",
                    fingerprint.Url);
                return recipe;
            }

            // Strategy 2: Try to extract from HTML meta tags and semantic markup
            recipe = await ExtractFromHtmlAsync(doc, fingerprint, cancellationToken);

            if (recipe != null)
            {
                _logger.LogDebug(
                    "Successfully extracted recipe from HTML markup for URL {Url}",
                    fingerprint.Url);
                return recipe;
            }

            _logger.LogWarning(
                "Failed to extract recipe from fingerprint {FingerprintId}, URL {Url}. No structured data found.",
                fingerprint.Id,
                fingerprint.Url);

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error extracting recipe from fingerprint {FingerprintId}, URL {Url}: {ErrorMessage}",
                fingerprint.Id,
                fingerprint.Url,
                ex.Message);
            return null;
        }
    }

    /// <summary>
    ///     Determines if a fingerprint contains extractable recipe content by checking for
    ///     common recipe indicators (JSON-LD, meta tags, semantic markup).
    /// </summary>
    public bool CanExtractRecipe(Fingerprint fingerprint)
    {
        if (fingerprint == null || string.IsNullOrEmpty(fingerprint.RawContent))
            return false;

        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(fingerprint.RawContent);

            // Check for JSON-LD Recipe schema
            var jsonLdNodes = doc.DocumentNode.SelectNodes("//script[@type='application/ld+json']");
            if (jsonLdNodes != null)
            {
                foreach (var node in jsonLdNodes)
                {
                    if (node.InnerText.Contains("\"@type\"") &&
                        (node.InnerText.Contains("\"Recipe\"") || node.InnerText.Contains("'Recipe'")))
                    {
                        return true;
                    }
                }
            }

            // Check for recipe-specific meta tags
            var metaTags = doc.DocumentNode.SelectNodes("//meta[@property or @name]");
            if (metaTags != null)
            {
                foreach (var meta in metaTags)
                {
                    string? property = meta.GetAttributeValue("property", string.Empty) ?? meta.GetAttributeValue("name", string.Empty);
                    if (property != null &&
                        (property.Contains("recipe", StringComparison.OrdinalIgnoreCase) ||
                         property.Contains("ingredient", StringComparison.OrdinalIgnoreCase)))
                    {
                        return true;
                    }
                }
            }

            // Check for common recipe HTML structures
            bool hasIngredientsList = doc.DocumentNode.SelectSingleNode("//ul[contains(@class, 'ingredient')] | //ol[contains(@class, 'ingredient')]") != null;
            bool hasInstructionsList = doc.DocumentNode.SelectSingleNode("//ol[contains(@class, 'instruction')] | //ul[contains(@class, 'instruction')]") != null;

            return hasIngredientsList || hasInstructionsList;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Error checking if fingerprint {FingerprintId} can extract recipe: {ErrorMessage}",
                fingerprint.Id,
                ex.Message);
            return false;
        }
    }

    /// <summary>
    ///     Gets extraction confidence score based on the presence and quality of recipe indicators.
    /// </summary>
    public decimal GetExtractionConfidence(Fingerprint fingerprint)
    {
        if (fingerprint == null || string.IsNullOrEmpty(fingerprint.RawContent))
            return 0.0m;

        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(fingerprint.RawContent);

            decimal confidence = 0.0m;

            // JSON-LD structured data: highest confidence
            var jsonLdNodes = doc.DocumentNode.SelectNodes("//script[@type='application/ld+json']");
            if (jsonLdNodes != null)
            {
                foreach (var node in jsonLdNodes)
                {
                    if (node.InnerText.Contains("\"@type\"") &&
                        (node.InnerText.Contains("\"Recipe\"") || node.InnerText.Contains("'Recipe'")))
                    {
                        confidence += 0.6m;
                        break;
                    }
                }
            }

            // Recipe meta tags: medium confidence
            var metaTags = doc.DocumentNode.SelectNodes("//meta[@property or @name]");
            int recipeMetaCount = 0;
            if (metaTags != null)
            {
                foreach (var meta in metaTags)
                {
                    string? property = meta.GetAttributeValue("property", string.Empty) ?? meta.GetAttributeValue("name", string.Empty);
                    if (property != null &&
                        (property.Contains("recipe", StringComparison.OrdinalIgnoreCase) ||
                         property.Contains("ingredient", StringComparison.OrdinalIgnoreCase)))
                    {
                        recipeMetaCount++;
                    }
                }
            }

            if (recipeMetaCount > 0)
                confidence += Math.Min(0.3m, recipeMetaCount * 0.05m);

            // Semantic HTML structures: lower confidence
            bool hasIngredientsList = doc.DocumentNode.SelectSingleNode("//ul[contains(@class, 'ingredient')] | //ol[contains(@class, 'ingredient')]") != null;
            bool hasInstructionsList = doc.DocumentNode.SelectSingleNode("//ol[contains(@class, 'instruction')] | //ul[contains(@class, 'instruction')]") != null;

            if (hasIngredientsList) confidence += 0.05m;
            if (hasInstructionsList) confidence += 0.05m;

            return Math.Min(1.0m, confidence);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Error calculating extraction confidence for fingerprint {FingerprintId}: {ErrorMessage}",
                fingerprint.Id,
                ex.Message);
            return 0.0m;
        }
    }

    /// <summary>
    ///     Extracts recipe from JSON-LD structured data (schema.org Recipe format).
    /// </summary>
    private async Task<Recipe?> ExtractFromJsonLdAsync(HtmlDocument doc, Fingerprint fingerprint, CancellationToken cancellationToken)
    {
        var jsonLdNodes = doc.DocumentNode.SelectNodes("//script[@type='application/ld+json']");
        if (jsonLdNodes == null)
            return null;

        foreach (var node in jsonLdNodes)
        {
            try
            {
                string jsonContent = node.InnerText.Trim();
                using var jsonDoc = JsonDocument.Parse(jsonContent);
                var root = jsonDoc.RootElement;

                // Handle both single object and array of objects
                JsonElement? recipeElement = null;

                if (root.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in root.EnumerateArray())
                    {
                        if (IsRecipeType(item))
                        {
                            recipeElement = item;
                            break;
                        }
                    }
                }
                else if (IsRecipeType(root))
                {
                    recipeElement = root;
                }

                if (recipeElement.HasValue)
                {
                    return await ParseRecipeFromJsonLdAsync(recipeElement.Value, fingerprint, cancellationToken);
                }
            }
            catch (JsonException ex)
            {
                _logger.LogDebug(
                    ex,
                    "Failed to parse JSON-LD content for fingerprint {FingerprintId}",
                    fingerprint.Id);
                continue;
            }
        }

        return null;
    }

    /// <summary>
    ///     Checks if a JSON-LD element represents a Recipe type.
    /// </summary>
    private static bool IsRecipeType(JsonElement element)
    {
        if (element.TryGetProperty("@type", out var typeProperty))
        {
            if (typeProperty.ValueKind == JsonValueKind.String)
            {
                return typeProperty.GetString()?.Equals("Recipe", StringComparison.OrdinalIgnoreCase) ?? false;
            }
            else if (typeProperty.ValueKind == JsonValueKind.Array)
            {
                foreach (var type in typeProperty.EnumerateArray())
                {
                    if (type.ValueKind == JsonValueKind.String &&
                        (type.GetString()?.Equals("Recipe", StringComparison.OrdinalIgnoreCase) ?? false))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    ///     Parses a Recipe entity from JSON-LD structured data.
    /// </summary>
    private async Task<Recipe?> ParseRecipeFromJsonLdAsync(JsonElement recipeElement, Fingerprint fingerprint, CancellationToken cancellationToken)
    {
        await Task.CompletedTask; // Satisfy async signature

        try
        {
            // Extract required fields
            string title = GetJsonString(recipeElement, "name") ?? "Untitled Recipe";
            string description = GetJsonString(recipeElement, "description") ?? string.Empty;

            // Create recipe entity
            var recipe = new Recipe(
                Guid.NewGuid(),
                title,
                description,
                fingerprint.Url,
                fingerprint.ProviderName);

            // Extract optional fields
            string? imageUrl = GetJsonString(recipeElement, "image");
            if (imageUrl != null && Uri.TryCreate(imageUrl, UriKind.Absolute, out _))
            {
                recipe.SetImageUrl(imageUrl);
            }

            // Extract timing information
            int prepTime = ParseIsoDuration(GetJsonString(recipeElement, "prepTime"));
            int cookTime = ParseIsoDuration(GetJsonString(recipeElement, "cookTime"));
            if (prepTime > 0 || cookTime > 0)
            {
                recipe.SetTimingInfo(prepTime, cookTime);
            }

            // Extract servings
            if (recipeElement.TryGetProperty("recipeYield", out var yieldProperty))
            {
                int servings = ParseServings(yieldProperty);
                if (servings > 0)
                {
                    recipe.UpdateBasicInfo(title, description, servings);
                }
            }

            // Extract ingredients
            if (recipeElement.TryGetProperty("recipeIngredient", out var ingredientsProperty))
            {
                var ingredients = ParseIngredients(ingredientsProperty);
                foreach (var ingredient in ingredients)
                {
                    recipe.AddIngredient(ingredient);
                }
            }

            // Extract instructions
            if (recipeElement.TryGetProperty("recipeInstructions", out var instructionsProperty))
            {
                var instructions = ParseInstructions(instructionsProperty);
                foreach (var instruction in instructions)
                {
                    recipe.AddInstruction(instruction);
                }
            }

            // Extract additional metadata
            string? cuisine = GetJsonString(recipeElement, "recipeCuisine");
            if (!string.IsNullOrEmpty(cuisine))
            {
                recipe.SetCuisine(cuisine);
            }

            // Only return recipe if it has the minimum required data
            if (recipe.IsReadyForPublication)
            {
                return recipe;
            }

            _logger.LogWarning(
                "Recipe extracted from JSON-LD but not ready for publication (missing ingredients or instructions). Title: {Title}, URL: {Url}",
                title,
                fingerprint.Url);

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error parsing recipe from JSON-LD for fingerprint {FingerprintId}: {ErrorMessage}",
                fingerprint.Id,
                ex.Message);
            return null;
        }
    }

    /// <summary>
    ///     Extracts recipe from HTML meta tags and semantic markup as a fallback.
    /// </summary>
    private async Task<Recipe?> ExtractFromHtmlAsync(HtmlDocument doc, Fingerprint fingerprint, CancellationToken cancellationToken)
    {
        await Task.CompletedTask; // Satisfy async signature

        try
        {
            // Extract title from meta tags or h1
            string? title = GetMetaContent(doc, "og:title") ??
                           GetMetaContent(doc, "twitter:title") ??
                           doc.DocumentNode.SelectSingleNode("//h1")?.InnerText.Trim();

            if (string.IsNullOrEmpty(title))
            {
                _logger.LogDebug(
                    "No title found in HTML for fingerprint {FingerprintId}",
                    fingerprint.Id);
                return null;
            }

            // Extract description
            string? description = GetMetaContent(doc, "og:description") ??
                                 GetMetaContent(doc, "description") ??
                                 GetMetaContent(doc, "twitter:description") ??
                                 string.Empty;

            // Create recipe entity
            var recipe = new Recipe(
                Guid.NewGuid(),
                title,
                description,
                fingerprint.Url,
                fingerprint.ProviderName);

            // Extract image
            string? imageUrl = GetMetaContent(doc, "og:image") ?? GetMetaContent(doc, "twitter:image");
            if (imageUrl != null && Uri.TryCreate(imageUrl, UriKind.Absolute, out _))
            {
                recipe.SetImageUrl(imageUrl);
            }

            // Try to extract ingredients from HTML structure
            var ingredientNodes = doc.DocumentNode.SelectNodes(
                "//ul[contains(@class, 'ingredient')] //li | //ol[contains(@class, 'ingredient')] //li | " +
                "//div[contains(@class, 'ingredient')] //li | //*[@itemprop='recipeIngredient']");

            if (ingredientNodes != null)
            {
                int order = 1;
                foreach (var node in ingredientNodes.Take(50)) // Limit to reasonable number
                {
                    string ingredientText = HtmlEntity.DeEntitize(node.InnerText.Trim());
                    if (!string.IsNullOrWhiteSpace(ingredientText) && ingredientText.Length < 200)
                    {
                        var ingredient = ParseIngredientFromText(ingredientText, order);
                        recipe.AddIngredient(ingredient);
                        order++;
                    }
                }
            }

            // Try to extract instructions from HTML structure
            var instructionNodes = doc.DocumentNode.SelectNodes(
                "//ol[contains(@class, 'instruction')] //li | //ol[contains(@class, 'step')] //li | " +
                "//*[@itemprop='recipeInstructions'] //li | //*[contains(@class, 'recipe-step')]");

            if (instructionNodes != null)
            {
                int step = 1;
                foreach (var node in instructionNodes.Take(50)) // Limit to reasonable number
                {
                    string instructionText = HtmlEntity.DeEntitize(node.InnerText.Trim());
                    if (!string.IsNullOrWhiteSpace(instructionText) && instructionText.Length < 2000)
                    {
                        var instruction = new Instruction(step, instructionText);
                        recipe.AddInstruction(instruction);
                        step++;
                    }
                }
            }

            // Only return recipe if it has the minimum required data
            if (recipe.IsReadyForPublication)
            {
                return recipe;
            }

            _logger.LogDebug(
                "Recipe extracted from HTML but not ready for publication (missing ingredients or instructions). Title: {Title}, URL: {Url}",
                title,
                fingerprint.Url);

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error parsing recipe from HTML for fingerprint {FingerprintId}: {ErrorMessage}",
                fingerprint.Id,
                ex.Message);
            return null;
        }
    }

    #region Helper Methods

    /// <summary>
    ///     Gets a string value from a JSON element by property name.
    /// </summary>
    private static string? GetJsonString(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var property))
        {
            if (property.ValueKind == JsonValueKind.String)
            {
                return property.GetString();
            }
            else if (property.ValueKind == JsonValueKind.Array && property.GetArrayLength() > 0)
            {
                // Take first element if array
                var firstElement = property[0];
                if (firstElement.ValueKind == JsonValueKind.String)
                {
                    return firstElement.GetString();
                }
                else if (firstElement.ValueKind == JsonValueKind.Object &&
                         firstElement.TryGetProperty("url", out var urlProp))
                {
                    return urlProp.GetString();
                }
            }
            else if (property.ValueKind == JsonValueKind.Object &&
                     property.TryGetProperty("url", out var urlProp))
            {
                return urlProp.GetString();
            }
        }

        return null;
    }

    /// <summary>
    ///     Parses ISO 8601 duration format (e.g., "PT30M", "PT1H30M") to minutes.
    /// </summary>
    private static int ParseIsoDuration(string? duration)
    {
        if (string.IsNullOrEmpty(duration))
            return 0;

        try
        {
            // Match patterns like PT1H30M, PT30M, PT1H
            var match = Regex.Match(duration, @"PT(?:(\d+)H)?(?:(\d+)M)?");
            if (match.Success)
            {
                int hours = match.Groups[1].Success ? int.Parse(match.Groups[1].Value) : 0;
                int minutes = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;
                return (hours * 60) + minutes;
            }
        }
        catch
        {
            // Ignore parsing errors
        }

        return 0;
    }

    /// <summary>
    ///     Parses servings from various JSON formats.
    /// </summary>
    private static int ParseServings(JsonElement yieldProperty)
    {
        if (yieldProperty.ValueKind == JsonValueKind.Number)
        {
            return yieldProperty.GetInt32();
        }
        else if (yieldProperty.ValueKind == JsonValueKind.String)
        {
            string? yieldStr = yieldProperty.GetString();
            if (!string.IsNullOrEmpty(yieldStr))
            {
                // Extract first number from string like "4 servings" or "Serves 6"
                var match = Regex.Match(yieldStr, @"\d+");
                if (match.Success && int.TryParse(match.Value, out int servings))
                {
                    return servings;
                }
            }
        }

        return 1; // Default to 1 serving
    }

    /// <summary>
    ///     Parses ingredients array from JSON-LD.
    /// </summary>
    private List<Ingredient> ParseIngredients(JsonElement ingredientsProperty)
    {
        var ingredients = new List<Ingredient>();

        if (ingredientsProperty.ValueKind != JsonValueKind.Array)
            return ingredients;

        int order = 1;
        foreach (var item in ingredientsProperty.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                string? ingredientText = item.GetString();
                if (!string.IsNullOrWhiteSpace(ingredientText))
                {
                    var ingredient = ParseIngredientFromText(ingredientText, order);
                    ingredients.Add(ingredient);
                    order++;
                }
            }
        }

        return ingredients;
    }

    /// <summary>
    ///     Parses a single ingredient from text format.
    ///     Attempts to extract amount, unit, and name from strings like "2 cups flour" or "1 tbsp olive oil".
    /// </summary>
    private static Ingredient ParseIngredientFromText(string text, int order)
    {
        // Simple pattern matching for common ingredient formats
        // Pattern: [amount] [unit] [ingredient name]
        var match = Regex.Match(text, @"^([\d\/\.\s]+)\s*([a-zA-Z]+)?\s+(.+)$");

        if (match.Success)
        {
            string amount = match.Groups[1].Value.Trim();
            string unit = match.Groups[2].Success ? match.Groups[2].Value : "unit";
            string name = match.Groups[3].Value.Trim();

            return new Ingredient(name, amount, unit, null, false, order);
        }

        // Fallback: treat entire text as ingredient name
        return new Ingredient(text, "1", "unit", null, false, order);
    }

    /// <summary>
    ///     Parses instructions array from JSON-LD.
    /// </summary>
    private List<Instruction> ParseInstructions(JsonElement instructionsProperty)
    {
        var instructions = new List<Instruction>();

        if (instructionsProperty.ValueKind == JsonValueKind.Array)
        {
            int step = 1;
            foreach (var item in instructionsProperty.EnumerateArray())
            {
                string? instructionText = null;

                if (item.ValueKind == JsonValueKind.String)
                {
                    instructionText = item.GetString();
                }
                else if (item.ValueKind == JsonValueKind.Object)
                {
                    // Handle HowToStep format
                    instructionText = GetJsonString(item, "text");
                }

                if (!string.IsNullOrWhiteSpace(instructionText))
                {
                    instructions.Add(new Instruction(step, instructionText));
                    step++;
                }
            }
        }
        else if (instructionsProperty.ValueKind == JsonValueKind.String)
        {
            // Single string instruction - split by newlines or periods
            string? text = instructionsProperty.GetString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                var steps = text.Split(new[] { '\n', '.' }, StringSplitOptions.RemoveEmptyEntries);
                int step = 1;
                foreach (var stepText in steps)
                {
                    string trimmed = stepText.Trim();
                    if (!string.IsNullOrWhiteSpace(trimmed))
                    {
                        instructions.Add(new Instruction(step, trimmed));
                        step++;
                    }
                }
            }
        }

        return instructions;
    }

    /// <summary>
    ///     Gets meta tag content by property or name attribute.
    /// </summary>
    private static string? GetMetaContent(HtmlDocument doc, string propertyOrName)
    {
        var node = doc.DocumentNode.SelectSingleNode(
            $"//meta[@property='{propertyOrName}' or @name='{propertyOrName}']");

        return node?.GetAttributeValue("content", string.Empty);
    }

    #endregion
}
