using EasyMeals.RecipeEngine.Application.Interfaces;
using EasyMeals.RecipeEngine.Domain.ValueObjects;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EasyMeals.RecipeEngine.Infrastructure.Services;

/// <summary>
///     Hosted service that loads and validates provider configurations at startup.
/// </summary>
public class ProviderConfigurationHostedService : IHostedService
{
    private readonly IProviderConfigurationLoader _configurationLoader;
    private readonly ILogger<ProviderConfigurationHostedService> _logger;

    public ProviderConfigurationHostedService(
        IProviderConfigurationLoader configurationLoader,
        ILogger<ProviderConfigurationHostedService> logger)
    {
        _configurationLoader = configurationLoader ?? throw new ArgumentNullException(nameof(configurationLoader));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Loading provider configurations from MongoDB...");
            await _configurationLoader.LoadConfigurationsAsync(cancellationToken);

            // Get all loaded configurations to log details
            IEnumerable<ProviderConfiguration> configurations = await _configurationLoader.GetAllEnabledAsync(cancellationToken);
            List<ProviderConfiguration> configList = configurations.ToList();

            _logger.LogInformation(
                "Successfully loaded {Count} enabled provider configuration(s)",
                configList.Count);

            // Log each provider's settings (sanitize URLs)
            foreach (ProviderConfiguration config in configList)
            {
                _logger.LogInformation(
                    "Loaded provider {ProviderId}: Strategy={Strategy}, BatchSize={BatchSize}, " +
                    "TimeWindow={TimeWindow}min, MaxRequests={MaxRequests}/min",
                    config.ProviderId,
                    config.DiscoveryStrategy,
                    config.BatchSize,
                    config.TimeWindow.TotalMinutes,
                    config.MaxRequestsPerMinute);
            }

            // Fail fast if no enabled providers found
            if (configList.Count == 0)
            {
                _logger.LogError("No enabled provider configurations found. Recipe engine cannot start.");
                throw new InvalidOperationException(
                    "No enabled provider configurations found in MongoDB. " +
                    "Please seed the provider_configurations collection with at least one enabled provider.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load provider configurations at startup. Application will terminate.");
            throw; // Fail fast - don't start the application if configs can't be loaded
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Provider configuration hosted service stopping");
        return Task.CompletedTask;
    }
}