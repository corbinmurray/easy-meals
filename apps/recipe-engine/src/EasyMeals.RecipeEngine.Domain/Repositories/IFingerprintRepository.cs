using EasyMeals.RecipeEngine.Domain.Entities;

namespace EasyMeals.RecipeEngine.Domain.Repositories;

/// <summary>
///     Repository interface for managing Fingerprint entities
///     Supports the pre-processing workflow for scraped content
/// </summary>
public interface IFingerprintRepository
{
    /// <summary>
    ///     Adds a new fingerprint
    /// </summary>
    Task<Fingerprint> AddAsync(Fingerprint fingerprint, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Updates an existing fingerprint
    /// </summary>
    Task<Fingerprint> UpdateAsync(Fingerprint fingerprint, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Checks if a fingerprint hash already exists, ensures we don't process a duplicate recipe
    /// </summary>
    /// <param name="fingerprintHash"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<bool> ExistsAsync(string fingerprintHash, CancellationToken cancellationToken = default);
}