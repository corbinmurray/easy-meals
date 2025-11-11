using EasyMeals.RecipeEngine.Application.Interfaces;
using EasyMeals.RecipeEngine.Domain.Entities;
using EasyMeals.RecipeEngine.Domain.Events;
using EasyMeals.RecipeEngine.Domain.Repositories;
using EasyMeals.RecipeEngine.Infrastructure.Normalization;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace EasyMeals.RecipeEngine.Tests.Unit.Normalization;

/// <summary>
///     Unit tests for IngredientNormalizationService.
///     Tests cover: mapped codes, unmapped codes, batch processing, caching, and logging.
/// </summary>
public class IngredientNormalizationServiceTests
{
	private readonly Mock<IEventBus> _mockEventBus;
	private readonly Mock<ILogger<IngredientNormalizationService>> _mockLogger;
	private readonly Mock<IIngredientMappingRepository> _mockRepository;
	private readonly IIngredientNormalizer _sut;

	public IngredientNormalizationServiceTests()
	{
		_mockRepository = new Mock<IIngredientMappingRepository>();
		_mockLogger = new Mock<ILogger<IngredientNormalizationService>>();
		_mockEventBus = new Mock<IEventBus>();
		_sut = new IngredientNormalizationService(_mockRepository.Object, _mockLogger.Object, _mockEventBus.Object);
	}

	[Fact(DisplayName = "NormalizeAsync with mapped code returns canonical form")]
	public async Task NormalizeAsync_MappedCode_ReturnsCanonicalForm()
	{
		// Arrange
		const string providerId = "provider_001";
		const string providerCode = "HF-BROCCOLI-FROZEN-012";
		const string expectedCanonical = "broccoli, frozen";

		var mapping = IngredientMapping.Create(providerId, providerCode, expectedCanonical);
		_mockRepository
			.Setup(r => r.GetByCodeAsync(providerId, providerCode, It.IsAny<CancellationToken>()))
			.ReturnsAsync(mapping);

		// Act
		string? result = await _sut.NormalizeAsync(providerId, providerCode);

		// Assert
		result.Should().Be(expectedCanonical);
		_mockRepository.Verify(
			r => r.GetByCodeAsync(providerId, providerCode, It.IsAny<CancellationToken>()),
			Times.Once);
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

	[Fact(DisplayName = "NormalizeAsync uses cache for repeated lookups")]
	public async Task NormalizeAsync_RepeatedLookup_UsesCache()
	{
		// Arrange
		const string providerId = "provider_001";
		const string providerCode = "HF-GARLIC-012";
		const string canonicalForm = "garlic";

		var mapping = IngredientMapping.Create(providerId, providerCode, canonicalForm);
		_mockRepository
			.Setup(r => r.GetByCodeAsync(providerId, providerCode, It.IsAny<CancellationToken>()))
			.ReturnsAsync(mapping);

		// Act - Call twice
		string? result1 = await _sut.NormalizeAsync(providerId, providerCode);
		string? result2 = await _sut.NormalizeAsync(providerId, providerCode);

		// Assert
		result1.Should().Be(canonicalForm);
		result2.Should().Be(canonicalForm);

		_mockRepository.Verify(
			r => r.GetByCodeAsync(providerId, providerCode, It.IsAny<CancellationToken>()),
			Times.Once);
	}

	[Fact(DisplayName = "NormalizeAsync with unmapped code returns null and logs warning")]
	public async Task NormalizeAsync_UnmappedCode_ReturnsNullAndLogsWarning()
	{
		// Arrange
		const string providerId = "provider_001";
		const string providerCode = "UNKNOWN-INGREDIENT-999";

		_mockRepository
			.Setup(r => r.GetByCodeAsync(providerId, providerCode, It.IsAny<CancellationToken>()))
			.ReturnsAsync((IngredientMapping?)null);

		// Act
		string? result = await _sut.NormalizeAsync(providerId, providerCode);

		// Assert
		result.Should().BeNull();
		_mockRepository.Verify(
			r => r.GetByCodeAsync(providerId, providerCode, It.IsAny<CancellationToken>()),
			Times.Once);

		_mockLogger.Verify(
			l => l.Log(
				LogLevel.Warning,
				It.IsAny<EventId>(),
				It.IsAny<It.IsAnyType>(),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);
	}

	[Fact(DisplayName = "NormalizeAsync publishes IngredientMappingMissingEvent for unmapped ingredient")]
	public async Task NormalizeAsync_UnmappedIngredient_PublishesEvent()
	{
		// Arrange
		const string providerId = "provider_001";
		const string providerCode = "UNKNOWN-INGREDIENT";

		_mockRepository
			.Setup(r => r.GetByCodeAsync(providerId, providerCode, It.IsAny<CancellationToken>()))
			.ReturnsAsync((IngredientMapping?)null);

		// Act
		await _sut.NormalizeAsync(providerId, providerCode);

		// Assert - Event is published synchronously
		_mockEventBus.Verify(
			eb => eb.Publish(It.Is<IngredientMappingMissingEvent>(e =>
				e.ProviderId == providerId &&
				e.ProviderCode == providerCode)),
			Times.Once);
	}

	[Fact(DisplayName = "NormalizeBatchAsync with duplicate codes processes only once")]
	public async Task NormalizeBatchAsync_DuplicateCodes_ProcessesOnlyOnce()
	{
		// Arrange
		const string providerId = "provider_001";
		var providerCodes = new[] { "HF-BROCCOLI-012", "HF-BROCCOLI-012", "HF-BROCCOLI-012" };

		var mapping = IngredientMapping.Create(providerId, "HF-BROCCOLI-012", "broccoli");
		_mockRepository
			.Setup(r => r.GetByCodeAsync(providerId, "HF-BROCCOLI-012", It.IsAny<CancellationToken>()))
			.ReturnsAsync(mapping);

		// Act
		IDictionary<string, string?> result = await _sut.NormalizeBatchAsync(providerId, providerCodes);

		// Assert
		result.Should().HaveCount(1);
		result["HF-BROCCOLI-012"].Should().Be("broccoli");

		_mockRepository.Verify(
			r => r.GetByCodeAsync(providerId, "HF-BROCCOLI-012", It.IsAny<CancellationToken>()),
			Times.Once);
	}

	[Fact(DisplayName = "NormalizeBatchAsync with empty codes returns empty dictionary")]
	public async Task NormalizeBatchAsync_EmptyCodes_ReturnsEmptyDictionary()
	{
		// Arrange
		const string providerId = "provider_001";
		string[] providerCodes = Array.Empty<string>();

		// Act
		IDictionary<string, string?> result = await _sut.NormalizeBatchAsync(providerId, providerCodes);

		// Assert
		result.Should().BeEmpty();
		_mockRepository.Verify(
			r => r.GetByCodeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
			Times.Never);
	}

	[Fact(DisplayName = "NormalizeBatchAsync with multiple codes returns dictionary with mapped and unmapped")]
	public async Task NormalizeBatchAsync_MultipleCodes_ReturnsDictionaryWithMappedAndUnmapped()
	{
		// Arrange
		const string providerId = "provider_001";
		var providerCodes = new[] { "HF-BROCCOLI-012", "HF-GARLIC-015", "UNKNOWN-999" };

		var broccoliMapping = IngredientMapping.Create(providerId, "HF-BROCCOLI-012", "broccoli");
		var garlicMapping = IngredientMapping.Create(providerId, "HF-GARLIC-015", "garlic");

		_mockRepository
			.Setup(r => r.GetByCodeAsync(providerId, "HF-BROCCOLI-012", It.IsAny<CancellationToken>()))
			.ReturnsAsync(broccoliMapping);
		_mockRepository
			.Setup(r => r.GetByCodeAsync(providerId, "HF-GARLIC-015", It.IsAny<CancellationToken>()))
			.ReturnsAsync(garlicMapping);
		_mockRepository
			.Setup(r => r.GetByCodeAsync(providerId, "UNKNOWN-999", It.IsAny<CancellationToken>()))
			.ReturnsAsync((IngredientMapping?)null);

		// Act
		IDictionary<string, string?> result = await _sut.NormalizeBatchAsync(providerId, providerCodes);

		// Assert
		result.Should().HaveCount(3);
		result["HF-BROCCOLI-012"].Should().Be("broccoli");
		result["HF-GARLIC-015"].Should().Be("garlic");
		result["UNKNOWN-999"].Should().BeNull();
	}

	[Fact(DisplayName = "NormalizeBatchAsync logs unmapped ingredients")]
	public async Task NormalizeBatchAsync_UnmappedIngredients_LogsWarnings()
	{
		// Arrange
		const string providerId = "provider_001";
		var providerCodes = new[] { "UNKNOWN-1", "UNKNOWN-2" };

		_mockRepository
			.Setup(r => r.GetByCodeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((IngredientMapping?)null);

		// Act
		await _sut.NormalizeBatchAsync(providerId, providerCodes);

		// Assert - Verify at least 2 warnings were logged for unmapped codes
		_mockLogger.Verify(
			l => l.Log(
				LogLevel.Warning,
				It.IsAny<EventId>(),
				It.IsAny<It.IsAnyType>(),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.AtLeast(2));
	}
}