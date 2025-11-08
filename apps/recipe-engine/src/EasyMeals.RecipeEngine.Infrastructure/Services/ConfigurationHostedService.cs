using EasyMeals.RecipeEngine.Application.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EasyMeals.RecipeEngine.Infrastructure.Services;

/// <summary>
///     Hosted service that loads provider configurations at startup.
/// </summary>
public class ConfigurationHostedService : IHostedService
{
	private readonly IProviderConfigurationLoader _configurationLoader;
	private readonly ILogger<ConfigurationHostedService> _logger;

	public ConfigurationHostedService(
		IProviderConfigurationLoader configurationLoader,
		ILogger<ConfigurationHostedService> logger)
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
			_logger.LogInformation("Provider configurations loaded successfully");
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to load provider configurations at startup");
			throw;
		}
	}

	public Task StopAsync(CancellationToken cancellationToken)
	{
		_logger.LogInformation("Stopping configuration hosted service");
		return Task.CompletedTask;
	}
}