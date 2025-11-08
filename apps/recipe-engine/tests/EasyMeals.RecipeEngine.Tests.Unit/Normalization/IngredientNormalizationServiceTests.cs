using EasyMeals.RecipeEngine.Application.Interfaces;
using EasyMeals.RecipeEngine.Domain.Entities;
using EasyMeals.RecipeEngine.Domain.Events;
using EasyMeals.RecipeEngine.Domain.Repositories;
using EasyMeals.RecipeEngine.Infrastructure.Normalization;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ReceivedExtensions;

namespace EasyMeals.RecipeEngine.Tests.Unit.Normalization;

/// <summary>
///     Unit tests for IngredientNormalizationService.
///     Tests cover: mapped codes, unmapped codes, batch processing, caching, and logging.
/// </summary>
public class IngredientNormalizationServiceTests
{
    private readonly IIngredientMappingRepository _mockRepository;
    private readonly ILogger<IngredientNormalizationService> _mockLogger;
    private readonly IEventBus _mockEventBus;
    private readonly IIngredientNormalizer _sut;

    public IngredientNormalizationServiceTests()
    {
        _mockRepository = Substitute.For<IIngredientMappingRepository>();
        _mockLogger = Substitute.For<ILogger<IngredientNormalizationService>>();
        _mockEventBus = Substitute.For<IEventBus>();
        _sut = new IngredientNormalizationService(_mockRepository, _mockLogger, _mockEventBus);
    }

    #region NormalizeAsync Tests (T061 & T062)

    [Fact(DisplayName = "NormalizeAsync with mapped code returns canonical form")]
    public async Task NormalizeAsync_MappedCode_ReturnsCanonicalForm()
    {
        // Arrange
        const string providerId = "provider_001";
        const string providerCode = "HF-BROCCOLI-FROZEN-012";
        const string expectedCanonical = "broccoli, frozen";
        
        var mapping = IngredientMapping.Create(providerId, providerCode, expectedCanonical);
        _mockRepository.GetByCodeAsync(providerId, providerCode, Arg.Any<CancellationToken>())
            .Returns(mapping);

        // Act
        var result = await _sut.NormalizeAsync(providerId, providerCode);

        // Assert
        result.Should().Be(expectedCanonical);
        await _mockRepository.Received(1).GetByCodeAsync(providerId, providerCode, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "NormalizeAsync with unmapped code returns null and logs warning")]
    public async Task NormalizeAsync_UnmappedCode_ReturnsNullAndLogsWarning()
    {
        // Arrange
        const string providerId = "provider_001";
        const string providerCode = "UNKNOWN-INGREDIENT-999";
        
        _mockRepository.GetByCodeAsync(providerId, providerCode, Arg.Any<CancellationToken>())
            .Returns((IngredientMapping?)null);

        // Act
        var result = await _sut.NormalizeAsync(providerId, providerCode);

        // Assert
        result.Should().BeNull();
        await _mockRepository.Received(1).GetByCodeAsync(providerId, providerCode, Arg.Any<CancellationToken>());
        
        // Verify warning was logged (checking logger was called)
        _mockLogger.ReceivedWithAnyArgs(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact(DisplayName = "NormalizeAsync with null provider ID throws ArgumentException")]
    public async Task NormalizeAsync_NullProviderId_ThrowsArgumentException()
    {
        // Arrange
        string? providerId = null;
        const string providerCode = "SOME-CODE";

        // Act
        Func<Task> act = async () => await _sut.NormalizeAsync(providerId!, providerCode);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("providerId");
    }

    [Fact(DisplayName = "NormalizeAsync with null provider code throws ArgumentException")]
    public async Task NormalizeAsync_NullProviderCode_ThrowsArgumentException()
    {
        // Arrange
        const string providerId = "provider_001";
        string? providerCode = null;

        // Act
        Func<Task> act = async () => await _sut.NormalizeAsync(providerId, providerCode!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("providerCode");
    }

    [Fact(DisplayName = "NormalizeAsync uses cache for repeated lookups")]
    public async Task NormalizeAsync_RepeatedLookup_UsesCache()
    {
        // Arrange
        const string providerId = "provider_001";
        const string providerCode = "HF-GARLIC-012";
        const string canonicalForm = "garlic";
        
        var mapping = IngredientMapping.Create(providerId, providerCode, canonicalForm);
        _mockRepository.GetByCodeAsync(providerId, providerCode, Arg.Any<CancellationToken>())
            .Returns(mapping);

        // Act - Call twice
        var result1 = await _sut.NormalizeAsync(providerId, providerCode);
        var result2 = await _sut.NormalizeAsync(providerId, providerCode);

        // Assert
        result1.Should().Be(canonicalForm);
        result2.Should().Be(canonicalForm);
        
        // Repository should only be called once (second call uses cache)
        await _mockRepository.Received(1).GetByCodeAsync(providerId, providerCode, Arg.Any<CancellationToken>());
    }

    #endregion

    #region NormalizeBatchAsync Tests (T063)

    [Fact(DisplayName = "NormalizeBatchAsync with multiple codes returns dictionary with mapped and unmapped")]
    public async Task NormalizeBatchAsync_MultipleCodes_ReturnsDictionaryWithMappedAndUnmapped()
    {
        // Arrange
        const string providerId = "provider_001";
        var providerCodes = new[] { "HF-BROCCOLI-012", "HF-GARLIC-015", "UNKNOWN-999" };
        
        var broccoliMapping = IngredientMapping.Create(providerId, "HF-BROCCOLI-012", "broccoli");
        var garlicMapping = IngredientMapping.Create(providerId, "HF-GARLIC-015", "garlic");
        
        _mockRepository.GetByCodeAsync(providerId, "HF-BROCCOLI-012", Arg.Any<CancellationToken>())
            .Returns(broccoliMapping);
        _mockRepository.GetByCodeAsync(providerId, "HF-GARLIC-015", Arg.Any<CancellationToken>())
            .Returns(garlicMapping);
        _mockRepository.GetByCodeAsync(providerId, "UNKNOWN-999", Arg.Any<CancellationToken>())
            .Returns((IngredientMapping?)null);

        // Act
        var result = await _sut.NormalizeBatchAsync(providerId, providerCodes);

        // Assert
        result.Should().HaveCount(3);
        result["HF-BROCCOLI-012"].Should().Be("broccoli");
        result["HF-GARLIC-015"].Should().Be("garlic");
        result["UNKNOWN-999"].Should().BeNull();
    }

    [Fact(DisplayName = "NormalizeBatchAsync with empty codes returns empty dictionary")]
    public async Task NormalizeBatchAsync_EmptyCodes_ReturnsEmptyDictionary()
    {
        // Arrange
        const string providerId = "provider_001";
        var providerCodes = Array.Empty<string>();

        // Act
        var result = await _sut.NormalizeBatchAsync(providerId, providerCodes);

        // Assert
        result.Should().BeEmpty();
        await _mockRepository.DidNotReceiveWithAnyArgs().GetByCodeAsync(default!, default!, default);
    }

    [Fact(DisplayName = "NormalizeBatchAsync logs unmapped ingredients")]
    public async Task NormalizeBatchAsync_UnmappedIngredients_LogsWarnings()
    {
        // Arrange
        const string providerId = "provider_001";
        var providerCodes = new[] { "UNKNOWN-1", "UNKNOWN-2" };
        
        _mockRepository.GetByCodeAsync(providerId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((IngredientMapping?)null);

        // Act
        await _sut.NormalizeBatchAsync(providerId, providerCodes);

        // Assert - Verify at least 2 warnings were logged for unmapped codes
        _mockLogger.Received(2).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact(DisplayName = "NormalizeBatchAsync with duplicate codes processes only once")]
    public async Task NormalizeBatchAsync_DuplicateCodes_ProcessesOnlyOnce()
    {
        // Arrange
        const string providerId = "provider_001";
        var providerCodes = new[] { "HF-BROCCOLI-012", "HF-BROCCOLI-012", "HF-BROCCOLI-012" };
        
        var mapping = IngredientMapping.Create(providerId, "HF-BROCCOLI-012", "broccoli");
        _mockRepository.GetByCodeAsync(providerId, "HF-BROCCOLI-012", Arg.Any<CancellationToken>())
            .Returns(mapping);

        // Act
        var result = await _sut.NormalizeBatchAsync(providerId, providerCodes);

        // Assert
        result.Should().HaveCount(1);
        result["HF-BROCCOLI-012"].Should().Be("broccoli");
        
        // Should only query repository once for the unique code
        await _mockRepository.Received(1).GetByCodeAsync(providerId, "HF-BROCCOLI-012", Arg.Any<CancellationToken>());
    }

    #endregion

    #region Event Publishing Tests

    [Fact(DisplayName = "NormalizeAsync publishes IngredientMappingMissingEvent for unmapped ingredient")]
    public async Task NormalizeAsync_UnmappedIngredient_PublishesEvent()
    {
        // Arrange
        const string providerId = "provider_001";
        const string providerCode = "UNKNOWN-INGREDIENT";
        
        _mockRepository.GetByCodeAsync(providerId, providerCode, Arg.Any<CancellationToken>())
            .Returns((IngredientMapping?)null);

        // Act
        await _sut.NormalizeAsync(providerId, providerCode);

        // Assert - Event is published synchronously
        _mockEventBus.Received(1).Publish(
            Arg.Is<IngredientMappingMissingEvent>(e => 
                e.ProviderId == providerId && 
                e.ProviderCode == providerCode));
    }

    #endregion
}
