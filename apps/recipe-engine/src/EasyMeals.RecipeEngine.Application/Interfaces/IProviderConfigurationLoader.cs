using EasyMeals.RecipeEngine.Domain.ValueObjects;

namespace EasyMeals.RecipeEngine.Application.Interfaces;

/// <summary>
/// Service for loading provider configurations from MongoDB.
/// </summary>
public interface IProviderConfigurationLoader
{
    /// <summary>
    /// Get configuration for a specific provider.
    /// </summary>
    Task<ProviderConfiguration?> GetByProviderIdAsync(
        string providerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all enabled provider configurations.
    /// </summary>
    Task<IEnumerable<ProviderConfiguration>> GetAllEnabledAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Load all configurations from MongoDB (typically called at startup).
    /// </summary>
    Task LoadConfigurationsAsync(CancellationToken cancellationToken = default);
}
