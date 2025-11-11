using EasyMeals.RecipeEngine.Application.Interfaces;
using EasyMeals.RecipeEngine.Domain.Entities;
using EasyMeals.RecipeEngine.Domain.Repositories;
using EasyMeals.RecipeEngine.Infrastructure.Fingerprinting;
using FluentAssertions;
using Moq;

namespace EasyMeals.RecipeEngine.Tests.Unit.Fingerprinting;

/// <summary>
///     Unit tests for RecipeFingerprintService implementation.
///     Tests fingerprint generation (SHA256 hash) and duplicate detection.
/// </summary>
public class RecipeFingerprintServiceTests
{
	private readonly IRecipeFingerprinter _fingerprintService;
	private readonly Mock<IRecipeFingerprintRepository> _repositoryMock;

	public RecipeFingerprintServiceTests()
	{
		_repositoryMock = new Mock<IRecipeFingerprintRepository>();
		_fingerprintService = new RecipeFingerprintService(_repositoryMock.Object);
	}

	[Fact(DisplayName = "Different descriptions produce different fingerprints")]
	public void GenerateFingerprint_DifferentDescriptions_ProducesDifferentHashes()
	{
		// Arrange
		const string url = "https://example.com/recipes/pasta";
		const string title = "Pasta Recipe";
		const string description1 = "Classic Italian dish";
		const string description2 = "Modern fusion recipe";

		// Act
		string fingerprint1 = _fingerprintService.GenerateFingerprint(url, title, description1);
		string fingerprint2 = _fingerprintService.GenerateFingerprint(url, title, description2);

		// Assert
		fingerprint1.Should().NotBe(fingerprint2, "Different descriptions should produce different hashes");
	}

	[Fact(DisplayName = "Different titles produce different fingerprints")]
	public void GenerateFingerprint_DifferentTitles_ProducesDifferentHashes()
	{
		// Arrange
		const string url = "https://example.com/recipes/pasta";
		const string title1 = "Pasta Carbonara";
		const string title2 = "Pasta Alfredo";
		const string description = "Delicious pasta";

		// Act
		string fingerprint1 = _fingerprintService.GenerateFingerprint(url, title1, description);
		string fingerprint2 = _fingerprintService.GenerateFingerprint(url, title2, description);

		// Assert
		fingerprint1.Should().NotBe(fingerprint2, "Different titles should produce different hashes");
	}

	[Fact(DisplayName = "Different URLs produce different fingerprints")]
	public void GenerateFingerprint_DifferentUrls_ProducesDifferentHashes()
	{
		// Arrange
		const string url1 = "https://example.com/recipes/pasta-carbonara";
		const string url2 = "https://example.com/recipes/pasta-alfredo";
		const string title = "Pasta Recipe";
		const string description = "Delicious pasta";

		// Act
		string fingerprint1 = _fingerprintService.GenerateFingerprint(url1, title, description);
		string fingerprint2 = _fingerprintService.GenerateFingerprint(url2, title, description);

		// Assert
		fingerprint1.Should().NotBe(fingerprint2, "Different URLs should produce different hashes");
	}

	[Fact(DisplayName = "Generate fingerprint handles empty description")]
	public void GenerateFingerprint_EmptyDescription_GeneratesValidHash()
	{
		// Arrange
		const string url = "https://example.com/recipes/pasta";
		const string title = "Test Recipe";
		const string description = "";

		// Act
		string fingerprint = _fingerprintService.GenerateFingerprint(url, title, description);

		// Assert
		fingerprint.Should().NotBeNullOrEmpty();
		fingerprint.Should().HaveLength(64);
		fingerprint.Should().MatchRegex("^[a-f0-9]{64}$");
	}

	[Fact(DisplayName = "Generate fingerprint normalizes description by taking first 200 chars, trimming, and lowercasing")]
	public void GenerateFingerprint_LongDescription_TakesFirst200Chars()
	{
		// Arrange
		const string url = "https://example.com/recipes/pasta";
		const string title = "Test Recipe";
		const string shortDescription = "Short description";
		var longDescription = new string('A', 300); // 300 characters
		var truncatedDescription = new string('a', 200); // First 200 chars, lowercased

		// Act
		string fingerprintShort = _fingerprintService.GenerateFingerprint(url, title, shortDescription);
		string fingerprintLong = _fingerprintService.GenerateFingerprint(url, title, longDescription);

		// Verify fingerprint uses truncated description
		string expectedFingerprint = _fingerprintService.GenerateFingerprint(url, title, truncatedDescription);

		// Assert
		fingerprintLong.Should().Be(expectedFingerprint, "Description should be truncated to 200 chars");
		fingerprintShort.Should().NotBe(fingerprintLong, "Short and truncated long descriptions should produce different hashes");
	}

	[Fact(DisplayName = "Same content produces same fingerprint hash")]
	public void GenerateFingerprint_SameContent_ProducesSameHash()
	{
		// Arrange
		const string url = "https://example.com/recipes/pasta-carbonara";
		const string title = "Pasta Carbonara";
		const string description = "Classic Italian pasta dish with eggs, cheese, and bacon";

		// Act - Generate fingerprint twice
		string fingerprint1 = _fingerprintService.GenerateFingerprint(url, title, description);
		string fingerprint2 = _fingerprintService.GenerateFingerprint(url, title, description);

		// Assert
		fingerprint1.Should().Be(fingerprint2, "Same content should always produce the same hash");
	}

	[Fact(DisplayName = "Generate fingerprint normalizes title by trimming and lowercasing")]
	public void GenerateFingerprint_TitleWithWhitespace_TrimsAndLowercases()
	{
		// Arrange
		const string url = "https://example.com/recipes/pasta";
		const string titleWithWhitespace = "  Pasta Carbonara  ";
		const string titleNormalized = "pasta carbonara";
		const string description = "Test description";

		// Act
		string fingerprint1 = _fingerprintService.GenerateFingerprint(url, titleWithWhitespace, description);
		string fingerprint2 = _fingerprintService.GenerateFingerprint(url, titleNormalized, description);

		// Assert
		fingerprint1.Should().Be(fingerprint2, "Title should be trimmed and lowercased");
	}

	[Fact(DisplayName = "Generate fingerprint normalizes URL to lowercase")]
	public void GenerateFingerprint_UppercaseUrl_NormalizesToLowercase()
	{
		// Arrange
		const string urlUppercase = "HTTPS://EXAMPLE.COM/RECIPES/PASTA";
		const string urlLowercase = "https://example.com/recipes/pasta";
		const string title = "Test Recipe";
		const string description = "Test description";

		// Act
		string fingerprint1 = _fingerprintService.GenerateFingerprint(urlUppercase, title, description);
		string fingerprint2 = _fingerprintService.GenerateFingerprint(urlLowercase, title, description);

		// Assert
		fingerprint1.Should().Be(fingerprint2, "URL should be normalized to lowercase");
	}

	[Fact(DisplayName = "Generate fingerprint removes query parameters from URL")]
	public void GenerateFingerprint_UrlWithQueryParams_RemovesQueryParams()
	{
		// Arrange
		const string urlWithParams = "https://example.com/recipes/pasta?ref=source&utm=campaign";
		const string urlWithoutParams = "https://example.com/recipes/pasta";
		const string title = "Test Recipe";
		const string description = "Test description";

		// Act
		string fingerprint1 = _fingerprintService.GenerateFingerprint(urlWithParams, title, description);
		string fingerprint2 = _fingerprintService.GenerateFingerprint(urlWithoutParams, title, description);

		// Assert
		fingerprint1.Should().Be(fingerprint2, "Query parameters should be removed from URL");
	}

	[Fact(DisplayName = "Generate fingerprint with valid inputs returns SHA256 hash")]
	public void GenerateFingerprint_ValidInputs_ReturnsSha256Hash()
	{
		// Arrange
		const string url = "https://example.com/recipes/pasta-carbonara";
		const string title = "Pasta Carbonara";
		const string description = "Classic Italian pasta dish with eggs, cheese, and bacon";

		// Act
		string fingerprint = _fingerprintService.GenerateFingerprint(url, title, description);

		// Assert
		fingerprint.Should().NotBeNullOrEmpty();
		fingerprint.Should().HaveLength(64); // SHA256 produces 64-character hex string
		fingerprint.Should().MatchRegex("^[a-f0-9]{64}$"); // Only lowercase hex characters
	}

	[Fact(DisplayName = "Is duplicate returns true when fingerprint exists in repository")]
	public async Task IsDuplicateAsync_ExistingFingerprint_ReturnsTrue()
	{
		// Arrange - Use proper 64-character SHA256 hash
		const string fingerprintHash = "abc123def456789012345678901234567890abcdef1234567890123456789012";

		_repositoryMock
			.Setup(repository => repository.ExistsByHashAsync(
				fingerprintHash,
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(true);

		// Act
		bool isDuplicate = await _fingerprintService.IsDuplicateAsync(fingerprintHash);

		// Assert
		isDuplicate.Should().BeTrue("Fingerprint exists in repository");
	}

	[Fact(DisplayName = "Is duplicate returns false when fingerprint does not exist")]
	public async Task IsDuplicateAsync_NewFingerprint_ReturnsFalse()
	{
		// Arrange - Use proper 64-character SHA256 hash
		const string fingerprintHash = "1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef";

		_repositoryMock
			.Setup(repository => repository.ExistsByHashAsync(
				fingerprintHash,
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(false);

		// Act
		bool isDuplicate = await _fingerprintService.IsDuplicateAsync(fingerprintHash);

		// Assert
		isDuplicate.Should().BeFalse("Fingerprint does not exist in repository");
	}

	[Fact(DisplayName = "Store fingerprint persists to repository")]
	public async Task StoreFingerprintAsync_ValidData_PersistsToRepository()
	{
		// Arrange - Use proper 64-character SHA256 hash
		const string fingerprintHash = "fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210";
		const string providerId = "provider_001";
		const string recipeUrl = "https://example.com/recipes/pasta";
		var recipeId = Guid.NewGuid();

		// Act
		await _fingerprintService.StoreFingerprintAsync(
			fingerprintHash,
			providerId,
			recipeUrl,
			recipeId);

		// Assert
		_repositoryMock.Verify(repository => repository.SaveAsync(
				It.Is<RecipeFingerprint>(fp =>
					fp.FingerprintHash == fingerprintHash &&
					fp.ProviderId == providerId &&
					fp.RecipeUrl == recipeUrl &&
					fp.RecipeId == recipeId),
				It.IsAny<CancellationToken>()),
			Times.Once());
	}
}