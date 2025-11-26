namespace EasyMeals.Domain.ProviderConfiguration;

public static class CssSelectorValidator
{
    // Lightweight validation for Phase 1: ensures selector is non-empty and looks like a selector.
    // Full CSS parsing via AngleSharp.Css will be implemented as part of Phase 2 validation tests
    // where a pre-release package or correct stable package can be pinned.
    public static bool IsValid(string selector)
    {
        if (string.IsNullOrWhiteSpace(selector)) return false;

        // Reject obvious HTML fragments or control chars
        if (selector.Contains('<') || selector.Contains('>') || selector.IndexOf('\n') >= 0) return false;

        // Basic check: selectors normally contain alphanumeric characters, dots, hashes, brackets or combinators
        return selector.Any(c => char.IsLetterOrDigit(c)) && selector.Length <= 1024;
    }
}
