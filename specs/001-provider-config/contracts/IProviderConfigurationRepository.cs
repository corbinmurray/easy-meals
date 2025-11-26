// Contract: IProviderConfigurationRepository
// Feature: 001-provider-config
// Date: 2025-11-25
//
// This file defines the repository interface for provider configuration
// persistence. Implementation will be in EasyMeals.Persistence.Mongo.
//
// IMPORTANT: This interface operates on DOMAIN entities (ProviderConfiguration),
// NOT persistence documents. Mapping between domain and document types is the
// responsibility of the repository implementation in EasyMeals.Persistence.Mongo.

using EasyMeals.Domain.ProviderConfiguration;

namespace EasyMeals.Persistence.Abstractions.Repositories;

/// <summary>
/// Repository interface for provider configuration persistence operations.
/// Operates on domain entities; implementations handle mapping to/from persistence models.
/// </summary>
/// <remarks>
/// <para>
/// This interface provides domain-focused CRUD operations for <see cref="ProviderConfiguration"/>
/// entities plus specialized queries for common access patterns.
/// </para>
/// <para>
/// Implementations should:
/// <list type="bullet">
///   <item>Apply soft-delete filtering to all queries (honor <c>IsDeleted</c> flag)</item>
///   <item>Support optimistic concurrency via <c>ConcurrencyToken</c></item>
///   <item>Return enabled providers ordered by priority (descending)</item>
///   <item>Normalize provider names to lowercase before persistence</item>
///   <item>Handle domain ↔ document mapping internally</item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class RecipeDiscoveryService
/// {
///     private readonly IProviderConfigurationRepository _repository;
///     
///     public async Task DiscoverRecipesAsync(CancellationToken ct)
///     {
///         var providers = await _repository.GetAllEnabledAsync(ct);
///         foreach (var provider in providers)
///         {
///             // Process in priority order (domain entities)
///         }
///     }
/// }
/// </code>
/// </example>
public interface IProviderConfigurationRepository
{
    #region Read Operations

    /// <summary>
    /// Retrieves a provider configuration by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier (MongoDB ObjectId as string).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The provider configuration if found; otherwise, <c>null</c>.</returns>
    Task<ProviderConfiguration?> GetByIdAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// Retrieves all enabled provider configurations, ordered by priority descending.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A read-only list of enabled provider configurations, sorted by <c>Priority</c> 
    /// in descending order (highest priority first). Returns an empty list if no 
    /// enabled providers exist.
    /// </returns>
    /// <remarks>
    /// This is the primary query method used by the recipe engine during discovery.
    /// Results should be cached at the application layer (see <c>CachedProviderConfigurationRepository</c>).
    /// </remarks>
    Task<IReadOnlyList<ProviderConfiguration>> GetAllEnabledAsync(CancellationToken ct = default);

    /// <summary>
    /// Retrieves a provider configuration by its unique provider name.
    /// </summary>
    /// <param name="providerName">
    /// The unique provider identifier (e.g., "hellofresh", "allrecipes").
    /// Input is normalized to lowercase before lookup (case-insensitive).
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The provider configuration if found; otherwise, <c>null</c>.</returns>
    /// <remarks>
    /// <para>
    /// Provider names are unique business keys. Format: lowercase alphanumeric with 
    /// hyphens only (e.g., "hello-fresh", "all-recipes"). Regex: <c>^[a-z0-9-]+$</c>
    /// </para>
    /// <para>
    /// This method respects soft-delete filtering; deleted providers return <c>null</c>.
    /// </para>
    /// </remarks>
    Task<ProviderConfiguration?> GetByProviderNameAsync(string providerName, CancellationToken ct = default);

    /// <summary>
    /// Checks if a provider with the given name already exists.
    /// </summary>
    /// <param name="providerName">The provider name to check (normalized to lowercase).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <c>true</c> if a provider with the given name exists (even if disabled); 
    /// otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// Use this before creating a new provider to ensure uniqueness.
    /// Soft-deleted providers are NOT considered to exist.
    /// </remarks>
    Task<bool> ExistsByProviderNameAsync(string providerName, CancellationToken ct = default);

    #endregion

    #region Write Operations

    /// <summary>
    /// Adds a new provider configuration.
    /// </summary>
    /// <param name="entity">The provider configuration to add.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The ID of the created provider configuration.</returns>
    /// <exception cref="ArgumentException">If provider name already exists.</exception>
    Task<string> AddAsync(ProviderConfiguration entity, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing provider configuration.
    /// </summary>
    /// <param name="entity">The provider configuration with updated values.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="ConcurrencyException">If optimistic concurrency check fails.</exception>
    Task UpdateAsync(ProviderConfiguration entity, CancellationToken ct = default);

    /// <summary>
    /// Soft-deletes a provider configuration by ID.
    /// </summary>
    /// <param name="id">The unique identifier of the provider to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DeleteAsync(string id, CancellationToken ct = default);

    #endregion
}

/// <summary>
/// Extended repository interface with cache management capabilities.
/// Implemented by the caching decorator for admin/testing scenarios.
/// </summary>
public interface ICacheableProviderConfigurationRepository : IProviderConfigurationRepository
{
    /// <summary>
    /// Clears all cached provider configurations, forcing the next read to fetch from the database.
    /// </summary>
    /// <remarks>
    /// Use this method for:
    /// <list type="bullet">
    ///   <item>Admin operations requiring immediate config refresh</item>
    ///   <item>Integration tests to ensure fresh data</item>
    ///   <item>After bulk configuration updates</item>
    /// </list>
    /// In normal operation, rely on TTL-based expiration instead.
    /// </remarks>
    void ClearCache();
}
