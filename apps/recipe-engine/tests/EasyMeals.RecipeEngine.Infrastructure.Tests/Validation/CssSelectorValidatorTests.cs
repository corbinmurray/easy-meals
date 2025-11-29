using EasyMeals.Domain.ProviderConfiguration;
using FluentAssertions;
using Xunit;

namespace EasyMeals.RecipeEngine.Infrastructure.Tests.Validation;

/// <summary>
/// Unit tests for <see cref="CssSelectorValidator"/> CSS selector validation.
/// </summary>
public class CssSelectorValidatorTests
{
    #region IsValid() - Valid Selectors

    [Theory]
    [InlineData(".class")]
    [InlineData("#id")]
    [InlineData("div")]
    [InlineData("div.class")]
    [InlineData("div#id")]
    [InlineData("div.class#id")]
    [InlineData("div > p")]
    [InlineData("div p")]
    [InlineData("div + p")]
    [InlineData("div ~ p")]
    [InlineData("[data-attribute]")]
    [InlineData("[data-attribute='value']")]
    [InlineData("[data-attribute~='value']")]
    [InlineData("[data-attribute|='value']")]
    [InlineData("[data-attribute^='value']")]
    [InlineData("[data-attribute$='value']")]
    [InlineData("[data-attribute*='value']")]
    [InlineData("div:first-child")]
    [InlineData("div:last-child")]
    [InlineData("div:nth-child(2)")]
    [InlineData("div:nth-child(odd)")]
    [InlineData("div:nth-child(even)")]
    [InlineData("div:nth-child(2n+1)")]
    [InlineData("div:not(.excluded)")]
    [InlineData("div:has(> p)")]
    [InlineData("ul li")]
    [InlineData("ul > li")]
    [InlineData("ul.ingredients > li")]
    [InlineData("div.recipe-card h1.title")]
    [InlineData("article[data-recipe-id]")]
    [InlineData(".recipe-title, .recipe-name")]  // Multiple selectors
    public void IsValid_WithValidSelector_ReturnsTrue(string selector)
    {
        // Act
        var result = CssSelectorValidator.IsValid(selector);

        // Assert
        result.Should().BeTrue($"selector '{selector}' should be valid");
    }

    #endregion

    #region IsValid() - Invalid Selectors

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    public void IsValid_WithNullOrWhitespace_ReturnsFalse(string? selector)
    {
        // Act
        var result = CssSelectorValidator.IsValid(selector!);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("[")]
    [InlineData("]")]
    [InlineData("[invalid[")]
    [InlineData("[[]]")]
    [InlineData("div[")]
    [InlineData("div]")]
    public void IsValid_WithUnbalancedBrackets_ReturnsFalse(string selector)
    {
        // Act
        var result = CssSelectorValidator.IsValid(selector);

        // Assert
        result.Should().BeFalse($"selector '{selector}' has unbalanced brackets");
    }

    [Theory]
    [InlineData("(")]
    [InlineData(")")]
    [InlineData("div(")]
    [InlineData(":nth-child(")]
    [InlineData(":nth-child(2")]
    public void IsValid_WithUnbalancedParentheses_ReturnsFalse(string selector)
    {
        // Act
        var result = CssSelectorValidator.IsValid(selector);

        // Assert
        result.Should().BeFalse($"selector '{selector}' has unbalanced parentheses");
    }

    [Theory]
    [InlineData(">>>")]
    public void IsValid_WithPureCombinatorOnly_ReturnsFalse(string selector)
    {
        // Act
        var result = CssSelectorValidator.IsValid(selector);

        // Assert
        // AngleSharp rejects standalone combinators without element context
        result.Should().BeFalse($"selector '{selector}' is a pure combinator without context");
    }

    // NOTE: AngleSharp is lenient with non-standard combinators like "div >> p" and "div >>> p"
    // These are technically invalid CSS but AngleSharp parses them without throwing.
    // Since we rely on AngleSharp for runtime execution, we accept what AngleSharp accepts.

    [Fact]
    public void IsValid_WithSelectorExceedingMaxLength_ReturnsFalse()
    {
        // Arrange - Create a selector longer than 1024 characters
        var longSelector = new string('a', 1025);

        // Act
        var result = CssSelectorValidator.IsValid(longSelector);

        // Assert
        result.Should().BeFalse("selector exceeds maximum length");
    }

    [Fact]
    public void IsValid_WithSelectorAtMaxLength_ReturnsTrue()
    {
        // Arrange - Create a valid selector exactly 1024 characters
        var maxLengthSelector = "." + new string('a', 1023);

        // Act
        var result = CssSelectorValidator.IsValid(maxLengthSelector);

        // Assert
        result.Should().BeTrue("selector at maximum length should be valid");
    }

    #endregion

    #region Validate() - Success Cases

    [Fact]
    public void Validate_WithValidSelector_ReturnsSuccessResult()
    {
        // Arrange
        const string validSelector = ".recipe-title";

        // Act
        var result = CssSelectorValidator.Validate(validSelector);

        // Assert
        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void Validate_WithComplexValidSelector_ReturnsSuccessResult()
    {
        // Arrange
        const string complexSelector = "div.recipe-card:not(.hidden) > article[data-id] h1.title:first-child";

        // Act
        var result = CssSelectorValidator.Validate(complexSelector);

        // Assert
        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    #endregion

    #region Validate() - Failure Cases

    [Theory]
    [InlineData(null, "null or whitespace")]
    [InlineData("", "null or whitespace")]
    [InlineData("   ", "null or whitespace")]
    public void Validate_WithNullOrWhitespace_ReturnsFailureWithMessage(string? selector, string expectedContains)
    {
        // Act
        var result = CssSelectorValidator.Validate(selector!);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain(expectedContains);
    }

    [Fact]
    public void Validate_WithSelectorExceedingMaxLength_ReturnsFailureWithMessage()
    {
        // Arrange
        var longSelector = new string('a', 1025);

        // Act
        var result = CssSelectorValidator.Validate(longSelector);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("maximum length");
    }

    [Fact]
    public void Validate_WithInvalidSyntax_ReturnsFailureWithMessage()
    {
        // Arrange
        const string invalidSelector = "[invalid[";

        // Act
        var result = CssSelectorValidator.Validate(invalidSelector);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Real-World Recipe Selectors

    [Theory]
    [InlineData(".recipe-title")]
    [InlineData("h1[itemprop='name']")]
    [InlineData(".recipe-description p")]
    [InlineData("ul.ingredients-list > li")]
    [InlineData("ol.instructions-list > li")]
    [InlineData(".prep-time-value")]
    [InlineData(".cook-time-value")]
    [InlineData("span[itemprop='totalTime']")]
    [InlineData(".yield-value")]
    [InlineData("img.recipe-image")]
    [InlineData(".author-name")]
    [InlineData("[itemprop='author'] [itemprop='name']")]
    [InlineData(".cuisine-tag")]
    [InlineData(".difficulty-level")]
    [InlineData(".nutrition-info")]
    public void IsValid_WithRealWorldRecipeSelectors_ReturnsTrue(string selector)
    {
        // Act
        var result = CssSelectorValidator.IsValid(selector);

        // Assert
        result.Should().BeTrue($"real-world recipe selector '{selector}' should be valid");
    }

    #endregion
}
