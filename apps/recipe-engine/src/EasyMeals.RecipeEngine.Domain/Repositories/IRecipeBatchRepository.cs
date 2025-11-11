using EasyMeals.RecipeEngine.Domain.Entities;
using EasyMeals.RecipeEngine.Domain.ValueObjects;

namespace EasyMeals.RecipeEngine.Domain.Repositories;

/// <summary>
///     Repository contract for RecipeBatch aggregate persistence.
/// </summary>
public interface IRecipeBatchRepository
{
    /// <summary>
    ///     Retrieve a batch by its unique identifier.
    /// </summary>
    Task<RecipeBatch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get the currently active (in-progress) batch for a provider.
    /// </summary>
    Task<RecipeBatch?> GetActiveAsync(string providerId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Create a new batch for a provider.
    /// </summary>
    Task<RecipeBatch> CreateAsync(
        string providerId,
        ProviderConfiguration config,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Save or update a batch.
    /// </summary>
    Task SaveAsync(RecipeBatch batch, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get recent batches for a provider (ordered by StartedAt descending).
    /// </summary>
    Task<IEnumerable<RecipeBatch>> GetRecentBatchesAsync(
        string providerId,
        int count,
        CancellationToken cancellationToken = default);
}