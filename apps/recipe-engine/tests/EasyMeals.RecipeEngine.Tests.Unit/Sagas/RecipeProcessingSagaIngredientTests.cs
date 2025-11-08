using EasyMeals.RecipeEngine.Application.Interfaces;
using EasyMeals.RecipeEngine.Application.Sagas;
using EasyMeals.RecipeEngine.Domain.Events;
using EasyMeals.RecipeEngine.Domain.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace EasyMeals.RecipeEngine.Tests.Unit.Sagas;

/// <summary>
///     Unit tests for RecipeProcessingSaga ingredient normalization (Phase 4 - T069).
///     Tests the ProcessIngredientsAsync method that demonstrates ingredient normalization integration.
/// </summary>
public class RecipeProcessingSagaIngredientTests
{
    private readonly Mock<ILogger<RecipeProcessingSaga>> _mockLogger;
    private readonly Mock<ISagaStateRepository> _mockSagaStateRepository;
    private readonly Mock<IIngredientNormalizer> _mockIngredientNormalizer;
    private readonly Mock<IEventBus> _mockEventBus;
    private readonly RecipeProcessingSaga _sut;

    public RecipeProcessingSagaIngredientTests()
    {
        _mockLogger = new Mock<ILogger<RecipeProcessingSaga>>();
        _mockSagaStateRepository = new Mock<ISagaStateRepository>();
        _mockIngredientNormalizer = new Mock<IIngredientNormalizer>();
        _mockEventBus = new Mock<IEventBus>();
        
        _sut = new RecipeProcessingSaga(
            _mockLogger.Object,
            _mockSagaStateRepository.Object,
            _mockIngredientNormalizer.Object,
            _mockEventBus.Object);
    }

    [Fact(DisplayName = "ProcessIngredientsAsync with all mapped ingredients creates IngredientReferences with canonical forms")]
    public async Task ProcessIngredientsAsync_AllMappedIngredients_CreatesIngredientReferencesWithCanonicalForms()
    {
        // Arrange
        const string providerId = "provider_001";
        const string recipeUrl = "https://example.com/recipe/123";
        var rawCodes = new[] { "HF-BROCCOLI-001", "HF-GARLIC-002", "HF-OLIVE-OIL-003" };
        
        var normalizedMap = new Dictionary<string, string?>
        {
            ["HF-BROCCOLI-001"] = "broccoli",
            ["HF-GARLIC-002"] = "garlic",
            ["HF-OLIVE-OIL-003"] = "olive oil"
        };
        
        _mockIngredientNormalizer
            .Setup(n => n.NormalizeBatchAsync(providerId, rawCodes, It.IsAny<CancellationToken>()))
            .ReturnsAsync(normalizedMap);

        // Act
        var result = await _sut.ProcessIngredientsAsync(providerId, recipeUrl, rawCodes);

        // Assert
        result.Should().HaveCount(3);
        result[0].ProviderCode.Should().Be("HF-BROCCOLI-001");
        result[0].CanonicalForm.Should().Be("broccoli");
        result[0].DisplayOrder.Should().Be(1);
        
        result[1].ProviderCode.Should().Be("HF-GARLIC-002");
        result[1].CanonicalForm.Should().Be("garlic");
        result[1].DisplayOrder.Should().Be(2);
        
        result[2].ProviderCode.Should().Be("HF-OLIVE-OIL-003");
        result[2].CanonicalForm.Should().Be("olive oil");
        result[2].DisplayOrder.Should().Be(3);
        
        // Verify no events were published for mapped ingredients
        _mockEventBus.Verify(
            eb => eb.Publish(It.IsAny<IngredientMappingMissingEvent>()),
            Times.Never);
    }

    [Fact(DisplayName = "ProcessIngredientsAsync with unmapped ingredients publishes IngredientMappingMissingEvent")]
    public async Task ProcessIngredientsAsync_UnmappedIngredients_PublishesIngredientMappingMissingEvent()
    {
        // Arrange
        const string providerId = "provider_001";
        const string recipeUrl = "https://example.com/recipe/123";
        var rawCodes = new[] { "HF-BROCCOLI-001", "UNKNOWN-999" };
        
        var normalizedMap = new Dictionary<string, string?>
        {
            ["HF-BROCCOLI-001"] = "broccoli",
            ["UNKNOWN-999"] = null // Unmapped
        };
        
        _mockIngredientNormalizer
            .Setup(n => n.NormalizeBatchAsync(providerId, rawCodes, It.IsAny<CancellationToken>()))
            .ReturnsAsync(normalizedMap);

        // Act
        var result = await _sut.ProcessIngredientsAsync(providerId, recipeUrl, rawCodes);

        // Assert
        result.Should().HaveCount(2);
        result[0].CanonicalForm.Should().Be("broccoli");
        result[1].CanonicalForm.Should().BeNull(); // Unmapped stored as null
        
        // Verify event was published for unmapped ingredient
        _mockEventBus.Verify(
            eb => eb.Publish(It.Is<IngredientMappingMissingEvent>(e =>
                e.ProviderId == providerId &&
                e.ProviderCode == "UNKNOWN-999" &&
                e.RecipeUrl == recipeUrl)),
            Times.Once);
    }

    [Fact(DisplayName = "ProcessIngredientsAsync with mixed mapped/unmapped ingredients continues processing")]
    public async Task ProcessIngredientsAsync_MixedMappedUnmappedIngredients_ContinuesProcessing()
    {
        // Arrange
        const string providerId = "provider_001";
        const string recipeUrl = "https://example.com/recipe/123";
        var rawCodes = new[] { "HF-BROCCOLI-001", "UNKNOWN-1", "HF-GARLIC-002", "UNKNOWN-2" };
        
        var normalizedMap = new Dictionary<string, string?>
        {
            ["HF-BROCCOLI-001"] = "broccoli",
            ["UNKNOWN-1"] = null,
            ["HF-GARLIC-002"] = "garlic",
            ["UNKNOWN-2"] = null
        };
        
        _mockIngredientNormalizer
            .Setup(n => n.NormalizeBatchAsync(providerId, rawCodes, It.IsAny<CancellationToken>()))
            .ReturnsAsync(normalizedMap);

        // Act
        var result = await _sut.ProcessIngredientsAsync(providerId, recipeUrl, rawCodes);

        // Assert - All 4 ingredients processed despite 2 being unmapped
        result.Should().HaveCount(4);
        result[0].CanonicalForm.Should().Be("broccoli");
        result[1].CanonicalForm.Should().BeNull();
        result[2].CanonicalForm.Should().Be("garlic");
        result[3].CanonicalForm.Should().BeNull();
        
        // Verify events published for both unmapped ingredients
        _mockEventBus.Verify(
            eb => eb.Publish(It.Is<IngredientMappingMissingEvent>(e => e.ProviderCode == "UNKNOWN-1")),
            Times.Once);
        _mockEventBus.Verify(
            eb => eb.Publish(It.Is<IngredientMappingMissingEvent>(e => e.ProviderCode == "UNKNOWN-2")),
            Times.Once);
    }

    [Fact(DisplayName = "ProcessIngredientsAsync with empty ingredient list returns empty result")]
    public async Task ProcessIngredientsAsync_EmptyIngredientList_ReturnsEmptyResult()
    {
        // Arrange
        const string providerId = "provider_001";
        const string recipeUrl = "https://example.com/recipe/123";
        var rawCodes = Array.Empty<string>();
        
        _mockIngredientNormalizer
            .Setup(n => n.NormalizeBatchAsync(providerId, rawCodes, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string?>());

        // Act
        var result = await _sut.ProcessIngredientsAsync(providerId, recipeUrl, rawCodes);

        // Assert
        result.Should().BeEmpty();
        _mockEventBus.Verify(
            eb => eb.Publish(It.IsAny<IngredientMappingMissingEvent>()),
            Times.Never);
    }

    [Fact(DisplayName = "ProcessIngredientsAsync maintains display order for ingredients")]
    public async Task ProcessIngredientsAsync_MultipleIngredients_MaintainsDisplayOrder()
    {
        // Arrange
        const string providerId = "provider_001";
        const string recipeUrl = "https://example.com/recipe/123";
        var rawCodes = new[] { "CODE-1", "CODE-2", "CODE-3", "CODE-4", "CODE-5" };
        
        var normalizedMap = new Dictionary<string, string?>
        {
            ["CODE-1"] = "ingredient_1",
            ["CODE-2"] = "ingredient_2",
            ["CODE-3"] = "ingredient_3",
            ["CODE-4"] = "ingredient_4",
            ["CODE-5"] = "ingredient_5"
        };
        
        _mockIngredientNormalizer
            .Setup(n => n.NormalizeBatchAsync(providerId, rawCodes, It.IsAny<CancellationToken>()))
            .ReturnsAsync(normalizedMap);

        // Act
        var result = await _sut.ProcessIngredientsAsync(providerId, recipeUrl, rawCodes);

        // Assert
        result.Should().HaveCount(5);
        for (int i = 0; i < 5; i++)
        {
            result[i].DisplayOrder.Should().Be(i + 1);
            result[i].ProviderCode.Should().Be($"CODE-{i + 1}");
        }
    }

    [Fact(DisplayName = "ProcessIngredientsAsync calls NormalizeBatchAsync with correct parameters")]
    public async Task ProcessIngredientsAsync_CallsNormalizeBatchAsync_WithCorrectParameters()
    {
        // Arrange
        const string providerId = "provider_001";
        const string recipeUrl = "https://example.com/recipe/123";
        var rawCodes = new[] { "CODE-1", "CODE-2" };
        
        _mockIngredientNormalizer
            .Setup(n => n.NormalizeBatchAsync(providerId, rawCodes, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string?>
            {
                ["CODE-1"] = "canonical_1",
                ["CODE-2"] = "canonical_2"
            });

        // Act
        await _sut.ProcessIngredientsAsync(providerId, recipeUrl, rawCodes);

        // Assert
        _mockIngredientNormalizer.Verify(
            n => n.NormalizeBatchAsync(
                providerId,
                rawCodes,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
