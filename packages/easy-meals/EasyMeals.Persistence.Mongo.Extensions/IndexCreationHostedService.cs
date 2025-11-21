using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EasyMeals.Persistence.Mongo.Extensions;

/// <summary>
///   Hosted service that creates indexes at application startup.
/// </summary>
/// <param name="provider"></param>
/// <param name="indexCreators"></param>
/// <param name="logger"></param>
public class IndexCreationHostedService(
	IServiceProvider provider,
	IEnumerable<Func<IServiceProvider, Task>> indexCreators,
	ILogger<IndexCreationHostedService> logger)
	: IHostedService
{
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		using IServiceScope scope = provider.CreateScope();
		IServiceProvider sp = scope.ServiceProvider;

		foreach (Func<IServiceProvider, Task> create in indexCreators)
		{
			try
			{
				await create(sp).WaitAsync(cancellationToken);
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Failed to create indexes during startup.");
				throw;
			}
		}
	}

	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}