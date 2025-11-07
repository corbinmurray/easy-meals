using EasyMeals.RecipeEngine.Application.Interfaces;
using EasyMeals.RecipeEngine.Domain.ValueObjects;
using EasyMeals.RecipeEngine.Infrastructure.Documents;
using EasyMeals.Shared.Data.Repositories;
using MongoDB.Driver;

namespace EasyMeals.RecipeEngine.Infrastructure.Services;

/// <summary>
/// Loads provider configurations from MongoDB.
/// </summary>
public class ProviderConfigurationLoader : IProviderConfigurationLoader
{
    private readonly IMongoRepository<ProviderConfigurationDocument> _repository;

    public ProviderConfigurationLoader(IMongoRepository<ProviderConfigurationDocument> repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<ProviderConfiguration?> GetByProviderIdAsync(
        string providerId,
        CancellationToken cancellationToken = default)
    {
        var document = await _repository.GetFirstOrDefaultAsync(
            d => d.ProviderId == providerId && d.Enabled,
            cancellationToken);

        return document != null ? ToDomain(document) : null;
    }

    public async Task<IEnumerable<ProviderConfiguration>> GetAllEnabledAsync(
        CancellationToken cancellationToken = default)
    {
        var documents = await _repository.GetAllAsync(d => d.Enabled, cancellationToken);

        return documents.Select(ToDomain).ToList();
    }

    public async Task LoadConfigurationsAsync(CancellationToken cancellationToken = default)
    {
        // Load all configurations to validate they parse correctly
        var configs = await GetAllEnabledAsync(cancellationToken);
        var configList = configs.ToList();

        if (configList.Count == 0)
        {
            throw new InvalidOperationException(
                "No enabled provider configurations found in MongoDB. " +
                "Please seed the provider_configurations collection.");
        }

        // Log successful load (in production, use ILogger)
        Console.WriteLine($"Loaded {configList.Count} provider configuration(s) from MongoDB");
    }

    private static ProviderConfiguration ToDomain(ProviderConfigurationDocument document)
    {
        // Parse the discovery strategy from string to enum
        if (!Enum.TryParse<DiscoveryStrategy>(document.DiscoveryStrategy, true, out var strategy))
        {
            throw new InvalidOperationException($"Invalid DiscoveryStrategy value: {document.DiscoveryStrategy}");
        }

        return new ProviderConfiguration(
            document.ProviderId,
            document.Enabled,
            strategy,
            document.RecipeRootUrl,
            document.BatchSize,
            document.TimeWindowMinutes,
            document.MinDelaySeconds,
            document.MaxRequestsPerMinute,
            document.RetryCount,
            document.RequestTimeoutSeconds
        );
    }
}
