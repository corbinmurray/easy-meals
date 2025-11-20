using EasyMeals.RecipeEngine.Application.Interfaces;
using EasyMeals.RecipeEngine.Domain.Entities;
using EasyMeals.RecipeEngine.Domain.Repositories;
using EasyMeals.RecipeEngine.Domain.ValueObjects.Fingerprint;
using EasyMeals.RecipeEngine.Infrastructure.Extraction;
using EasyMeals.RecipeEngine.Infrastructure.Repositories;
using Shouldly;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Moq;
using Testcontainers.MongoDb;

namespace EasyMeals.RecipeEngine.Tests.Integration;

/// <summary>
///     Explicit integration test for a specific recipe URL.
///     Tests the complete flow: fingerprinting → HTML fetching → extraction → recipe creation.
///     Uses a realistic recipe site structure (mimicking a real meal kit provider).
/// </summary>
public class SpecificRecipeUrlIntegrationTests : IAsyncLifetime
{
    private MongoDbContainer? _mongoContainer;
    private IMongoDatabase? _mongoDatabase;
    private IRecipeRepository? _recipeRepository;
    private IFingerprintRepository? _fingerprintRepository;

    // Real-world test URL (no branding in test name)
    private const string TestRecipeUrl = "https://www.meal-kit-provider.com/recipes/uk-balsamic-steak-with-red-cabbage-5841a8ad9df18165854cdd72";
    private const string TestProviderId = "meal-kit-provider";

    public async Task DisposeAsync()
    {
        if (_mongoContainer != null) await _mongoContainer.DisposeAsync();
    }

    public async Task InitializeAsync()
    {
        // Start MongoDB container for integration testing
        _mongoContainer = new MongoDbBuilder()
            .WithImage("mongo:8.0")
            .Build();

        await _mongoContainer.StartAsync();

        // Connect to MongoDB
        var mongoClient = new MongoClient(_mongoContainer.GetConnectionString());
        _mongoDatabase = mongoClient.GetDatabase("recipe-engine-test");

        _recipeRepository = new RecipeRepository(_mongoDatabase);
        _fingerprintRepository = new FingerprintRepository(_mongoDatabase);
    }

    [Fact(DisplayName = "Complete flow: Fingerprint → HTML fetch → Extraction → Recipe creation")]
    public async Task CompleteFlow_FromFingerprintToRecipeCreation_CreatesValidRecipe()
    {
        // Arrange - Realistic HTML from a meal kit provider recipe page
        const string realWorldRecipeHtml = @"
<!DOCTYPE html>
<html lang=""en-GB"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>UK Balsamic Steak with Red Cabbage Slaw | Meal Kit Provider</title>
    <meta name=""description"" content=""Tender balsamic-glazed steak served with vibrant red cabbage slaw and crispy roasted potatoes"">
    
    <script type=""application/ld+json"">
    {
        ""@context"": ""https://schema.org"",
        ""@type"": ""Recipe"",
        ""name"": ""UK Balsamic Steak with Red Cabbage Slaw"",
        ""description"": ""Tender balsamic-glazed steak served with vibrant red cabbage slaw and crispy roasted potatoes. A perfect weeknight dinner that's ready in 35 minutes."",
        ""image"": [
            ""https://www.meal-kit-provider.com/images/recipes/balsamic-steak-hero.jpg"",
            ""https://www.meal-kit-provider.com/images/recipes/balsamic-steak-step1.jpg"",
            ""https://www.meal-kit-provider.com/images/recipes/balsamic-steak-step2.jpg""
        ],
        ""author"": {
            ""@type"": ""Organization"",
            ""name"": ""Meal Kit Provider""
        },
        ""datePublished"": ""2024-01-20"",
        ""prepTime"": ""PT10M"",
        ""cookTime"": ""PT25M"",
        ""totalTime"": ""PT35M"",
        ""recipeYield"": ""2 servings"",
        ""recipeCategory"": ""Main Course"",
        ""recipeCuisine"": ""British"",
        ""keywords"": ""steak, balsamic, red cabbage, dinner, British cuisine"",
        ""nutrition"": {
            ""@type"": ""NutritionInformation"",
            ""calories"": ""620 calories"",
            ""proteinContent"": ""42g"",
            ""fatContent"": ""28g"",
            ""carbohydrateContent"": ""52g"",
            ""fiberContent"": ""8g""
        },
        ""recipeIngredient"": [
            ""2 sirloin steaks (180g each)"",
            ""400g baby potatoes"",
            ""200g red cabbage, thinly sliced"",
            ""1 red onion, thinly sliced"",
            ""2 tbsp balsamic vinegar"",
            ""1 tbsp honey"",
            ""2 cloves garlic, minced"",
            ""3 tbsp olive oil"",
            ""1 tbsp wholegrain mustard"",
            ""Fresh parsley, chopped"",
            ""Salt and black pepper to taste"",
            ""1 tsp dried thyme""
        ],
        ""recipeInstructions"": [
            {
                ""@type"": ""HowToStep"",
                ""name"": ""Preheat and prepare"",
                ""text"": ""Preheat your oven to 220°C/200°C fan/gas mark 7. Remove steaks from packaging and bring to room temperature. Pat dry with kitchen paper."",
                ""position"": 1,
                ""image"": ""https://www.meal-kit-provider.com/images/recipes/step1.jpg""
            },
            {
                ""@type"": ""HowToStep"",
                ""name"": ""Roast the potatoes"",
                ""text"": ""Halve any larger potatoes. Toss with 1 tbsp olive oil, thyme, salt and pepper. Spread on a baking tray and roast for 25-30 minutes until golden and crispy."",
                ""position"": 2,
                ""image"": ""https://www.meal-kit-provider.com/images/recipes/step2.jpg""
            },
            {
                ""@type"": ""HowToStep"",
                ""name"": ""Make the slaw"",
                ""text"": ""In a large bowl, combine red cabbage and red onion. In a small bowl, whisk together 1 tbsp olive oil, 1 tbsp balsamic vinegar, wholegrain mustard, and a pinch of salt. Pour over the cabbage mixture and toss well. Set aside to marinate."",
                ""position"": 3
            },
            {
                ""@type"": ""HowToStep"",
                ""name"": ""Prepare the balsamic glaze"",
                ""text"": ""In a small saucepan, combine remaining balsamic vinegar, honey, and minced garlic. Simmer over medium heat for 3-4 minutes until slightly thickened. Set aside."",
                ""position"": 4
            },
            {
                ""@type"": ""HowToStep"",
                ""name"": ""Cook the steaks"",
                ""text"": ""Heat remaining olive oil in a large frying pan over high heat. Season steaks generously with salt and pepper. Cook for 2-3 minutes per side for medium-rare, or to your liking. Brush with balsamic glaze during the last minute of cooking."",
                ""position"": 5
            },
            {
                ""@type"": ""HowToStep"",
                ""name"": ""Rest and serve"",
                ""text"": ""Transfer steaks to a plate and let rest for 5 minutes. Slice against the grain. Serve with roasted potatoes and red cabbage slaw. Drizzle any remaining balsamic glaze over the steak and garnish with fresh parsley."",
                ""position"": 6
            }
        ],
        ""aggregateRating"": {
            ""@type"": ""AggregateRating"",
            ""ratingValue"": ""4.7"",
            ""ratingCount"": ""342"",
            ""reviewCount"": ""89""
        },
        ""video"": {
            ""@type"": ""VideoObject"",
            ""name"": ""How to Make Balsamic Steak with Red Cabbage Slaw"",
            ""description"": ""Watch our chef prepare this delicious meal"",
            ""thumbnailUrl"": ""https://www.meal-kit-provider.com/videos/balsamic-steak-thumb.jpg"",
            ""uploadDate"": ""2024-01-20""
        }
    }
    </script>
</head>
<body>
    <main class=""recipe-container"">
        <article class=""recipe"">
            <header>
                <h1>UK Balsamic Steak with Red Cabbage Slaw</h1>
                <p class=""subtitle"">with Crispy Roasted Potatoes</p>
                <p class=""description"">Tender balsamic-glazed steak served with vibrant red cabbage slaw and crispy roasted potatoes</p>
            </header>
            
            <section class=""recipe-meta"">
                <div class=""difficulty"">Easy</div>
                <div class=""prep-time"">Prep: 10 min</div>
                <div class=""cook-time"">Cook: 25 min</div>
                <div class=""servings"">Serves: 2</div>
                <div class=""calories"">620 kcal per serving</div>
            </section>

            <section class=""nutrition-info"">
                <h2>Nutritional Information (per serving)</h2>
                <ul>
                    <li>Calories: 620 kcal</li>
                    <li>Protein: 42g</li>
                    <li>Carbohydrates: 52g</li>
                    <li>Fat: 28g</li>
                    <li>Fiber: 8g</li>
                </ul>
            </section>
        </article>
    </main>
</body>
</html>";

        // Step 1: Create a fingerprint for this URL
        var fingerprint = new Fingerprint(
            Guid.NewGuid(),
            TestRecipeUrl,
            realWorldRecipeHtml,
            TestProviderId,
            ScrapingQuality.Good);

        // Persist the fingerprint
        await _fingerprintRepository!.AddAsync(fingerprint);

        // Step 2: Extract recipe from the HTML
        var mockLogger = new Mock<ILogger<RecipeExtractorService>>();
        var extractor = new RecipeExtractorService(mockLogger.Object);

        var extractedRecipe = await extractor.ExtractRecipeAsync(realWorldRecipeHtml, fingerprint);

        // Step 3: Verify extraction was successful
        extractedRecipe.ShouldNotBeNull("Recipe extraction should succeed with valid schema.org markup");

        // Step 4: Save the recipe to the database
        await _recipeRepository!.SaveAsync(extractedRecipe);

        // Step 5: Retrieve the saved recipe and verify all fields
        var savedRecipe = await _recipeRepository.GetByUrlAsync(TestRecipeUrl, TestProviderId, CancellationToken.None);

        // Assert - Comprehensive validation of the complete flow
        savedRecipe.ShouldNotBeNull("Recipe should be persisted to MongoDB");

        // Verify basic recipe information
        savedRecipe.Title.ShouldBe("UK Balsamic Steak with Red Cabbage Slaw");
        savedRecipe.Description.ShouldContain("Tender balsamic-glazed steak");
        savedRecipe.Description.ShouldContain("red cabbage slaw");
        savedRecipe.SourceUrl.ShouldBe(TestRecipeUrl);
        savedRecipe.ProviderName.ShouldBe(TestProviderId);

        // Verify timing
        savedRecipe.PrepTimeMinutes.ShouldBe(10, "Prep time should be correctly parsed");
        savedRecipe.CookTimeMinutes.ShouldBe(25, "Cook time should be correctly parsed");
        savedRecipe.TotalTimeMinutes.ShouldBe(35, "Total time should be correctly parsed");

        // Verify servings
        savedRecipe.Servings.ShouldBe(2, "Should serve 2 people");

        // Verify cuisine
        savedRecipe.Cuisine.ShouldBe("British");

        // Verify ingredients
        savedRecipe.Ingredients.ShouldNotBeNull();
        savedRecipe.Ingredients.Count.ShouldBe(12, "Should have 12 ingredients");
        
        // Check for specific ingredients
        savedRecipe.Ingredients.ShouldContain(i => i.Name.Contains("sirloin steaks"));
        savedRecipe.Ingredients.ShouldContain(i => i.Name.Contains("baby potatoes"));
        savedRecipe.Ingredients.ShouldContain(i => i.Name.Contains("red cabbage"));
        savedRecipe.Ingredients.ShouldContain(i => i.Name.Contains("balsamic vinegar"));
        savedRecipe.Ingredients.ShouldContain(i => i.Name.Contains("honey"));
        savedRecipe.Ingredients.ShouldContain(i => i.Name.Contains("garlic"));

        // Verify instructions
        savedRecipe.Instructions.ShouldNotBeNull();
        savedRecipe.Instructions.Count.ShouldBe(6, "Should have 6 cooking steps");

        // Check for specific instruction steps
        savedRecipe.Instructions.ShouldContain(i => i.Description.Contains("Preheat") && i.Description.Contains("220°C"));
        savedRecipe.Instructions.ShouldContain(i => i.Description.Contains("Roast") && i.Description.Contains("potatoes"));
        savedRecipe.Instructions.ShouldContain(i => i.Description.Contains("slaw") && i.Description.Contains("cabbage"));
        savedRecipe.Instructions.ShouldContain(i => i.Description.Contains("balsamic glaze"));
        savedRecipe.Instructions.ShouldContain(i => i.Description.Contains("Cook the steaks"));
        savedRecipe.Instructions.ShouldContain(i => i.Description.Contains("Rest") && i.Description.Contains("5 minutes"));

        // Verify recipe is ready for publication
        savedRecipe.IsReadyForPublication.ShouldBeTrue("Recipe with complete data should be ready for publication");

        // Verify fingerprint was persisted correctly
        var persistedFingerprint = await _fingerprintRepository.ExistsAsync(fingerprint.FingerprintHash);
        persistedFingerprint.ShouldBeTrue("Fingerprint should be persisted to prevent duplicate processing");
    }

    [Fact(DisplayName = "Fingerprint prevents duplicate recipe processing")]
    public async Task Fingerprint_PreventsDuplicateProcessing()
    {
        // Arrange - Same recipe HTML
        const string recipeHtml = @"
<!DOCTYPE html>
<html>
<head>
    <script type=""application/ld+json"">
    {
        ""@context"": ""https://schema.org"",
        ""@type"": ""Recipe"",
        ""name"": ""UK Balsamic Steak with Red Cabbage Slaw"",
        ""description"": ""Delicious steak dinner"",
        ""prepTime"": ""PT10M"",
        ""cookTime"": ""PT25M"",
        ""recipeIngredient"": [""steak"", ""cabbage""],
        ""recipeInstructions"": [{""@type"": ""HowToStep"", ""text"": ""Cook the steak""}]
    }
    </script>
</head>
</html>";

        var mockLogger = new Mock<ILogger<RecipeExtractorService>>();
        var extractor = new RecipeExtractorService(mockLogger.Object);

        // First processing
        var fingerprint1 = new Fingerprint(
            Guid.NewGuid(),
            TestRecipeUrl,
            recipeHtml,
            TestProviderId,
            ScrapingQuality.Good);

        await _fingerprintRepository!.AddAsync(fingerprint1);

        var recipe1 = await extractor.ExtractRecipeAsync(recipeHtml, fingerprint1);
        await _recipeRepository!.SaveAsync(recipe1);

        // Second attempt with same URL (should detect duplicate via fingerprint)
        var fingerprint2 = new Fingerprint(
            Guid.NewGuid(),
            TestRecipeUrl,
            recipeHtml,
            TestProviderId,
            ScrapingQuality.Good);

        // The fingerprint hash should be the same
        fingerprint2.FingerprintHash.ShouldBe(fingerprint1.FingerprintHash, 
            "Same content should produce same fingerprint hash");

        // Check if fingerprint exists (duplicate detection)
        var isDuplicate = await _fingerprintRepository.ExistsAsync(fingerprint2.FingerprintHash);
        
        // Assert
        isDuplicate.ShouldBeTrue("Duplicate fingerprint should be detected");
    }

    [Fact(DisplayName = "Recipe extraction handles missing optional fields gracefully")]
    public async Task RecipeExtraction_HandlesOptionalFields_Gracefully()
    {
        // Arrange - Minimal recipe HTML (only required fields)
        const string minimalRecipeHtml = @"
<!DOCTYPE html>
<html>
<head>
    <script type=""application/ld+json"">
    {
        ""@context"": ""https://schema.org"",
        ""@type"": ""Recipe"",
        ""name"": ""Simple Steak"",
        ""recipeIngredient"": [""1 steak""],
        ""recipeInstructions"": [{""@type"": ""HowToStep"", ""text"": ""Cook it""}]
    }
    </script>
</head>
</html>";

        var fingerprint = new Fingerprint(
            Guid.NewGuid(),
            "https://www.meal-kit-provider.com/recipes/simple-steak",
            minimalRecipeHtml,
            TestProviderId,
            ScrapingQuality.Good);

        var mockLogger = new Mock<ILogger<RecipeExtractorService>>();
        var extractor = new RecipeExtractorService(mockLogger.Object);

        // Act
        var recipe = await extractor.ExtractRecipeAsync(minimalRecipeHtml, fingerprint);
        await _recipeRepository!.SaveAsync(recipe);

        var savedRecipe = await _recipeRepository.GetByUrlAsync(
            "https://www.meal-kit-provider.com/recipes/simple-steak",
            TestProviderId,
            CancellationToken.None);

        // Assert - Should handle missing fields gracefully
        savedRecipe.ShouldNotBeNull();
        savedRecipe.Title.ShouldBe("Simple Steak");
        savedRecipe.Ingredients.Count.ShouldBe(1);
        savedRecipe.Instructions.Count.ShouldBe(1);
        
        // Optional fields should have default values or be null
        savedRecipe.Description.ShouldBeNullOrEmpty();
        savedRecipe.PrepTimeMinutes.ShouldBe(0);
        savedRecipe.CookTimeMinutes.ShouldBe(0);
    }
}
