using EasyMeals.RecipeEngine.Domain.Interfaces;
using EasyMeals.RecipeEngine.Domain.ValueObjects;
using EasyMeals.RecipeEngine.Infrastructure.Discovery;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace EasyMeals.RecipeEngine.Infrastructure.HealthChecks;

public class DiscoveryServiceHealthCheck : IHealthCheck
{
	private readonly IDiscoveryServiceFactory _discoveryServiceFactory;

	public DiscoveryServiceHealthCheck(IDiscoveryServiceFactory discoveryServiceFactory) => _discoveryServiceFactory =
		discoveryServiceFactory ?? throw new ArgumentNullException(nameof(discoveryServiceFactory));

	public Task<HealthCheckResult> CheckHealthAsync(
		HealthCheckContext context,
		CancellationToken cancellationToken = default)
	{
		try
		{
			// Try to create discovery services to verify factory is functioning
			IDiscoveryService? staticService = _discoveryServiceFactory.CreateDiscoveryService(DiscoveryStrategy.Static);
			IDiscoveryService? dynamicService = _discoveryServiceFactory.CreateDiscoveryService(DiscoveryStrategy.Dynamic);
			IDiscoveryService? apiService = _discoveryServiceFactory.CreateDiscoveryService(DiscoveryStrategy.Api);

			if (staticService != null && dynamicService != null && apiService != null)
				return Task.FromResult(HealthCheckResult.Healthy(
					"Discovery services are available",
					new Dictionary<string, object>
					{
						{ "staticServiceAvailable", staticService != null },
						{ "dynamicServiceAvailable", dynamicService != null },
						{ "apiServiceAvailable", apiService != null }
					}));

			return Task.FromResult(HealthCheckResult.Degraded(
				"Some discovery services may not be available"));
		}
		catch (Exception ex)
		{
			return Task.FromResult(HealthCheckResult.Unhealthy(
				"Discovery services check failed",
				ex));
		}
	}
}