using System.Security.Cryptography;
using System.Text;
using EasyMeals.RecipeEngine.Application.Interfaces;
using EasyMeals.RecipeEngine.Domain.Entities;
using EasyMeals.RecipeEngine.Domain.Repositories;

namespace EasyMeals.RecipeEngine.Infrastructure.Fingerprinting;

/// <summary>
///     Service for generating content-based fingerprints for recipes using SHA256 hash.
///     Implements duplicate detection by comparing normalized content (URL + title + description).
/// </summary>
public class RecipeFingerprintService : IRecipeFingerprinter
{
    private readonly IRecipeFingerprintRepository _repository;

    public RecipeFingerprintService(IRecipeFingerprintRepository repository) =>
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));

    /// <summary>
    ///     T121: Generates a content-based fingerprint (SHA256 hash) for a recipe.
    ///     Normalizes URL (lowercase, removes query params), title (trim, lowercase),
    ///     and description (first 200 chars, trim, lowercase) before hashing.
    /// </summary>
    public string GenerateFingerprint(string url, string title, string description)
    {
        // Normalize URL: lowercase and remove query parameters
        string normalizedUrl = NormalizeUrl(url);

        // Normalize title: trim and lowercase
        string normalizedTitle = NormalizeTitle(title);

        // Normalize description: substring first 200 chars, trim, lowercase
        string normalizedDescription = NormalizeDescription(description);

        // Concatenate normalized components
        var contentToHash = $"{normalizedUrl}|{normalizedTitle}|{normalizedDescription}";

        // Compute SHA256 hash
        return ComputeSha256Hash(contentToHash);
    }

    /// <summary>
    ///     T122: Checks if a recipe with the given fingerprint has already been processed.
    ///     Queries the recipe_fingerprints MongoDB collection by hash.
    /// </summary>
    public async Task<bool> IsDuplicateAsync(
        string fingerprintHash,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fingerprintHash))
            throw new ArgumentException("Fingerprint hash cannot be null or empty", nameof(fingerprintHash));

        // Check if a fingerprint with this hash already exists in the database
        return await _repository.ExistsByHashAsync(fingerprintHash, cancellationToken);
    }

    /// <summary>
    ///     T123: Persists a recipe fingerprint to MongoDB after successful recipe processing.
    ///     Creates a RecipeFingerprint entity and saves via repository.
    /// </summary>
    public async Task StoreFingerprintAsync(
        string fingerprintHash,
        string providerId,
        string recipeUrl,
        Guid recipeId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fingerprintHash))
            throw new ArgumentException("Fingerprint hash cannot be null or empty", nameof(fingerprintHash));

        if (string.IsNullOrWhiteSpace(providerId))
            throw new ArgumentException("Provider ID cannot be null or empty", nameof(providerId));

        if (string.IsNullOrWhiteSpace(recipeUrl))
            throw new ArgumentException("Recipe URL cannot be null or empty", nameof(recipeUrl));

        if (recipeId == Guid.Empty)
            throw new ArgumentException("Recipe ID cannot be empty", nameof(recipeId));

        // Create RecipeFingerprint entity using factory method
        var fingerprint = RecipeFingerprint.Create(
            fingerprintHash,
            providerId,
            recipeUrl,
            recipeId);

        // Persist to MongoDB
        await _repository.SaveAsync(fingerprint, cancellationToken);
    }

    #region Private Helper Methods

    /// <summary>
    ///     Normalizes URL by converting to lowercase and removing query parameters.
    /// </summary>
    private static string NormalizeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return string.Empty;

        // Convert to lowercase
        url = url.ToLowerInvariant();

        // Remove query parameters (everything after '?')
        int queryIndex = url.IndexOf('?');
        if (queryIndex >= 0) url = url[..queryIndex];

        return url;
    }

    /// <summary>
    ///     Normalizes title by trimming whitespace and converting to lowercase.
    /// </summary>
    private static string NormalizeTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return string.Empty;

        return title.Trim().ToLowerInvariant();
    }

    /// <summary>
    ///     Normalizes description by taking first 200 characters, trimming, and converting to lowercase.
    /// </summary>
    private static string NormalizeDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return string.Empty;

        description = description.Trim();

        // Take first 200 characters
        if (description.Length > 200) description = description[..200];

        return description.ToLowerInvariant();
    }

    /// <summary>
    ///     Computes SHA256 hash of input string and returns as lowercase hexadecimal string.
    /// </summary>
    private static string ComputeSha256Hash(string input)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(input);
        byte[] hash = SHA256.HashData(bytes);

        // Convert to lowercase hex string
        var builder = new StringBuilder(hash.Length * 2);
        foreach (byte b in hash)
        {
            builder.Append(b.ToString("x2"));
        }

        return builder.ToString();
    }

    #endregion
}