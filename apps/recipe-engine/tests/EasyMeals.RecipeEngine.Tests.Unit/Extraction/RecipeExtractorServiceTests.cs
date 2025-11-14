using EasyMeals.RecipeEngine.Domain.Entities;
using EasyMeals.RecipeEngine.Domain.Interfaces;
using EasyMeals.RecipeEngine.Domain.ValueObjects.Fingerprint;
using EasyMeals.RecipeEngine.Infrastructure.Extraction;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace EasyMeals.RecipeEngine.Tests.Unit.Extraction;

/// <summary>
///     Unit tests for RecipeExtractorService.
///     Tests cover: JSON-LD extraction, HTML fallback extraction, confidence scoring, and edge cases.
/// </summary>
public class RecipeExtractorServiceTests
{
    private readonly Mock<ILogger<RecipeExtractorService>> _mockLogger;
    private readonly IRecipeExtractor _sut;

    public RecipeExtractorServiceTests()
    {
        _mockLogger = new Mock<ILogger<RecipeExtractorService>>();
        _sut = new RecipeExtractorService(_mockLogger.Object);
    }

    [Fact(DisplayName = "ExtractRecipeAsync with JSON-LD structured data returns valid recipe")]
    public async Task ExtractRecipeAsync_JsonLdStructuredData_ReturnsValidRecipe()
    {
        // Arrange
        const string htmlContent = @"
<!DOCTYPE html>
<html>
<head>
    <script type=""application/ld+json"">
    {
        ""@context"": ""https://schema.org"",
        ""@type"": ""Recipe"",
        ""name"": ""Chocolate Chip Cookies"",
        ""description"": ""Delicious homemade chocolate chip cookies"",
        ""image"": ""https://example.com/cookies.jpg"",
        ""prepTime"": ""PT15M"",
        ""cookTime"": ""PT12M"",
        ""recipeYield"": ""24 cookies"",
        ""recipeIngredient"": [
            ""2 cups all-purpose flour"",
            ""1 tsp baking soda"",
            ""1 cup butter"",
            ""3/4 cup granulated sugar"",
            ""3/4 cup packed brown sugar"",
            ""2 large eggs"",
            ""2 tsp vanilla extract"",
            ""2 cups semisweet chocolate chips""
        ],
        ""recipeInstructions"": [
            {
                ""@type"": ""HowToStep"",
                ""text"": ""Preheat oven to 375°F""
            },
            {
                ""@type"": ""HowToStep"",
                ""text"": ""Mix flour and baking soda in a bowl""
            },
            {
                ""@type"": ""HowToStep"",
                ""text"": ""Beat butter and sugars until creamy""
            },
            {
                ""@type"": ""HowToStep"",
                ""text"": ""Add eggs and vanilla""
            },
            {
                ""@type"": ""HowToStep"",
                ""text"": ""Stir in chocolate chips""
            },
            {
                ""@type"": ""HowToStep"",
                ""text"": ""Bake for 9-11 minutes""
            }
        ],
        ""recipeCuisine"": ""American""
    }
    </script>
</head>
<body></body>
</html>";

        var fingerprint = new Fingerprint(
            Guid.NewGuid(),
            "https://example.com/cookies",
            htmlContent,
            "test-provider",
            ScrapingQuality.Good);

        // Act
        Recipe? recipe = await _sut.ExtractRecipeAsync(fingerprint);

        // Assert
        recipe.ShouldNotBeNull();
        recipe.Title.ShouldBe("Chocolate Chip Cookies");
        recipe.Description.ShouldBe("Delicious homemade chocolate chip cookies");
        recipe.SourceUrl.ShouldBe("https://example.com/cookies");
        recipe.SourceProvider.ShouldBe("test-provider");
        recipe.PrepTimeMinutes.ShouldBe(15);
        recipe.CookTimeMinutes.ShouldBe(12);
        recipe.Servings.ShouldBe(24);
        recipe.Ingredients.Count.ShouldBe(8);
        recipe.Instructions.Count.ShouldBe(6);
        recipe.Cuisine.ShouldBe("American");
        recipe.IsReadyForPublication.ShouldBeTrue();
    }

    [Fact(DisplayName = "ExtractRecipeAsync with null fingerprint throws ArgumentNullException")]
    public async Task ExtractRecipeAsync_NullFingerprint_ThrowsArgumentNullException()
    {
        // Act
        Func<Task> act = async () => await _sut.ExtractRecipeAsync(null!);

        // Assert
        await Should.ThrowAsync<ArgumentNullException>(act);
    }

    [Fact(DisplayName = "ExtractRecipeAsync with empty raw content returns null")]
    public async Task ExtractRecipeAsync_EmptyRawContent_ReturnsNull()
    {
        // Arrange
        var fingerprint = Fingerprint.Reconstitute(
            Guid.NewGuid(),
            "https://example.com/recipe",
            "hash123",
            null, // Empty raw content
            DateTime.UtcNow,
            "test-provider",
            FingerprintStatus.Success,
            ScrapingQuality.Good,
            null,
            new Dictionary<string, object>(),
            0,
            null,
            null,
            DateTime.UtcNow,
            DateTime.UtcNow);

        // Act
        Recipe? recipe = await _sut.ExtractRecipeAsync(fingerprint);

        // Assert
        recipe.ShouldBeNull();
    }

    [Fact(DisplayName = "ExtractRecipeAsync with HTML fallback returns valid recipe")]
    public async Task ExtractRecipeAsync_HtmlFallback_ReturnsValidRecipe()
    {
        // Arrange
        const string htmlContent = @"
<!DOCTYPE html>
<html>
<head>
    <meta property=""og:title"" content=""Simple Pasta Recipe"" />
    <meta property=""og:description"" content=""A quick and easy pasta dish"" />
    <meta property=""og:image"" content=""https://example.com/pasta.jpg"" />
</head>
<body>
    <h1>Simple Pasta Recipe</h1>
    <ul class=""ingredients-list"">
        <li>1 lb pasta</li>
        <li>2 tbsp olive oil</li>
        <li>3 cloves garlic</li>
        <li>1 cup cherry tomatoes</li>
    </ul>
    <ol class=""instructions-list"">
        <li>Boil pasta according to package directions</li>
        <li>Heat olive oil in a pan</li>
        <li>Add garlic and cook until fragrant</li>
        <li>Add tomatoes and cook until soft</li>
        <li>Toss with cooked pasta</li>
    </ol>
</body>
</html>";

        var fingerprint = new Fingerprint(
            Guid.NewGuid(),
            "https://example.com/pasta",
            htmlContent,
            "test-provider",
            ScrapingQuality.Good);

        // Act
        Recipe? recipe = await _sut.ExtractRecipeAsync(fingerprint);

        // Assert
        recipe.ShouldNotBeNull();
        recipe.Title.ShouldBe("Simple Pasta Recipe");
        recipe.Description.ShouldBe("A quick and easy pasta dish");
        recipe.Ingredients.Count.ShouldBe(4);
        recipe.Instructions.Count.ShouldBe(5);
        recipe.IsReadyForPublication.ShouldBeTrue();
    }

    [Fact(DisplayName = "ExtractRecipeAsync with incomplete data returns null")]
    public async Task ExtractRecipeAsync_IncompleteData_ReturnsNull()
    {
        // Arrange - only has title, no ingredients or instructions
        const string htmlContent = @"
<!DOCTYPE html>
<html>
<head>
    <meta property=""og:title"" content=""Incomplete Recipe"" />
</head>
<body>
    <h1>Incomplete Recipe</h1>
    <p>Some content but no recipe structure</p>
</body>
</html>";

        var fingerprint = new Fingerprint(
            Guid.NewGuid(),
            "https://example.com/incomplete",
            htmlContent,
            "test-provider",
            ScrapingQuality.Good);

        // Act
        Recipe? recipe = await _sut.ExtractRecipeAsync(fingerprint);

        // Assert
        recipe.ShouldBeNull();
    }

    [Fact(DisplayName = "CanExtractRecipe with JSON-LD Recipe type returns true")]
    public async Task CanExtractRecipe_JsonLdRecipeType_ReturnsTrue()
    {
        // Arrange
        const string htmlContent = @"
<!DOCTYPE html>
<html>
<head>
    <script type=""application/ld+json"">
    {
        ""@type"": ""Recipe"",
        ""name"": ""Test Recipe""
    }
    </script>
</head>
<body></body>
</html>";

        var fingerprint = new Fingerprint(
            Guid.NewGuid(),
            "https://example.com/test",
            htmlContent,
            "test-provider",
            ScrapingQuality.Good);

        // Act
        bool canExtract = _sut.CanExtractRecipe(fingerprint);

        // Assert
        canExtract.ShouldBeTrue();
    }

    [Fact(DisplayName = "CanExtractRecipe with recipe meta tags returns true")]
    public async Task CanExtractRecipe_RecipeMetaTags_ReturnsTrue()
    {
        // Arrange
        const string htmlContent = @"
<!DOCTYPE html>
<html>
<head>
    <meta property=""recipe:ingredient"" content=""flour"" />
</head>
<body></body>
</html>";

        var fingerprint = new Fingerprint(
            Guid.NewGuid(),
            "https://example.com/test",
            htmlContent,
            "test-provider",
            ScrapingQuality.Good);

        // Act
        bool canExtract = _sut.CanExtractRecipe(fingerprint);

        // Assert
        canExtract.ShouldBeTrue();
    }

    [Fact(DisplayName = "CanExtractRecipe with ingredient list structure returns true")]
    public async Task CanExtractRecipe_IngredientListStructure_ReturnsTrue()
    {
        // Arrange
        const string htmlContent = @"
<!DOCTYPE html>
<html>
<body>
    <ul class=""ingredient-list"">
        <li>Item 1</li>
        <li>Item 2</li>
    </ul>
</body>
</html>";

        var fingerprint = new Fingerprint(
            Guid.NewGuid(),
            "https://example.com/test",
            htmlContent,
            "test-provider",
            ScrapingQuality.Good);

        // Act
        bool canExtract = _sut.CanExtractRecipe(fingerprint);

        // Assert
        canExtract.ShouldBeTrue();
    }

    [Fact(DisplayName = "CanExtractRecipe with no recipe content returns false")]
    public async Task CanExtractRecipe_NoRecipeContent_ReturnsFalse()
    {
        // Arrange
        const string htmlContent = @"
<!DOCTYPE html>
<html>
<body>
    <h1>Not a recipe</h1>
    <p>Just some regular content</p>
</body>
</html>";

        var fingerprint = new Fingerprint(
            Guid.NewGuid(),
            "https://example.com/test",
            htmlContent,
            "test-provider",
            ScrapingQuality.Good);

        // Act
        bool canExtract = _sut.CanExtractRecipe(fingerprint);

        // Assert
        canExtract.ShouldBeFalse();
    }

    [Fact(DisplayName = "CanExtractRecipe with null fingerprint returns false")]
    public async Task CanExtractRecipe_NullFingerprint_ReturnsFalse()
    {
        // Act
        bool canExtract = _sut.CanExtractRecipe(null!);

        // Assert
        canExtract.ShouldBeFalse();
    }

    [Fact(DisplayName = "GetExtractionConfidence with JSON-LD returns high confidence")]
    public async Task GetExtractionConfidence_JsonLd_ReturnsHighConfidence()
    {
        // Arrange
        const string htmlContent = @"
<!DOCTYPE html>
<html>
<head>
    <script type=""application/ld+json"">
    {
        ""@type"": ""Recipe"",
        ""name"": ""Test Recipe""
    }
    </script>
</head>
<body></body>
</html>";

        var fingerprint = new Fingerprint(
            Guid.NewGuid(),
            "https://example.com/test",
            htmlContent,
            "test-provider",
            ScrapingQuality.Good);

        // Act
        decimal confidence = _sut.GetExtractionConfidence(fingerprint);

        // Assert
        confidence.ShouldBeGreaterThanOrEqualTo(0.6m);
    }

    [Fact(DisplayName = "GetExtractionConfidence with meta tags returns medium confidence")]
    public async Task GetExtractionConfidence_MetaTags_ReturnsMediumConfidence()
    {
        // Arrange
        const string htmlContent = @"
<!DOCTYPE html>
<html>
<head>
    <meta property=""recipe:ingredient"" content=""flour"" />
    <meta property=""recipe:ingredient"" content=""sugar"" />
</head>
<body></body>
</html>";

        var fingerprint = new Fingerprint(
            Guid.NewGuid(),
            "https://example.com/test",
            htmlContent,
            "test-provider",
            ScrapingQuality.Good);

        // Act
        decimal confidence = _sut.GetExtractionConfidence(fingerprint);

        // Assert
        confidence.ShouldBeGreaterThan(0.0m);
        confidence.ShouldBeLessThan(0.6m);
    }

    [Fact(DisplayName = "GetExtractionConfidence with no recipe content returns zero")]
    public async Task GetExtractionConfidence_NoRecipeContent_ReturnsZero()
    {
        // Arrange
        const string htmlContent = @"
<!DOCTYPE html>
<html>
<body>
    <h1>Not a recipe</h1>
</body>
</html>";

        var fingerprint = new Fingerprint(
            Guid.NewGuid(),
            "https://example.com/test",
            htmlContent,
            "test-provider",
            ScrapingQuality.Good);

        // Act
        decimal confidence = _sut.GetExtractionConfidence(fingerprint);

        // Assert
        confidence.ShouldBe(0.0m);
    }

    [Fact(DisplayName = "GetExtractionConfidence with null fingerprint returns zero")]
    public async Task GetExtractionConfidence_NullFingerprint_ReturnsZero()
    {
        // Act
        decimal confidence = _sut.GetExtractionConfidence(null!);

        // Assert
        confidence.ShouldBe(0.0m);
    }

    [Fact(DisplayName = "ExtractRecipeAsync with ISO duration formats parses correctly")]
    public async Task ExtractRecipeAsync_IsoDurationFormats_ParsesCorrectly()
    {
        // Arrange
        const string htmlContent = @"
<!DOCTYPE html>
<html>
<head>
    <script type=""application/ld+json"">
    {
        ""@type"": ""Recipe"",
        ""name"": ""Quick Meal"",
        ""description"": ""Fast and easy"",
        ""prepTime"": ""PT1H30M"",
        ""cookTime"": ""PT45M"",
        ""recipeIngredient"": [""ingredient1""],
        ""recipeInstructions"": [""step1""]
    }
    </script>
</head>
<body></body>
</html>";

        var fingerprint = new Fingerprint(
            Guid.NewGuid(),
            "https://example.com/quick-meal",
            htmlContent,
            "test-provider",
            ScrapingQuality.Good);

        // Act
        Recipe? recipe = await _sut.ExtractRecipeAsync(fingerprint);

        // Assert
        recipe.ShouldNotBeNull();
        recipe.PrepTimeMinutes.ShouldBe(90); // 1 hour 30 minutes
        recipe.CookTimeMinutes.ShouldBe(45);
    }

    [Fact(DisplayName = "ExtractRecipeAsync with array JSON-LD finds Recipe type")]
    public async Task ExtractRecipeAsync_ArrayJsonLd_FindsRecipeType()
    {
        // Arrange
        const string htmlContent = @"
<!DOCTYPE html>
<html>
<head>
    <script type=""application/ld+json"">
    [
        {
            ""@type"": ""Organization"",
            ""name"": ""Test Org""
        },
        {
            ""@type"": ""Recipe"",
            ""name"": ""Array Recipe"",
            ""description"": ""Recipe in array"",
            ""recipeIngredient"": [""ingredient1""],
            ""recipeInstructions"": [""step1""]
        }
    ]
    </script>
</head>
<body></body>
</html>";

        var fingerprint = new Fingerprint(
            Guid.NewGuid(),
            "https://example.com/array-recipe",
            htmlContent,
            "test-provider",
            ScrapingQuality.Good);

        // Act
        Recipe? recipe = await _sut.ExtractRecipeAsync(fingerprint);

        // Assert
        recipe.ShouldNotBeNull();
        recipe.Title.ShouldBe("Array Recipe");
    }
}
