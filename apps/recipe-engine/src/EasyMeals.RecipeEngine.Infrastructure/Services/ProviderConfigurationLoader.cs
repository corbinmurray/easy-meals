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
        var filter = Builders<ProviderConfigurationDocument>.Filter.And(
            Builders<ProviderConfigurationDocument>.Filter.Eq(d => d.ProviderId, providerId),
            Builders<ProviderConfigurationDocument>.Filter.Eq(d => d.Enabled, true)
        );

        var documents = await _repository.FindAsync(filter, cancellationToken);
        var document = documents.FirstOrDefault();

        return document != null ? ToDomain(document) : null;
    }

    public async Task<IEnumerable<ProviderConfiguration>> GetAllEnabledAsync(
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<ProviderConfigurationDocument>.Filter.Eq(d => d.Enabled, true);
        var documents = await _repository.FindAsync(filter, cancellationToken);

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
        return new ProviderConfiguration(
            document.ProviderId,
            document.RecipeRootUrl,
            document.DiscoveryStrategy,
            document.BatchSize,
            TimeSpan.FromMinutes(document.TimeWindowMinutes),
            document.MinDelaySeconds,
            document.MaxDelaySeconds,
            document.MaxRequestsPerMinute,
            document.RetryCount,
            document.RequestTimeoutSeconds
        );
    }
}
