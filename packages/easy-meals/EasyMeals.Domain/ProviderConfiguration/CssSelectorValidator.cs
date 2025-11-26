using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace EasyMeals.Domain.ProviderConfiguration;

/// <summary>
/// Validates CSS selectors using AngleSharp's CSS selector parser.
/// </summary>
public static class CssSelectorValidator
{
    private static readonly HtmlParser Parser = new();
    private static readonly IDocument EmptyDocument = Parser.ParseDocument("<html><body></body></html>");

    /// <summary>
    /// Validates whether a CSS selector string is syntactically valid.
    /// </summary>
    /// <param name="selector">The CSS selector to validate.</param>
    /// <returns><c>true</c> if the selector is valid; otherwise, <c>false</c>.</returns>
    public static bool IsValid(string selector)
    {
        if (string.IsNullOrWhiteSpace(selector))
            return false;

        if (selector.Length > 1024)
            return false;

        try
        {
            // AngleSharp will throw if the selector is invalid
            EmptyDocument.QuerySelectorAll(selector);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Validates a CSS selector and returns details about any issues.
    /// </summary>
    /// <param name="selector">The CSS selector to validate.</param>
    /// <returns>A validation result with details.</returns>
    public static CssSelectorValidationResult Validate(string selector)
    {
        if (string.IsNullOrWhiteSpace(selector))
            return CssSelectorValidationResult.Failure("Selector cannot be null or whitespace.");

        if (selector.Length > 1024)
            return CssSelectorValidationResult.Failure("Selector exceeds maximum length of 1024 characters.");

        try
        {
            EmptyDocument.QuerySelectorAll(selector);
            return CssSelectorValidationResult.Success();
        }
        catch (DomException ex)
        {
            return CssSelectorValidationResult.Failure($"Invalid CSS selector syntax: {ex.Message}");
        }
        catch (Exception ex)
        {
            return CssSelectorValidationResult.Failure($"Selector validation failed: {ex.Message}");
        }
    }
}

/// <summary>
/// Result of a CSS selector validation.
/// </summary>
public sealed record CssSelectorValidationResult
{
    /// <summary>Whether the selector is valid.</summary>
    public bool IsValid { get; private init; }

    /// <summary>Error message if validation failed.</summary>
    public string? ErrorMessage { get; private init; }

    private CssSelectorValidationResult() { }

    /// <summary>Creates a successful validation result.</summary>
    public static CssSelectorValidationResult Success() => new() { IsValid = true };

    /// <summary>Creates a failed validation result with an error message.</summary>
    public static CssSelectorValidationResult Failure(string errorMessage) =>
        new() { IsValid = false, ErrorMessage = errorMessage };
}

