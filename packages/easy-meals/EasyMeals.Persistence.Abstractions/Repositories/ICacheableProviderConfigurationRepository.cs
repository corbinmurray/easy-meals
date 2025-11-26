namespace EasyMeals.Persistence.Abstractions.Repositories;

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
